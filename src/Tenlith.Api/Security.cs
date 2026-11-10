using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Tenlith.Api;

public sealed record TokenSettings(string Key);

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public static class Rules
{
    public static bool IsWriteConflict(Exception error)
    {
        for (Exception? current = error; current != null; current = current.InnerException)
            if (
                current is Npgsql.PostgresException postgres
                && postgres.SqlState is "40001" or "40P01"
            )
                return true;
        return false;
    }

    public static string Text(string? text, string field, int max = 100)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > max)
            throw new ApiException(
                400,
                $"{field} is required and must be at most {max} characters."
            );
        return text.Trim();
    }

    public static string Email(string? email)
    {
        var value = Text(email, "Email", 254);
        if (
            !System.Net.Mail.MailAddress.TryCreate(value, out var parsed)
            || parsed.Address != value
        )
            throw new ApiException(400, "Enter a valid email.");
        return value.ToLowerInvariant();
    }

    public static Guid UserId(this HttpContext c) =>
        Guid.Parse(c.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string Hash(string? token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Text(token, "Token", 4096))));

    public static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static void Identity(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new ApiException(400, string.Join(" ", result.Errors.Select(x => x.Description)));
    }
}

public sealed class AuthService(AppDb db, TokenSettings settings, IWebHostEnvironment env)
{
    public object UserDto(User user) =>
        new
        {
            id = user.Id,
            name = user.Name,
            email = user.Email,
        };

    public async Task<object> Session(User user, HttpContext ctx, Guid? family = null)
    {
        var raw = Rules.Secret();
        db.RefreshSessions.Add(
            new RefreshSession
            {
                UserId = user.Id,
                FamilyId = family ?? Guid.NewGuid(),
                TokenHash = Rules.Hash(raw),
                ExpiresAt = DateTime.UtcNow.AddDays(14),
            }
        );
        await db.SaveChangesAsync();
        ctx.Response.Cookies.Append(
            "tenlith.refresh",
            raw,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = !env.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Path = "/api/auth",
                Expires = DateTimeOffset.UtcNow.AddDays(14),
            }
        );
        var token = new JwtSecurityToken(
            "tenlith",
            "tenlith-web",
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("stamp", user.SecurityStamp ?? ""),
            },
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)),
                SecurityAlgorithms.HmacSha256
            )
        );
        return new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(token),
            user = UserDto(user),
        };
    }

    public async Task Revoke(Guid user)
    {
        await db
            .RefreshSessions.Where(x => x.UserId == user && !x.Revoked)
            .ExecuteUpdateAsync(x => x.SetProperty(r => r.Revoked, true));
    }

    public void Mail(string to, string subject, string body)
    {
        db.Jobs.Add(
            new DurableJob
            {
                Payload = System.Text.Json.JsonSerializer.Serialize(
                    new MailMessageData(to, subject, body)
                ),
            }
        );
    }
}
