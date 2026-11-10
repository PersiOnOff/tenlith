using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tenlith.Api;
using Xunit;

namespace Tenlith.Api.Tests;

public sealed class CheckoutTests
{
    sealed class Provider : HttpMessageHandler
    {
        public int Checkouts;
        public string SessionStatus = "open";
        public List<string> Keys = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage r,
            CancellationToken ct
        )
        {
            object result;
            if (r.RequestUri!.AbsolutePath.EndsWith("/customers"))
                result = new { id = "cus_new" };
            else if (r.RequestUri.AbsolutePath.EndsWith("/subscriptions"))
                result = new { data = Array.Empty<object>(), has_more = false };
            else if (
                r.Method == HttpMethod.Get
                && r.RequestUri.AbsolutePath.Contains("/checkout/sessions/")
            )
                result = new { status = SessionStatus };
            else
            {
                Checkouts++;
                Keys.Add(r.Headers.GetValues("Idempotency-Key").Single());
                result = new
                {
                    id = "cs_pending",
                    url = "https://checkout.stripe.com/pending",
                    expires_at = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds(),
                };
            }
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result) }
            );
        }
    }

    [Theory]
    [InlineData("open", 1)]
    [InlineData("complete", 2)]
    public async Task Repeated_checkout_reuses_only_open_payable_sessions(
        string sessionStatus,
        int expectedSessions
    )
    {
        await using var root = new ApiFactory();
        var provider = new Provider { SessionStatus = sessionStatus };
        await using var f = root.WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration(
                    (_, c) =>
                        c.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["Stripe:SecretKey"] = "sk_test",
                                ["Stripe:WebhookSecret"] = "whsec_test",
                                ["Stripe:PriceId"] = "price_pro",
                            }
                        )
                )
                .ConfigureServices(s =>
                    s.AddHttpClient<StripeGateway>()
                        .ConfigurePrimaryHttpMessageHandler(() => provider)
                )
        );
        using var c = f.CreateClient();
        c.DefaultRequestHeaders.Add("X-Tenlith-Client", "web");
        var demo = await c.PostAsJsonAsync("/api/auth/demo", new { });
        var session = await demo.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new(
            "Bearer",
            session.GetProperty("accessToken").GetString()
        );
        var id = (await c.GetFromJsonAsync<JsonElement>("/api/organizations"))[0]
            .GetProperty("id")
            .GetGuid();
        for (var i = 0; i < 2; i++)
        {
            var response = await c.PostAsJsonAsync(
                $"/api/organizations/{id}/billing/checkout",
                new { }
            );
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(
                "https://checkout.stripe.com/pending",
                (await response.Content.ReadFromJsonAsync<JsonElement>())
                    .GetProperty("url")
                    .GetString()
            );
        }
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await c.DeleteAsync($"/api/organizations/{id}")).StatusCode
        );
        Assert.Equal(expectedSessions, provider.Checkouts);
        Assert.Equal($"tenlith-checkout-{id}-0", provider.Keys[0]);
        if (expectedSessions == 2)
            Assert.Equal($"tenlith-checkout-{id}-1", provider.Keys[1]);
        using var scope = f.Services.CreateScope();
        var org = await scope
            .ServiceProvider.GetRequiredService<AppDb>()
            .Organizations.SingleAsync();
        Assert.Equal("cs_pending", org.PendingCheckoutId);
        Assert.True(org.PendingCheckoutExpiresAt > DateTime.UtcNow.AddHours(22));
    }
}
