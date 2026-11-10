using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Tenlith.Api;

public sealed class StripeGateway(HttpClient http, IConfiguration config)
{
    public bool Configured =>
        !string.IsNullOrWhiteSpace(config["Stripe:SecretKey"])
        && !string.IsNullOrWhiteSpace(config["Stripe:PriceId"])
        && !string.IsNullOrWhiteSpace(config["Stripe:WebhookSecret"]);

    public async Task<JsonElement> Request(
        HttpMethod method,
        string path,
        Dictionary<string, string>? data = null,
        string? idempotency = null,
        CancellationToken ct = default
    )
    {
        if (!Configured)
            throw new ApiException(503, "Stripe billing is not configured.");
        using var request = new HttpRequestMessage(method, "https://api.stripe.com/v1/" + path);
        request.Headers.Authorization = new("Bearer", config["Stripe:SecretKey"]);
        if (idempotency != null)
            request.Headers.Add("Idempotency-Key", idempotency);
        if (data != null)
            request.Content = new FormUrlEncodedContent(data);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new ApiException(502, "The billing provider could not complete the request.");
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return doc.RootElement.Clone();
    }

    public async Task Reconcile(AppDb db, string customer, CancellationToken ct)
    {
        await using var transaction =
            db.Database.CurrentTransaction == null
                ? await db.Database.BeginTransactionAsync(ct)
                : null;
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({customer}, 0))",
                ct
            );
        var org = await db.Organizations.SingleOrDefaultAsync(
            x => x.StripeCustomerId == customer,
            ct
        );
        if (org == null)
        {
            if (transaction != null)
                await transaction.CommitAsync(ct);
            return;
        }
        await db.Entry(org).ReloadAsync(ct);
        var paid = false;
        string? subscription = null;
        string billingStatus = "inactive";
        string? cursor = null;
        do
        {
            var page = await Request(
                HttpMethod.Get,
                $"subscriptions?customer={Uri.EscapeDataString(customer)}&status=all&limit=100"
                    + (cursor == null ? "" : "&starting_after=" + Uri.EscapeDataString(cursor)),
                ct: ct
            );
            var data = page.GetProperty("data");
            foreach (var sub in data.EnumerateArray())
            {
                var status = sub.GetProperty("status").GetString();
                if (
                    status is not ("canceled" or "incomplete_expired")
                    && sub.GetProperty("items")
                        .GetProperty("data")
                        .EnumerateArray()
                        .Any(x =>
                            x.GetProperty("price").GetProperty("id").GetString()
                            == config["Stripe:PriceId"]
                        )
                )
                {
                    var entitled = status is "active" or "trialing";
                    if (!paid || entitled)
                    {
                        subscription = sub.GetProperty("id").GetString();
                        billingStatus = status ?? "inactive";
                    }
                    paid |= entitled;
                }
            }
            cursor =
                page.GetProperty("has_more").GetBoolean() && data.GetArrayLength() > 0
                    ? data[data.GetArrayLength() - 1].GetProperty("id").GetString()
                    : null;
        } while (cursor != null);
        var plan = paid ? "Pro" : "Free";
        if (org.Plan != plan || org.StripeSubscriptionId != subscription)
        {
            db.AuditEvents.Add(
                new AuditEvent
                {
                    OrganizationId = org.Id,
                    Action = $"Billing reconciled: {plan}",
                    ActorName = "Stripe",
                }
            );
        }
        org.Plan = plan;
        org.BillingStatus = billingStatus;
        org.StripeSubscriptionId = subscription;
        await db.SaveChangesAsync(ct);
        if (transaction != null)
            await transaction.CommitAsync(ct);
    }

    public static bool Verify(string raw, string header, string? secret, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return false;
        var parts = header
            .Split(',')
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .ToList();
        var stamp = parts.FirstOrDefault(x => x[0] == "t")?[1];
        if (
            !long.TryParse(stamp, out var seconds)
            || Math.Abs(now.ToUnixTimeSeconds() - seconds) > 300
        )
            return false;
        var expected = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(stamp + "." + raw)
        );
        foreach (var p in parts.Where(x => x[0] == "v1"))
        {
            try
            {
                if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(p[1])))
                    return true;
            }
            catch (FormatException) { }
        }
        return false;
    }
}

public record PlanRequest(string? Plan);

public static class BillingEndpoints
{
    public static void MapBilling(this WebApplication app)
    {
        var g = app.MapGroup("/api/organizations/{id:guid}/billing")
            .RequireAuthorization()
            .AddEndpointFilter<WriteTransactionFilter>();
        g.MapGet(
            "",
            async (
                Guid id,
                HttpContext c,
                WorkspaceService w,
                StripeGateway stripe,
                IWebHostEnvironment env,
                IConfiguration config
            ) =>
            {
                var m = await w.Membership(id, c.UserId());
                return Payload(m, w, stripe, env, config);
            }
        );
        g.MapPost(
            "/simulate",
            async (
                Guid id,
                PlanRequest r,
                HttpContext c,
                WorkspaceService w,
                StripeGateway stripe,
                IWebHostEnvironment env,
                IConfiguration config
            ) =>
            {
                if (!env.IsDevelopment() || !config.GetValue<bool>("Billing:Simulation"))
                    return Results.NotFound();
                var m = await w.Membership(id, c.UserId(), "Owner");
                if (r.Plan is not ("Free" or "Pro"))
                    throw new ApiException(400, "Plan must be Free or Pro.");
                if (m.Organization.StripeCustomerId != null)
                    throw new ApiException(
                        409,
                        "Simulation is unavailable for a live billing customer."
                    );
                m.Organization.Plan = r.Plan;
                m.Organization.BillingStatus = "simulated";
                w.Audit(id, $"Demo billing changed to {r.Plan}", m.User.Name);
                return Results.Ok(Payload(m, w, stripe, env, config));
            }
        );
        g.MapPost(
            "/checkout",
            async (
                Guid id,
                HttpContext c,
                WorkspaceService w,
                StripeGateway stripe,
                IConfiguration config,
                AppDb db
            ) =>
            {
                var m = await w.Membership(id, c.UserId(), "Owner");
                if (!stripe.Configured)
                    throw new ApiException(503, "Stripe billing is not configured.");
                var org = m.Organization;
                if (org.StripeCustomerId != null)
                    await stripe.Reconcile(db, org.StripeCustomerId, default);
                if (org.StripeSubscriptionId != null)
                    throw new ApiException(
                        409,
                        "A subscription already exists. Use the billing portal."
                    );
                if (org.StripeCustomerId == null)
                {
                    var customer = await stripe.Request(
                        HttpMethod.Post,
                        "customers",
                        new()
                        {
                            { "name", org.Name },
                            { "email", m.User.Email! },
                            { "metadata[organizationId]", id.ToString() },
                        },
                        "tenlith-customer-" + id
                    );
                    org.StripeCustomerId = customer.GetProperty("id").GetString();
                    await db.SaveChangesAsync();
                }
                if (
                    org.PendingCheckoutId != null
                    && org.PendingCheckoutUrl != null
                    && org.PendingCheckoutExpiresAt > DateTime.UtcNow
                )
                {
                    var pending = await stripe.Request(
                        HttpMethod.Get,
                        "checkout/sessions/" + Uri.EscapeDataString(org.PendingCheckoutId)
                    );
                    if (pending.GetProperty("status").GetString() == "open")
                        return new { url = org.PendingCheckoutUrl };
                }
                if (org.PendingCheckoutId != null)
                    org.CheckoutGeneration++;
                var session = await stripe.Request(
                    HttpMethod.Post,
                    "checkout/sessions",
                    new()
                    {
                        { "mode", "subscription" },
                        { "customer", org.StripeCustomerId! },
                        { "line_items[0][price]", config["Stripe:PriceId"]! },
                        { "line_items[0][quantity]", "1" },
                        { "client_reference_id", id.ToString() },
                        { "subscription_data[metadata][organizationId]", id.ToString() },
                        {
                            "success_url",
                            config["App:PublicUrl"] + "/app/billing?checkout=complete"
                        },
                        {
                            "cancel_url",
                            config["App:PublicUrl"] + "/app/billing?checkout=cancelled"
                        },
                    },
                    "tenlith-checkout-" + id + "-" + org.CheckoutGeneration
                );
                org.PendingCheckoutId = session.GetProperty("id").GetString();
                org.PendingCheckoutUrl = session.GetProperty("url").GetString();
                org.PendingCheckoutExpiresAt = DateTimeOffset
                    .FromUnixTimeSeconds(session.GetProperty("expires_at").GetInt64())
                    .UtcDateTime;
                w.Audit(id, "Checkout requested", m.User.Name);
                return new { url = org.PendingCheckoutUrl };
            }
        );
        g.MapPost(
            "/portal",
            async (
                Guid id,
                HttpContext c,
                WorkspaceService w,
                StripeGateway stripe,
                IConfiguration config
            ) =>
            {
                var m = await w.Membership(id, c.UserId(), "Owner");
                if (!stripe.Configured)
                    throw new ApiException(503, "Stripe billing is not configured.");
                if (m.Organization.StripeCustomerId == null)
                    throw new ApiException(409, "No billing customer exists yet.");
                var session = await stripe.Request(
                    HttpMethod.Post,
                    "billing_portal/sessions",
                    new()
                    {
                        { "customer", m.Organization.StripeCustomerId },
                        { "return_url", config["App:PublicUrl"] + "/app/billing" },
                    }
                );
                return new { url = session.GetProperty("url").GetString() };
            }
        );
        app.MapPost(
            "/api/billing/webhook",
            async (HttpContext c, IConfiguration config, AppDb db) =>
            {
                if (c.Request.ContentLength > 1024 * 1024)
                    return Results.BadRequest();
                using var reader = new StreamReader(c.Request.Body);
                var body = await reader.ReadToEndAsync();
                if (
                    body.Length > 1024 * 1024
                    || !StripeGateway.Verify(
                        body,
                        c.Request.Headers["Stripe-Signature"].ToString(),
                        config["Stripe:WebhookSecret"],
                        DateTimeOffset.UtcNow
                    )
                )
                    return Results.BadRequest();
                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(body);
                }
                catch (JsonException)
                {
                    return Results.BadRequest();
                }
                using (doc)
                {
                    var root = doc.RootElement;
                    if (
                        !root.TryGetProperty("id", out var id)
                        || !root.TryGetProperty("type", out var type)
                    )
                        return Results.BadRequest();
                    var eventId = id.GetString();
                    if (string.IsNullOrWhiteSpace(eventId))
                        return Results.BadRequest();
                    var kind = type.GetString() ?? "";
                    if (
                        kind.StartsWith("customer.subscription.")
                        || kind == "checkout.session.completed"
                        || kind == "invoice.paid"
                        || kind == "invoice.payment_failed"
                    )
                    {
                        if (
                            !root.TryGetProperty("data", out var data)
                            || !data.TryGetProperty("object", out var item)
                            || !item.TryGetProperty("customer", out var customer)
                            || customer.ValueKind != JsonValueKind.String
                        )
                            return Results.BadRequest();
                        var dedup = "stripe:" + eventId;
                        if (!await db.Jobs.AnyAsync(x => x.DeduplicationKey == dedup))
                        {
                            db.Jobs.Add(
                                new DurableJob
                                {
                                    Kind = "stripe",
                                    Payload = customer.GetString()!,
                                    DeduplicationKey = dedup,
                                }
                            );
                            try
                            {
                                await db.SaveChangesAsync();
                            }
                            catch (DbUpdateException)
                            {
                                if (
                                    !await db
                                        .Jobs.AsNoTracking()
                                        .AnyAsync(x => x.DeduplicationKey == dedup)
                                )
                                    throw;
                            }
                        }
                    }
                    return Results.Ok();
                }
            }
        );
    }

    static object Payload(
        Membership m,
        WorkspaceService w,
        StripeGateway stripe,
        IWebHostEnvironment env,
        IConfiguration config
    )
    {
        var simulation =
            env.IsDevelopment()
            && config.GetValue<bool>("Billing:Simulation")
            && m.Organization.StripeCustomerId == null;
        return new
        {
            plan = m.Organization.Plan,
            status = m.Organization.BillingStatus,
            provider = simulation ? "simulation"
            : stripe.Configured ? "stripe"
            : "unconfigured",
            projectLimit = w.ProjectLimit(m.Organization),
            memberLimit = w.MemberLimit(m.Organization),
            canManage = m.Role == "Owner",
            configured = simulation || stripe.Configured,
        };
    }
}
