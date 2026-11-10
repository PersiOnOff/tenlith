using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Tenlith.Api;
using Xunit;

namespace Tenlith.Api.Tests;

public sealed class WorkerTests
{
    sealed class Mail : IMailTransport
    {
        public int Sends;
        public bool Fail = true;

        public Task Send(MailMessageData data, Guid id, CancellationToken ct)
        {
            Sends++;
            if (Fail)
                throw new IOException("temporary SMTP outage");
            return Task.CompletedTask;
        }
    }

    sealed class StripeHandler : HttpMessageHandler
    {
        public string Status = "active";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(
                            new
                            {
                                has_more = false,
                                data = new[]
                                {
                                    new
                                    {
                                        id = "sub_test",
                                        status = Status,
                                        items = new
                                        {
                                            data = new[]
                                            {
                                                new { price = new { id = "price_pro" } },
                                            },
                                        },
                                    },
                                },
                            }
                        )
                    ),
                }
            );
    }

    static IConfiguration Config() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Stripe:SecretKey"] = "sk_test",
                    ["Stripe:WebhookSecret"] = "whsec_test",
                    ["Stripe:PriceId"] = "price_pro",
                }
            )
            .Build();

    [Fact]
    public async Task Production_rejects_demo_and_simulation_at_startup()
    {
        await using var f = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseEnvironment("Production")
                .UseSetting("Jwt:Key", new string('x', 64))
                .UseSetting("Demo:Enabled", "true")
                .UseSetting("Billing:Simulation", "true")
                .UseSetting("App:PublicUrl", "https://example.com")
                .ConfigureAppConfiguration(
                    (_, c) =>
                        c.AddInMemoryCollection(
                            new Dictionary<string, string?>
                            {
                                ["Jwt:Key"] = new string('x', 64),
                                ["ConnectionStrings:Database"] = "Host=localhost;Database=unused",
                                ["Demo:Enabled"] = "true",
                                ["Billing:Simulation"] = "true",
                                ["App:PublicUrl"] = "https://example.com",
                            }
                        )
                )
        );
        var ex = Assert.ThrowsAny<Exception>(() => f.CreateClient());
        Assert.Equal("Demo and billing simulation are only available in Development.", ex.Message);
    }

    [Fact]
    public async Task Durable_jobs_retry_failure_and_do_not_resend_completed_work()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var db = new AppDb(
            new DbContextOptionsBuilder<AppDb>().UseSqlite(conn).Options
        );
        await db.Database.EnsureCreatedAsync();
        var job = new DurableJob
        {
            Payload = JsonSerializer.Serialize(
                new MailMessageData("test@example.com", "Test", "A message")
            ),
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        var mail = new Mail();
        var runner = new JobRunner(
            db,
            mail,
            new StripeGateway(new HttpClient(), Config()),
            NullLogger<JobRunner>.Instance
        );
        Assert.True(await runner.RunOne());
        db.ChangeTracker.Clear();
        var failed = await db.Jobs.SingleAsync();
        Assert.Equal(1, failed.Attempts);
        Assert.Null(failed.CompletedAt);
        Assert.True(failed.AvailableAt > DateTime.UtcNow);
        Assert.Equal("IOException", failed.LastError);
        Assert.False(await runner.RunOne());
        failed.AvailableAt = DateTime.UtcNow.AddSeconds(-1);
        await db.SaveChangesAsync();
        mail.Fail = false;
        Assert.True(await runner.RunOne());
        Assert.False(await runner.RunOne());
        Assert.Equal(2, mail.Sends);
        db.ChangeTracker.Clear();
        Assert.NotNull((await db.Jobs.SingleAsync()).CompletedAt);
    }

    [Fact]
    public async Task Reconciliation_reads_current_provider_state_for_duplicate_or_out_of_order_events()
    {
        await using var conn = new SqliteConnection("Data Source=:memory:");
        await conn.OpenAsync();
        await using var db = new AppDb(
            new DbContextOptionsBuilder<AppDb>().UseSqlite(conn).Options
        );
        await db.Database.EnsureCreatedAsync();
        var org = new Organization
        {
            Name = "Company",
            Slug = "company",
            StripeCustomerId = "cus_test",
        };
        db.Organizations.Add(org);
        await db.SaveChangesAsync();
        var handler = new StripeHandler();
        var stripe = new StripeGateway(new HttpClient(handler), Config());
        await stripe.Reconcile(db, "cus_test", default);
        Assert.Equal("Pro", org.Plan);
        await stripe.Reconcile(db, "cus_test", default);
        Assert.Equal(1, await db.AuditEvents.CountAsync());
        handler.Status = "past_due";
        await stripe.Reconcile(db, "cus_test", default);
        Assert.Equal("Free", org.Plan);
        Assert.Equal("sub_test", org.StripeSubscriptionId);
        handler.Status = "canceled";
        await stripe.Reconcile(db, "cus_test", default);
        Assert.Equal("Free", org.Plan);
        await stripe.Reconcile(db, "cus_test", default);
        Assert.Equal(3, await db.AuditEvents.CountAsync());
    }

    [Fact]
    public void Webhook_verification_accepts_valid_signature_and_rejects_tampering_or_stale_timestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var raw = "{\"id\":\"evt_test\"}";
        var timestamp = now.ToUnixTimeSeconds();
        var signature = Convert.ToHexString(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes("secret"),
                Encoding.UTF8.GetBytes(timestamp + "." + raw)
            )
        );
        var header = $"t={timestamp},v1={signature}";
        Assert.True(StripeGateway.Verify(raw, header, "secret", now));
        Assert.False(StripeGateway.Verify(raw + " ", header, "secret", now));
        Assert.False(StripeGateway.Verify(raw, header, "secret", now.AddMinutes(6)));
        Assert.False(StripeGateway.Verify(raw, header, "wrong", now));
    }
}
