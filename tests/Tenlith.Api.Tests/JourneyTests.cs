using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tenlith.Api;
using Xunit;

namespace Tenlith.Api.Tests;

public sealed class JourneyTests
{
    static async Task<Guid> Org(HttpClient c) =>
        (await c.GetFromJsonAsync<JsonElement>("/api/organizations"))[0]
            .GetProperty("id")
            .GetGuid();

    static Dictionary<string, string> Query(string body)
    {
        var uri = new Uri(body[(body.IndexOf("http", StringComparison.Ordinal))..]);
        return Microsoft
            .AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query)
            .ToDictionary(x => x.Key, x => x.Value.ToString());
    }

    static async Task<MailMessageData> LatestMail(ApiFactory f, string email)
    {
        using var s = f.Services.CreateScope();
        var jobs = await s
            .ServiceProvider.GetRequiredService<AppDb>()
            .Jobs.OrderByDescending(x => x.AvailableAt)
            .ToListAsync();
        return jobs.Select(x => JsonSerializer.Deserialize<MailMessageData>(x.Payload)!)
            .First(x => x.To == email);
    }

    static async Task<HttpClient> Register(ApiFactory f, string email)
    {
        var c = f.Client();
        var r = await c.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                name = "Test member",
                email,
                password = "Good-password123!",
            }
        );
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        var q = Query((await LatestMail(f, email)).Body);
        Assert.True(
            (
                await c.PostAsJsonAsync(
                    "/api/auth/verify-email",
                    new { userId = q["userId"], token = q["token"] }
                )
            ).IsSuccessStatusCode
        );
        var login = await c.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password = "Good-password123!" }
        );
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var j = await login.Content.ReadFromJsonAsync<JsonElement>();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            j.GetProperty("accessToken").GetString()
        );
        c.DefaultRequestHeaders.Add(
            "Cookie",
            login.Headers.GetValues("Set-Cookie").First().Split(';')[0]
        );
        return c;
    }

    [Fact]
    public async Task Invitation_is_verified_email_bound_single_use_and_member_cannot_write()
    {
        await using var f = new ApiFactory();
        using var owner = await f.Demo();
        using var member = await Register(f, "member@example.com");
        using var wrong = await Register(f, "wrong@example.com");
        var id = await Org(owner);
        Assert.True(
            (
                await owner.PostAsJsonAsync(
                    $"/api/organizations/{id}/invitations",
                    new { email = "member@example.com", role = "Member" }
                )
            ).IsSuccessStatusCode
        );
        var token = Query((await LatestMail(f, "member@example.com")).Body)["token"];
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await wrong.PostAsJsonAsync("/api/invitations/accept", new { token })).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await member.PostAsJsonAsync("/api/invitations/accept", new { token })).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await member.PostAsJsonAsync("/api/invitations/accept", new { token })).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.OK,
            (await member.GetAsync($"/api/organizations/{id}/projects")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (
                await member.PostAsJsonAsync(
                    $"/api/organizations/{id}/projects",
                    new { name = "Denied" }
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (
                await member.PostAsJsonAsync(
                    $"/api/organizations/{id}/billing/simulate",
                    new { plan = "Pro" }
                )
            ).StatusCode
        );
    }

    [Fact]
    public async Task Owner_cannot_be_removed_and_transfer_revokes_old_owners_privileges()
    {
        await using var f = new ApiFactory();
        using var owner = await f.Demo();
        using var next = await Register(f, "next@example.com");
        var id = await Org(owner);
        var members = await owner.GetFromJsonAsync<JsonElement>($"/api/organizations/{id}/members");
        var ownerId = members[0].GetProperty("id").GetGuid();
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await owner.DeleteAsync($"/api/organizations/{id}/members/{ownerId}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (
                await owner.PatchAsJsonAsync(
                    $"/api/organizations/{id}/members/{ownerId}",
                    new { role = "Member" }
                )
            ).StatusCode
        );
        await owner.PostAsJsonAsync(
            $"/api/organizations/{id}/invitations",
            new { email = "next@example.com", role = "Admin" }
        );
        var token = Query((await LatestMail(f, "next@example.com")).Body)["token"];
        await next.PostAsJsonAsync("/api/invitations/accept", new { token });
        members = await owner.GetFromJsonAsync<JsonElement>($"/api/organizations/{id}/members");
        var target = members
            .EnumerateArray()
            .Single(x => x.GetProperty("email").GetString() == "next@example.com")
            .GetProperty("id")
            .GetGuid();
        Assert.Equal(
            HttpStatusCode.NoContent,
            (
                await owner.PostAsJsonAsync(
                    $"/api/organizations/{id}/transfer-ownership",
                    new { memberId = target }
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await owner.DeleteAsync($"/api/organizations/{id}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await next.DeleteAsync($"/api/organizations/{id}")).StatusCode
        );
    }

    [Fact]
    public async Task Password_reset_is_single_use_and_revokes_access_and_refresh_sessions()
    {
        await using var f = new ApiFactory();
        using var c = await Register(f, "reset@example.com");
        Assert.Equal(
            HttpStatusCode.OK,
            (
                await c.PostAsJsonAsync(
                    "/api/auth/forgot-password",
                    new { email = "reset@example.com" }
                )
            ).StatusCode
        );
        var q = Query((await LatestMail(f, "reset@example.com")).Body);
        var request = new
        {
            email = "reset@example.com",
            token = q["token"],
            password = "Replacement-password123!",
        };
        Assert.Equal(
            HttpStatusCode.OK,
            (await c.PostAsJsonAsync("/api/auth/reset-password", request)).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await c.PostAsJsonAsync("/api/auth/reset-password", request)).StatusCode
        );
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await c.PostAsJsonAsync("/api/auth/refresh", new { })).StatusCode
        );
    }

    [Fact]
    public async Task Expired_invitation_is_rejected_and_foreign_project_id_cannot_be_mutated()
    {
        await using var f = new ApiFactory();
        using var a = await f.Demo();
        using var b = await f.Demo();
        var aid = await Org(a);
        var bid = await Org(b);
        var pid = (await a.GetFromJsonAsync<JsonElement>($"/api/organizations/{aid}/projects"))[0]
            .GetProperty("id")
            .GetGuid();
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await b.PatchAsJsonAsync(
                    $"/api/organizations/{bid}/projects/{pid}",
                    new { name = "Intrusion" }
                )
            ).StatusCode
        );
        using var recipient = await Register(f, "expired@example.com");
        await a.PostAsJsonAsync(
            $"/api/organizations/{aid}/invitations",
            new { email = "expired@example.com", role = "Member" }
        );
        var token = Query((await LatestMail(f, "expired@example.com")).Body)["token"];
        using (var s = f.Services.CreateScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDb>();
            await db.Invitations.ExecuteUpdateAsync(x =>
                x.SetProperty(i => i.ExpiresAt, DateTime.UtcNow.AddDays(-1))
            );
        }
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await recipient.PostAsJsonAsync("/api/invitations/accept", new { token })).StatusCode
        );
    }
}
