using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Tenlith.Api;
using Xunit;

namespace Tenlith.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");

    public ApiFactory()
    {
        connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder b) =>
        b.UseEnvironment("Development")
            .ConfigureLogging(l => l.ClearProviders())
            .ConfigureAppConfiguration(
                (_, c) =>
                    c.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["Demo:Enabled"] = "true",
                            ["Billing:Simulation"] = "true",
                            ["Jwt:Key"] =
                                "test-key-with-at-least-64-characters-for-tenlith-http-integration-tests",
                            ["ConnectionStrings:Database"] = "Host=localhost;Database=not-used",
                            ["Mail:Host"] = "localhost",
                        }
                    )
            )
            .ConfigureServices(s =>
            {
                s.AddDataProtection().UseEphemeralDataProtectionProvider();
                s.RemoveAll<DbContextOptions<AppDb>>();
                s.RemoveAll<IDbContextOptionsConfiguration<AppDb>>();
                s.AddDbContext<AppDb>(o => o.UseSqlite(connection));
            });

    public HttpClient Client()
    {
        var c = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        c.DefaultRequestHeaders.Add("X-Tenlith-Client", "web");
        return c;
    }

    public async Task<HttpClient> Demo()
    {
        var c = Client();
        var r = await c.PostAsJsonAsync("/api/auth/demo", new { });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var j = await r.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            j.GetProperty("accessToken").GetString()
        );
        c.DefaultRequestHeaders.Add(
            "Cookie",
            r.Headers.GetValues("Set-Cookie").First().Split(';')[0]
        );
        return c;
    }
}

public sealed class SecurityTests
{
    static async Task<Guid> Org(HttpClient c) =>
        (await c.GetFromJsonAsync<JsonElement>("/api/organizations"))[0]
            .GetProperty("id")
            .GetGuid();

    [Fact]
    public async Task Demo_creates_isolated_sessions_and_blocks_foreign_tenant_access()
    {
        await using var f = new ApiFactory();
        using var a = await f.Demo();
        using var b = await f.Demo();
        var id = await Org(a);
        Assert.NotEqual(id, await Org(b));
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await b.GetAsync($"/api/organizations/{id}/projects")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (
                await b.PostAsJsonAsync(
                    $"/api/organizations/{id}/projects",
                    new { name = "Stolen" }
                )
            ).StatusCode
        );
    }

    [Fact]
    public async Task Refresh_replay_revokes_successor_and_logout_revokes_family()
    {
        await using var f = new ApiFactory();
        using var c = await f.Demo();
        var old = c.DefaultRequestHeaders.GetValues("Cookie").Single();
        var r = await c.PostAsJsonAsync("/api/auth/refresh", new { });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var fresh = r.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await c.PostAsJsonAsync("/api/auth/refresh", new { })).StatusCode
        );
        c.DefaultRequestHeaders.Remove("Cookie");
        c.DefaultRequestHeaders.Add("Cookie", fresh);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await c.PostAsJsonAsync("/api/auth/refresh", new { })).StatusCode
        );
    }

    [Fact]
    public async Task Mutations_require_client_header_and_reject_foreign_origin()
    {
        await using var f = new ApiFactory();
        using var c = f.CreateClient();
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await c.PostAsJsonAsync("/api/auth/demo", new { })).StatusCode
        );
        c.DefaultRequestHeaders.Add("X-Tenlith-Client", "web");
        c.DefaultRequestHeaders.Add("Origin", "https://evil.example");
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await c.PostAsJsonAsync("/api/auth/demo", new { })).StatusCode
        );
    }

    [Fact]
    public async Task Free_project_quota_is_enforced_and_simulation_upgrades_it()
    {
        await using var f = new ApiFactory();
        using var c = await f.Demo();
        var id = await Org(c);
        var initial = (
            await c.GetFromJsonAsync<JsonElement>($"/api/organizations/{id}/projects")
        ).GetArrayLength();
        for (var i = initial; i < 3; i++)
            Assert.True(
                (
                    await c.PostAsJsonAsync(
                        $"/api/organizations/{id}/projects",
                        new { name = $"Project {i}" }
                    )
                ).IsSuccessStatusCode
            );
        Assert.Equal(
            HttpStatusCode.Conflict,
            (
                await c.PostAsJsonAsync(
                    $"/api/organizations/{id}/projects",
                    new { name = "Too many" }
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await c.PostAsJsonAsync(
                    $"/api/organizations/{id}/billing/simulate",
                    new { plan = "Pro" }
                )
            ).StatusCode
        );
        Assert.True(
            (
                await c.PostAsJsonAsync(
                    $"/api/organizations/{id}/projects",
                    new { name = "Paid project" }
                )
            ).IsSuccessStatusCode
        );
    }

    [Fact]
    public async Task Invalid_login_is_generic_and_unverified_accounts_cannot_login()
    {
        await using var f = new ApiFactory();
        using var c = f.Client();
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await c.PostAsJsonAsync(
                    "/api/auth/register",
                    new
                    {
                        name = "Alice",
                        email = "alice@example.com",
                        password = "Good-password123!",
                    }
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (
                await c.PostAsJsonAsync(
                    "/api/auth/login",
                    new { email = "alice@example.com", password = "Good-password123!" }
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (
                await c.PostAsJsonAsync(
                    "/api/auth/login",
                    new { email = "nobody@example.com", password = "bad" }
                )
            ).StatusCode
        );
    }
}
