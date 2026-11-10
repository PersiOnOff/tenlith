using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Tenlith.Api;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    foreach (var proxy in config.GetSection("Proxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(proxy));
});
var development = builder.Environment.IsDevelopment();
if (
    !development
    && (config.GetValue<bool>("Demo:Enabled") || config.GetValue<bool>("Billing:Simulation"))
)
    throw new InvalidOperationException(
        "Demo and billing simulation are only available in Development."
    );
if (
    !development
    && (
        !Uri.TryCreate(config["App:PublicUrl"], UriKind.Absolute, out var publicOrigin)
        || publicOrigin.Scheme != "https"
    )
)
    throw new InvalidOperationException("App:PublicUrl must be an HTTPS URL in production.");
var key = config["Jwt:Key"];
if (string.IsNullOrWhiteSpace(key))
{
    if (!development)
        throw new InvalidOperationException("Jwt:Key is required.");
    key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
if (Encoding.UTF8.GetByteCount(key) < 32)
    throw new InvalidOperationException("Jwt:Key must contain at least 32 bytes.");
builder.Services.AddSingleton(new TokenSettings(key));
builder.Services.AddDbContext<AppDb>(o =>
    o.UseNpgsql(
        config.GetConnectionString("Database")
            ?? throw new InvalidOperationException("ConnectionStrings:Database is required.")
    )
);
builder
    .Services.AddIdentityCore<User>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 12;
        o.SignIn.RequireConfirmedEmail = true;
        o.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDb>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
var protection = builder.Services.AddDataProtection().SetApplicationName("Tenlith");
if (!string.IsNullOrWhiteSpace(config["DataProtection:KeysPath"]))
    protection.PersistKeysToFileSystem(new DirectoryInfo(config["DataProtection:KeysPath"]!));
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddHttpClient<StripeGateway>();
builder.Services.AddScoped<IMailTransport, SmtpTransport>();
builder.Services.AddScoped<JobRunner>();
builder.Services.AddHostedService<JobWorker>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy(
        "auth",
        ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 40,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            )
    );
});
var auth = builder
    .Services.AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "tenlith",
            ValidAudience = "tenlith-web",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromSeconds(15),
        };
        o.Events = new JwtBearerEvents
        {
            OnTokenValidated = async c =>
            {
                var users = c.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
                var user = await users.FindByIdAsync(
                    c.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!
                );
                if (user == null || user.SecurityStamp != c.Principal!.FindFirstValue("stamp"))
                    c.Fail("Session revoked.");
            },
        };
    })
    .AddCookie(
        "github-session",
        o =>
        {
            o.Cookie.Name = "tenlith.oauth";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        }
    );
if (
    !string.IsNullOrWhiteSpace(config["GitHub:ClientId"])
    && !string.IsNullOrWhiteSpace(config["GitHub:ClientSecret"])
)
    auth.AddOAuth(
        "GitHub",
        o =>
        {
            o.SignInScheme = "github-session";
            o.ClientId = config["GitHub:ClientId"]!;
            o.ClientSecret = config["GitHub:ClientSecret"]!;
            o.CallbackPath = "/api/auth/github/callback";
            o.AuthorizationEndpoint = "https://github.com/login/oauth/authorize";
            o.TokenEndpoint = "https://github.com/login/oauth/access_token";
            o.UserInformationEndpoint = "https://api.github.com/user";
            o.Scope.Add("user:email");
            o.SaveTokens = false;
            o.Events.OnCreatingTicket = async c =>
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, o.UserInformationEndpoint);
                req.Headers.Authorization = new("Bearer", c.AccessToken);
                req.Headers.UserAgent.ParseAdd("Tenlith/1.0");
                using var r = await c.Backchannel.SendAsync(req);
                r.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
                var ghId = doc.RootElement.GetProperty("id").ToString();
                var name =
                    doc.RootElement.TryGetProperty("name", out var n)
                    && n.ValueKind == JsonValueKind.String
                        ? n.GetString()
                        : doc.RootElement.GetProperty("login").GetString();
                using var er = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://api.github.com/user/emails"
                );
                er.Headers.Authorization = new("Bearer", c.AccessToken);
                er.Headers.UserAgent.ParseAdd("Tenlith/1.0");
                using var emails = await c.Backchannel.SendAsync(er);
                emails.EnsureSuccessStatusCode();
                using var ed = JsonDocument.Parse(await emails.Content.ReadAsStringAsync());
                var email = ed
                    .RootElement.EnumerateArray()
                    .FirstOrDefault(x =>
                        x.GetProperty("verified").GetBoolean()
                        && x.GetProperty("primary").GetBoolean()
                    );
                if (email.ValueKind == JsonValueKind.Undefined)
                    throw new InvalidOperationException(
                        "GitHub requires a verified primary email."
                    );
                var users = c.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
                var user = await users.FindByLoginAsync("GitHub", ghId);
                if (user == null)
                {
                    var address = email.GetProperty("email").GetString()!;
                    if (await users.FindByEmailAsync(address) != null)
                        throw new InvalidOperationException(
                            "Use email sign-in for the existing account."
                        );
                    user = new User
                    {
                        Id = Guid.NewGuid(),
                        UserName = address,
                        Email = address,
                        EmailConfirmed = true,
                        Name = name ?? "GitHub member",
                    };
                    var result = await users.CreateAsync(user);
                    if (!result.Succeeded)
                        throw new InvalidOperationException("Could not create account.");
                    await users.AddLoginAsync(user, new UserLoginInfo("GitHub", ghId, "GitHub"));
                }
                c.Identity!.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            };
            o.Events.OnRemoteFailure = c =>
            {
                c.HandleResponse();
                c.Response.Redirect(config["App:PublicUrl"] + "/login?error=github");
                return Task.CompletedTask;
            };
        }
    );
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseForwardedHeaders();
if (!development)
    app.UseExceptionHandler();
if (development)
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
app.Use(
    async (ctx, next) =>
    {
        try
        {
            await next();
        }
        catch (ApiException ex)
        {
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = ex.Status;
                await Results.Problem(detail: ex.Message, statusCode: ex.Status).ExecuteAsync(ctx);
            }
        }
        catch (Exception ex) when (Rules.IsWriteConflict(ex))
        {
            if (!ctx.Response.HasStarted)
                await Results
                    .Problem(
                        detail: "The update conflicted with another request. Please retry.",
                        statusCode: 409
                    )
                    .ExecuteAsync(ctx);
        }
        catch (DbUpdateException)
        {
            if (!ctx.Response.HasStarted)
                await Results
                    .Problem(
                        detail: "The update conflicted with another request. Please retry.",
                        statusCode: 409
                    )
                    .ExecuteAsync(ctx);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "40001" || ex.SqlState == "40P01")
        {
            if (!ctx.Response.HasStarted)
                await Results
                    .Problem(
                        detail: "The update conflicted with another request. Please retry.",
                        statusCode: 409
                    )
                    .ExecuteAsync(ctx);
        }
    }
);
app.Use(
    async (ctx, next) =>
    {
        if (
            ctx.Request.Path.StartsWithSegments("/api")
            && ctx.Request.Method is not ("GET" or "HEAD" or "OPTIONS")
            && ctx.Request.Path != "/api/billing/webhook"
        )
        {
            var publicUrl = config["App:PublicUrl"] ?? "http://localhost:8080";
            var origin = ctx.Request.Headers.Origin.ToString();
            if (
                ctx.Request.Headers["X-Tenlith-Client"] != "web"
                || (
                    !string.IsNullOrEmpty(origin)
                    && !string.Equals(
                        origin,
                        new Uri(publicUrl).GetLeftPart(UriPartial.Authority),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                await Results
                    .Problem(detail: "Request origin or client header is invalid.", statusCode: 403)
                    .ExecuteAsync(ctx);
                return;
            }
        }
        await next();
    }
);
app.UseRateLimiter();

// OAuth middleware uses the request origin for both authorization and token exchange.
// Pin that origin to deployment configuration rather than trusting proxy headers or Host.
app.Use(
    async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api/auth/github"))
        {
            var origin = new Uri(config["App:PublicUrl"] ?? "http://localhost:8080");
            context.Request.Scheme = origin.Scheme;
            context.Request.Host = origin.IsDefaultPort
                ? new HostString(origin.Host)
                : new HostString(origin.Host, origin.Port);
        }
        await next();
    }
);
app.UseAuthentication();
app.UseAuthorization();
app.MapGet(
    "/health",
    async (AppDb db) =>
        await db.Database.CanConnectAsync()
            ? Results.Ok(new { status = "healthy" })
            : Results.StatusCode(503)
);
app.MapAuth();
app.MapWorkspaces();
app.MapBilling();
if (development || args.Contains("--migrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    if (db.Database.IsNpgsql())
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
}
if (args.Contains("--migrate"))
    return;
app.Run();

public partial class Program { }
