using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Tenlith.Api;
using Xunit;

namespace Tenlith.Api.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TENLITH_TEST_POSTGRES")))
            Skip =
                "Set TENLITH_TEST_POSTGRES to an administrator PostgreSQL connection string to run the isolated-database concurrency check.";
    }
}

public sealed class PostgreSqlTests
{
    sealed class OrderedProvider : HttpMessageHandler
    {
        public readonly TaskCompletionSource FirstFetch = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        public readonly TaskCompletionSource ReleaseFirst = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        public int Fetches;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        )
        {
            var index = Interlocked.Increment(ref Fetches);
            if (index == 1)
            {
                FirstFetch.SetResult();
                await ReleaseFirst.Task.WaitAsync(ct);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(
                    new
                    {
                        has_more = false,
                        data = new[]
                        {
                            new
                            {
                                id = "sub_real_lock_test",
                                status = index == 1 ? "active" : "canceled",
                                items = new
                                {
                                    data = new[] { new { price = new { id = "price_pro" } } },
                                },
                            },
                        },
                    }
                ),
            };
        }
    }

    [PostgreSqlFact]
    public async Task Concurrent_reconciliation_serializes_provider_fetches_and_cannot_restore_stale_paid_state()
    {
        var root = Environment.GetEnvironmentVariable("TENLITH_TEST_POSTGRES")!;
        var name = "tenlith_test_" + Guid.NewGuid().ToString("N");
        await using var admin = new NpgsqlConnection(root);
        await admin.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin))
            await create.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(root) { Database = name, Pooling = false };
        var options = new DbContextOptionsBuilder<AppDb>()
            .UseNpgsql(builder.ConnectionString)
            .Options;
        try
        {
            await using (var setup = new AppDb(options))
            {
                await setup.Database.MigrateAsync();
                setup.Organizations.Add(
                    new Organization
                    {
                        Name = "Concurrency",
                        Slug = "concurrency",
                        StripeCustomerId = "cus_lock_test",
                    }
                );
                await setup.SaveChangesAsync();
            }
            var provider = new OrderedProvider();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["Stripe:SecretKey"] = "sk_test",
                        ["Stripe:WebhookSecret"] = "whsec_test",
                        ["Stripe:PriceId"] = "price_pro",
                    }
                )
                .Build();
            var gateway = new StripeGateway(new HttpClient(provider), config);
            await using var firstDb = new AppDb(options);
            await using var secondDb = new AppDb(options);
            var first = gateway.Reconcile(firstDb, "cus_lock_test", default);
            await provider.FirstFetch.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var second = gateway.Reconcile(secondDb, "cus_lock_test", default);
            await Task.WhenAny(second, Task.Delay(200));
            provider.ReleaseFirst.SetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(15));
            await using var check = new AppDb(options);
            Assert.Equal("Free", (await check.Organizations.SingleAsync()).Plan);
            Assert.Equal(2, provider.Fetches);
        }
        finally
        {
            await using var drop = new NpgsqlCommand(
                $"DROP DATABASE \"{name}\" WITH (FORCE)",
                admin
            );
            await drop.ExecuteNonQueryAsync();
        }
    }
}
