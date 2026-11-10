using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Tenlith.Api.Tests;

public sealed class OAuthTests
{
    [Fact]
    public async Task GitHub_callback_uses_configured_public_origin_instead_of_proxy_or_request_host()
    {
        await using var root = new ApiFactory();
        await using var f = root.WithWebHostBuilder(b =>
            b.UseSetting("GitHub:ClientId", "test-client")
                .UseSetting("GitHub:ClientSecret", "test-secret")
                .UseSetting("App:PublicUrl", "https://tenlith.example")
        );
        using var c = f.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        c.DefaultRequestHeaders.Host = "attacker.example";
        var response = await c.GetAsync("/api/auth/github");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("github.com", location.Host);
        Assert.Equal(
            "https://tenlith.example/api/auth/github/callback",
            QueryHelpers.ParseQuery(location.Query)["redirect_uri"]
        );
    }
}
