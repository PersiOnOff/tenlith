using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Tenlith.Api;

public sealed class User : IdentityUser<Guid>
{
    public string Name { get; set; } = "";
}

public sealed class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Plan { get; set; } = "Free";
    public string BillingStatus { get; set; } = "inactive";
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public string? PendingCheckoutId { get; set; }
    public string? PendingCheckoutUrl { get; set; }
    public DateTime? PendingCheckoutExpiresAt { get; set; }
    public int CheckoutGeneration { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Membership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = "Member";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Color { get; set; } = "#c8f56b";
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class Invitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Member";
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
}

public sealed class RefreshSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public bool Revoked { get; set; }
}

public sealed class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Action { get; set; } = "";
    public string ActorName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class DurableJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "mail";
    public string Payload { get; set; } = "";
    public string DeduplicationKey { get; set; } = Guid.NewGuid().ToString();
    public int Attempts { get; set; }
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? LastError { get; set; }
}

public sealed class AppDb(DbContextOptions<AppDb> options)
    : IdentityDbContext<User, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<DurableJob> Jobs => Set<DurableJob>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Membership>().HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
        b.Entity<Organization>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<Organization>().HasIndex(x => x.StripeCustomerId).IsUnique();
        b.Entity<RefreshSession>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<Invitation>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<DurableJob>().HasIndex(x => x.DeduplicationKey).IsUnique();
        b.Entity<DurableJob>().HasIndex(x => new { x.CompletedAt, x.AvailableAt });
    }
}
