using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Tenlith.Api;

public sealed record MailMessageData(string To, string Subject, string Body);

public interface IMailTransport
{
    Task Send(MailMessageData data, Guid id, CancellationToken ct);
}

public sealed class SmtpTransport(IConfiguration config, IWebHostEnvironment env) : IMailTransport
{
    public async Task Send(MailMessageData data, Guid id, CancellationToken ct)
    {
        var host =
            config["Mail:Host"] ?? throw new InvalidOperationException("Mail:Host is required.");
        var from =
            config["Mail:From"]
            ?? (
                env.IsDevelopment()
                    ? "hello@tenlith.local"
                    : throw new InvalidOperationException("Mail:From is required.")
            );
        using var smtp = new SmtpClient(
            host,
            config.GetValue<int?>("Mail:Port") ?? (env.IsDevelopment() ? 1025 : 587)
        )
        {
            EnableSsl = config.GetValue<bool?>("Mail:UseTls") ?? !env.IsDevelopment(),
            Timeout = 30000,
        };
        if (!string.IsNullOrWhiteSpace(config["Mail:Username"]))
            smtp.Credentials = new NetworkCredential(
                config["Mail:Username"],
                config["Mail:Password"]
            );
        using var message = new MailMessage(from, data.To, data.Subject, data.Body);
        message.Headers.Add("Message-ID", $"<{id:N}@tenlith.mail>");
        await smtp.SendMailAsync(message, ct);
    }
}

public sealed class JobRunner(
    AppDb db,
    IMailTransport mail,
    StripeGateway stripe,
    ILogger<JobRunner> logger
)
{
    public async Task<bool> RunOne(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var id = await db
            .Jobs.AsNoTracking()
            .Where(x =>
                x.CompletedAt == null
                && x.Attempts < 8
                && x.AvailableAt <= now
                && (x.LockedUntil == null || x.LockedUntil <= now)
            )
            .OrderBy(x => x.AvailableAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (id == null)
            return false;
        var lease = now.AddMinutes(2);
        var claimed = await db
            .Jobs.Where(x =>
                x.Id == id
                && x.CompletedAt == null
                && x.Attempts < 8
                && x.AvailableAt <= now
                && (x.LockedUntil == null || x.LockedUntil <= now)
            )
            .ExecuteUpdateAsync(
                x =>
                    x.SetProperty(j => j.LockedUntil, lease)
                        .SetProperty(j => j.Attempts, j => j.Attempts + 1),
                ct
            );
        if (claimed == 0)
            return true;
        var job = await db.Jobs.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        try
        {
            if (job.Kind == "mail")
                await mail.Send(
                    JsonSerializer.Deserialize<MailMessageData>(job.Payload)!,
                    job.Id,
                    ct
                );
            else if (job.Kind == "stripe")
                await stripe.Reconcile(db, job.Payload, ct);
            else
                throw new InvalidOperationException("Unknown job kind.");
            await db
                .Jobs.Where(x => x.Id == id && x.LockedUntil == lease)
                .ExecuteUpdateAsync(
                    x =>
                        x.SetProperty(j => j.CompletedAt, DateTime.UtcNow)
                            .SetProperty(j => j.LockedUntil, (DateTime?)null)
                            .SetProperty(j => j.LastError, (string?)null),
                    ct
                );
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                "Job {JobId} ({Kind}) failed on attempt {Attempt}; error type {ErrorType}.",
                id,
                job.Kind,
                job.Attempts,
                e.GetType().Name
            );
            db.ChangeTracker.Clear();
            await db
                .Jobs.Where(x => x.Id == id && x.LockedUntil == lease)
                .ExecuteUpdateAsync(
                    x =>
                        x.SetProperty(j => j.LockedUntil, (DateTime?)null)
                            .SetProperty(j => j.LastError, e.GetType().Name)
                            .SetProperty(
                                j => j.AvailableAt,
                                DateTime.UtcNow.AddSeconds(
                                    Math.Min(3600, Math.Pow(2, job.Attempts) * 10)
                                )
                            ),
                    ct
                );
        }
        return true;
    }

    public async Task ScheduleReconciliation(CancellationToken ct)
    {
        var bucket = DateTime.UtcNow.ToString("yyyyMMddHH");
        var customers = await db
            .Organizations.AsNoTracking()
            .Where(x => x.StripeCustomerId != null)
            .Select(x => x.StripeCustomerId!)
            .ToListAsync(ct);
        foreach (var customer in customers)
        {
            var key = "reconcile:" + customer + ":" + bucket;
            if (!await db.Jobs.AnyAsync(x => x.DeduplicationKey == key, ct))
                db.Jobs.Add(
                    new DurableJob
                    {
                        Kind = "stripe",
                        Payload = customer,
                        DeduplicationKey = key,
                    }
                );
        }
        await db.SaveChangesAsync(ct);
    }
}

public sealed class JobWorker(IServiceProvider services, ILogger<JobWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        var next = DateTime.MinValue;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<JobRunner>();
                if (DateTime.UtcNow >= next)
                {
                    await runner.ScheduleReconciliation(ct);
                    next = DateTime.UtcNow.AddMinutes(10);
                }
                if (await runner.RunOne(ct))
                    continue;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError("Background job polling failed: {ErrorType}.", e.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(2), ct);
        }
    }
}
