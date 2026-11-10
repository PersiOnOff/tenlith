using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tenlith.Api;
using Xunit;

namespace Tenlith.Api.Tests;

public sealed class BillingTests
{
    [Fact]
    public async Task Unconfigured_stripe_fails_closed_and_unsigned_webhooks_are_rejected()
    {
        await using var f = new ApiFactory();
        using var c = await f.Demo();
        var id = (await c.GetFromJsonAsync<JsonElement>("/api/organizations"))[0]
            .GetProperty("id")
            .GetGuid();
        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            (
                await c.PostAsJsonAsync($"/api/organizations/{id}/billing/checkout", new { })
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await c.PostAsJsonAsync(
                    "/api/billing/webhook",
                    new { id = "evt_fake", type = "customer.subscription.updated" }
                )
            ).StatusCode
        );
    }

    [Fact]
    public async Task Registration_queues_durable_mail_without_plaintext_password_or_refresh_token()
    {
        await using var f = new ApiFactory();
        using var c = f.Client();
        var response = await c.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                name = "Mail recipient",
                email = "mail@example.com",
                password = "Good-password123!",
            }
        );
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        using var s = f.Services.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<AppDb>();
        var job = await db.Jobs.SingleAsync();
        Assert.Equal("mail", job.Kind);
        Assert.Contains("verify-email", job.Payload);
        Assert.DoesNotContain("Good-password123!", job.Payload);
        Assert.Null(job.CompletedAt);
    }
}
