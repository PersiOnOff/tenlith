using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Tenlith.Api.Tests;

public sealed class ProxyTests
{
    [Theory]
    [InlineData("10.42.0.2", 200)]
    [InlineData("10.42.0.3", 429)]
    public async Task Only_trusted_proxy_can_supply_distinct_client_rate_limit_budgets(
        string peer,
        int finalStatus
    )
    {
        await using var root = new ApiFactory();
        await using var app = root.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?> { ["Proxy:KnownProxies:0"] = "10.42.0.2" }
                    )
            )
        );
        _ = app.CreateClient();
        int status = 0;
        for (var i = 1; i <= 41; i++)
        {
            var response = await app.Server.SendAsync(context =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
                context.Request.Method = "GET";
                context.Request.Path = "/api/auth/config";
                context.Request.Headers["X-Forwarded-For"] = $"203.0.113.{i}";
                context.Request.Headers["X-Forwarded-Proto"] = "https";
            });
            status = response.Response.StatusCode;
        }
        Assert.Equal(finalStatus, status);
    }
}
