using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Tenlith.Api.Tests;

public sealed class InputTests
{
    [Fact]
    public async Task Null_action_tokens_are_validation_errors()
    {
        await using var f = new ApiFactory();
        using var c = await f.Demo();
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await c.PostAsJsonAsync("/api/invitations/accept", new { token = (string?)null })
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await c.PostAsJsonAsync(
                    "/api/auth/verify-email",
                    new { userId = Guid.NewGuid(), token = (string?)null }
                )
            ).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await c.PostAsJsonAsync(
                    "/api/auth/reset-password",
                    new
                    {
                        email = "test@example.com",
                        token = (string?)null,
                        password = (string?)null,
                    }
                )
            ).StatusCode
        );
    }
}
