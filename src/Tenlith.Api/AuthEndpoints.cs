using System.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Tenlith.Api;

public sealed class WriteTransactionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext c,
        EndpointFilterDelegate next
    )
    {
        if (c.HttpContext.Request.Method is "GET" or "HEAD")
            return await next(c);
        var db = c.HttpContext.RequestServices.GetRequiredService<AppDb>();
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable
        );
        var result = await next(c);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return result;
    }
}

public record RegisterRequest(string? Name, string? Email, string? Password);

public record LoginRequest(string? Email, string? Password);

public record VerifyRequest(Guid UserId, string Token);

public record EmailRequest(string? Email);

public record ResetRequest(string? Email, string Token, string Password);

public static class AuthEndpoints
{
    public static void MapAuth(this WebApplication app)
    {
        var g = app.MapGroup("/api/auth")
            .RequireRateLimiting("auth")
            .AddEndpointFilter<WriteTransactionFilter>();
        g.MapGet(
            "/config",
            (IConfiguration c, IWebHostEnvironment e) =>
                new
                {
                    githubEnabled = !string.IsNullOrWhiteSpace(c["GitHub:ClientId"])
                        && !string.IsNullOrWhiteSpace(c["GitHub:ClientSecret"]),
                    demoEnabled = e.IsDevelopment() && c.GetValue<bool>("Demo:Enabled"),
                }
        );
        g.MapPost(
            "/register",
            async (
                RegisterRequest r,
                UserManager<User> users,
                AuthService auth,
                AppDb db,
                IConfiguration config
            ) =>
            {
                var email = Rules.Email(r.Email);
                var name = Rules.Text(r.Name, "Name");
                if (await users.FindByEmailAsync(email) != null)
                    return Results.Ok(
                        new
                        {
                            message = "If this address can be registered, a verification email is on its way.",
                        }
                    );
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    Email = email,
                    Name = name,
                };
                Rules.Identity(await users.CreateAsync(user, r.Password ?? ""));
                var token = await users.GenerateEmailConfirmationTokenAsync(user);
                auth.Mail(
                    email,
                    "Verify your Tenlith email",
                    $"Verify your email: {config["App:PublicUrl"] ?? "http://localhost:8080"}/verify-email?userId={user.Id}&token={Uri.EscapeDataString(token)}"
                );
                await db.SaveChangesAsync();
                return Results.Ok(
                    new
                    {
                        message = "If this address can be registered, a verification email is on its way.",
                    }
                );
            }
        );
        g.MapPost(
            "/verify-email",
            async (VerifyRequest r, UserManager<User> users) =>
            {
                var u =
                    await users.FindByIdAsync(r.UserId.ToString())
                    ?? throw new ApiException(400, "Verification link is invalid.");
                Rules.Identity(
                    await users.ConfirmEmailAsync(u, Rules.Text(r.Token, "Token", 4096))
                );
                return Results.Ok(new { message = "Email verified. You can now sign in." });
            }
        );
        g.MapPost(
            "/login",
            async (LoginRequest r, UserManager<User> users, AuthService auth, HttpContext ctx) =>
            {
                var email = Rules.Email(r.Email);
                var u = await users.FindByEmailAsync(email);
                if (u == null || await users.IsLockedOutAsync(u))
                    return Results.Problem(
                        detail: "Email or password is incorrect, or email has not been verified.",
                        statusCode: 401
                    );
                if (!await users.CheckPasswordAsync(u, r.Password ?? ""))
                {
                    await users.AccessFailedAsync(u);
                    return Results.Problem(
                        detail: "Email or password is incorrect, or email has not been verified.",
                        statusCode: 401
                    );
                }
                if (!u.EmailConfirmed)
                    return Results.Problem(
                        detail: "Email or password is incorrect, or email has not been verified.",
                        statusCode: 401
                    );
                await users.ResetAccessFailedCountAsync(u);
                return Results.Ok(await auth.Session(u, ctx));
            }
        );
        g.MapPost(
            "/refresh",
            async (HttpContext ctx, AppDb db, AuthService auth) =>
            {
                var raw = ctx.Request.Cookies["tenlith.refresh"];
                if (raw == null)
                    return Results.Unauthorized();
                var hash = Rules.Hash(raw);
                var token = await db
                    .RefreshSessions.Include(x => x.User)
                    .SingleOrDefaultAsync(x => x.TokenHash == hash);
                if (token == null)
                    return Results.Unauthorized();
                if (token.ConsumedAt != null || token.Revoked || token.ExpiresAt <= DateTime.UtcNow)
                {
                    await db
                        .RefreshSessions.Where(x => x.FamilyId == token.FamilyId)
                        .ExecuteUpdateAsync(x => x.SetProperty(y => y.Revoked, true));
                    return Results.Unauthorized();
                }
                token.ConsumedAt = DateTime.UtcNow;
                return Results.Ok(await auth.Session(token.User, ctx, token.FamilyId));
            }
        );
        g.MapPost(
            "/logout",
            async (HttpContext ctx, AppDb db) =>
            {
                var raw = ctx.Request.Cookies["tenlith.refresh"];
                if (raw != null)
                {
                    var hash = Rules.Hash(raw);
                    var token = await db.RefreshSessions.SingleOrDefaultAsync(x =>
                        x.TokenHash == hash
                    );
                    if (token != null)
                        await db
                            .RefreshSessions.Where(x => x.FamilyId == token.FamilyId)
                            .ExecuteUpdateAsync(x => x.SetProperty(y => y.Revoked, true));
                }
                ctx.Response.Cookies.Delete(
                    "tenlith.refresh",
                    new CookieOptions
                    {
                        Path = "/api/auth",
                        HttpOnly = true,
                        Secure = ctx.Request.IsHttps,
                        SameSite = SameSiteMode.Strict,
                    }
                );
                return Results.NoContent();
            }
        );
        g.MapGet(
                "/me",
                async (HttpContext ctx, UserManager<User> users, AuthService auth) =>
                    auth.UserDto((await users.FindByIdAsync(ctx.UserId().ToString()))!)
            )
            .RequireAuthorization();
        g.MapPost(
            "/forgot-password",
            async (
                EmailRequest r,
                UserManager<User> users,
                AuthService auth,
                IConfiguration config
            ) =>
            {
                var email = Rules.Email(r.Email);
                var user = await users.FindByEmailAsync(email);
                if (user?.EmailConfirmed == true)
                {
                    var token = await users.GeneratePasswordResetTokenAsync(user);
                    auth.Mail(
                        email,
                        "Reset your Tenlith password",
                        $"Reset your password: {config["App:PublicUrl"] ?? "http://localhost:8080"}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}"
                    );
                }
                return Results.Ok(
                    new { message = "If a verified account exists, a reset link is on its way." }
                );
            }
        );
        g.MapPost(
            "/reset-password",
            async (ResetRequest r, UserManager<User> users, AuthService auth) =>
            {
                var user =
                    await users.FindByEmailAsync(Rules.Email(r.Email))
                    ?? throw new ApiException(400, "Reset link is invalid.");
                Rules.Identity(
                    await users.ResetPasswordAsync(
                        user,
                        Rules.Text(r.Token, "Token", 4096),
                        Rules.Text(r.Password, "Password", 1024)
                    )
                );
                await auth.Revoke(user.Id);
                return Results.Ok(
                    new { message = "Password reset. Sign in with your new password." }
                );
            }
        );
        g.MapGet(
            "/github",
            (IConfiguration c) =>
                string.IsNullOrWhiteSpace(c["GitHub:ClientId"])
                    ? Results.Problem(detail: "GitHub sign-in is not configured.", statusCode: 503)
                    : Results.Challenge(
                        new AuthenticationProperties { RedirectUri = "/api/auth/github/complete" },
                        new[] { "GitHub" }
                    )
        );
        g.MapGet(
            "/github/complete",
            async (
                HttpContext ctx,
                UserManager<User> users,
                AuthService auth,
                IConfiguration config
            ) =>
            {
                var ticket = await ctx.AuthenticateAsync("github-session");
                if (!ticket.Succeeded)
                    return Results.Unauthorized();
                var id = ticket
                    .Principal!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                    ?.Value;
                var user = id == null ? null : await users.FindByIdAsync(id);
                if (user == null)
                    return Results.Unauthorized();
                await auth.Session(user, ctx);
                await ctx.SignOutAsync("github-session");
                return Results.Redirect(
                    (config["App:PublicUrl"] ?? "http://localhost:8080") + "/app"
                );
            }
        );
        g.MapPost(
            "/demo",
            async (
                IWebHostEnvironment env,
                IConfiguration config,
                UserManager<User> users,
                AuthService auth,
                WorkspaceService work,
                HttpContext ctx,
                AppDb db
            ) =>
            {
                if (!env.IsDevelopment() || !config.GetValue<bool>("Demo:Enabled"))
                    return Results.NotFound();
                var u = new User
                {
                    Id = Guid.NewGuid(),
                    Name = "Alex Morgan",
                    Email = $"demo-{Guid.NewGuid():N}@example.test",
                    EmailConfirmed = true,
                };
                u.UserName = u.Email;
                Rules.Identity(await users.CreateAsync(u));
                var org = await work.Create(u.Id, "Acme Studio");
                db.Projects.AddRange(
                    new Project
                    {
                        OrganizationId = org.Id,
                        Name = "Brand refresh",
                        Description = "A fresh identity for our next chapter.",
                        Color = "#c8f56b",
                    },
                    new Project
                    {
                        OrganizationId = org.Id,
                        Name = "Customer portal",
                        Description = "A simpler way to work together.",
                        Color = "#a7b5ff",
                    }
                );
                work.Audit(org.Id, "Workspace demo created", u.Name);
                await db.SaveChangesAsync();
                return Results.Ok(await auth.Session(u, ctx));
            }
        );
    }
}
