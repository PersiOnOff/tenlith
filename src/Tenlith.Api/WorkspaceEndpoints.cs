using Microsoft.EntityFrameworkCore;

namespace Tenlith.Api;

public sealed class WorkspaceService(AppDb db, IConfiguration config)
{
    public async Task<Membership> Membership(Guid org, Guid user, string? minimum = null)
    {
        var m =
            await db
                .Memberships.Include(x => x.Organization)
                .Include(x => x.User)
                .SingleOrDefaultAsync(x => x.OrganizationId == org && x.UserId == user)
            ?? throw new ApiException(403, "You do not have access to this organization.");
        if (minimum == "Owner" && m.Role != "Owner" || minimum == "Admin" && m.Role == "Member")
            throw new ApiException(403, "Your role does not allow this action.");
        return m;
    }

    public int ProjectLimit(Organization org) =>
        config.GetValue<int?>(
            org.Plan == "Pro" ? "Billing:ProProjectLimit" : "Billing:FreeProjectLimit"
        ) ?? (org.Plan == "Pro" ? 50 : 3);

    public int MemberLimit(Organization org) =>
        config.GetValue<int?>(
            org.Plan == "Pro" ? "Billing:ProMemberLimit" : "Billing:FreeMemberLimit"
        ) ?? (org.Plan == "Pro" ? 25 : 3);

    public object OrganizationDto(Organization org, string role) =>
        new
        {
            id = org.Id,
            name = org.Name,
            slug = org.Slug,
            role,
            plan = org.Plan,
            createdAt = org.CreatedAt,
        };

    public static object ProjectDto(Project p) =>
        new
        {
            id = p.Id,
            name = p.Name,
            description = p.Description,
            color = p.Color,
            status = p.Status,
            createdAt = p.CreatedAt,
        };

    public async Task<Organization> Create(Guid user, string name)
    {
        name = Rules.Text(name, "Organization name");
        var org = new Organization
        {
            Name = name,
            Slug =
                System
                    .Text.RegularExpressions.Regex.Replace(
                        name.ToLowerInvariant(),
                        "[^a-z0-9]+",
                        "-"
                    )
                    .Trim('-')
                + "-"
                + Guid.NewGuid().ToString("N")[..8],
        };
        db.Organizations.Add(org);
        db.Memberships.Add(
            new Membership
            {
                OrganizationId = org.Id,
                UserId = user,
                Role = "Owner",
            }
        );
        await db.SaveChangesAsync();
        return org;
    }

    public void Audit(Guid org, string action, string actor) =>
        db.AuditEvents.Add(
            new AuditEvent
            {
                OrganizationId = org,
                Action = action,
                ActorName = actor,
            }
        );

    public static string Role(string? role)
    {
        if (role is not ("Admin" or "Member"))
            throw new ApiException(400, "Role must be Admin or Member.");
        return role;
    }

    public static void CanManage(Membership actor, Membership target)
    {
        if (
            target.Role == "Owner"
            || actor.Role == "Member"
            || actor.Role == "Admin" && target.Role != "Member"
        )
            throw new ApiException(403, "This role cannot be changed or removed.");
    }
}

public record NameRequest(string? Name);

public record ProjectRequest(string? Name, string? Description, string? Color, string? Status);

public record RoleRequest(string? Role);

public record MemberRequest(Guid MemberId);

public record InviteRequest(string? Email, string? Role);

public record AcceptRequest(string Token);

public static class WorkspaceEndpoints
{
    public static void MapWorkspaces(this WebApplication app)
    {
        var g = app.MapGroup("/api/organizations")
            .RequireAuthorization()
            .AddEndpointFilter<WriteTransactionFilter>();
        g.MapGet(
            "",
            async (HttpContext c, AppDb db, WorkspaceService w) =>
            {
                var ms = await db
                    .Memberships.Include(x => x.Organization)
                    .Where(x => x.UserId == c.UserId())
                    .OrderBy(x => x.JoinedAt)
                    .ToListAsync();
                return ms.Select(x => w.OrganizationDto(x.Organization, x.Role));
            }
        );
        g.MapPost(
            "",
            async (NameRequest r, HttpContext c, WorkspaceService w) =>
            {
                var org = await w.Create(c.UserId(), r.Name ?? "");
                return Results.Created(
                    $"/api/organizations/{org.Id}",
                    w.OrganizationDto(org, "Owner")
                );
            }
        );
        g.MapPatch(
            "/{id:guid}",
            async (Guid id, NameRequest r, HttpContext c, WorkspaceService w) =>
            {
                var m = await w.Membership(id, c.UserId(), "Owner");
                m.Organization.Name = Rules.Text(r.Name, "Organization name");
                w.Audit(id, "Organization renamed", m.User.Name);
                return w.OrganizationDto(m.Organization, m.Role);
            }
        );
        g.MapDelete(
            "/{id:guid}",
            async (Guid id, HttpContext c, WorkspaceService w, AppDb db, StripeGateway stripe) =>
            {
                var m = await w.Membership(id, c.UserId(), "Owner");
                if (m.Organization.StripeCustomerId != null)
                {
                    await stripe.Reconcile(db, m.Organization.StripeCustomerId, default);
                    if (
                        m.Organization.StripeSubscriptionId != null
                        || m.Organization.PendingCheckoutExpiresAt > DateTime.UtcNow
                    )
                        throw new ApiException(
                            409,
                            "Cancel your subscription in the billing portal and wait for pending checkout sessions to expire before deleting this organization."
                        );
                }
                db.Organizations.Remove(m.Organization);
                return Results.NoContent();
            }
        );
        g.MapGet(
            "/{id:guid}/overview",
            async (Guid id, HttpContext c, WorkspaceService w, AppDb db) =>
            {
                var m = await w.Membership(id, c.UserId());
                return new
                {
                    projectCount = await db.Projects.CountAsync(x => x.OrganizationId == id),
                    memberCount = await db.Memberships.CountAsync(x => x.OrganizationId == id),
                    plan = m.Organization.Plan,
                    projectLimit = w.ProjectLimit(m.Organization),
                    memberLimit = w.MemberLimit(m.Organization),
                    activity = await db
                        .AuditEvents.Where(x => x.OrganizationId == id)
                        .OrderByDescending(x => x.CreatedAt)
                        .Take(12)
                        .Select(x => new
                        {
                            x.Id,
                            x.Action,
                            x.ActorName,
                            x.CreatedAt,
                        })
                        .ToListAsync(),
                };
            }
        );
        g.MapGet(
            "/{id:guid}/projects",
            async (Guid id, HttpContext c, WorkspaceService w, AppDb db) =>
            {
                await w.Membership(id, c.UserId());
                return (
                    await db
                        .Projects.Where(x => x.OrganizationId == id)
                        .OrderBy(x => x.CreatedAt)
                        .ToListAsync()
                ).Select(WorkspaceService.ProjectDto);
            }
        );
        g.MapPost(
            "/{id:guid}/projects",
            async (Guid id, ProjectRequest r, HttpContext c, WorkspaceService w, AppDb db) =>
            {
                var m = await w.Membership(id, c.UserId(), "Admin");
                if (
                    await db.Projects.CountAsync(x => x.OrganizationId == id)
                    >= w.ProjectLimit(m.Organization)
                )
                    throw new ApiException(
                        409,
                        "Project limit reached. Upgrade your plan to add more projects."
                    );
                var p = new Project { OrganizationId = id };
                Apply(p, r);
                db.Projects.Add(p);
                w.Audit(id, $"Project created: {p.Name}", m.User.Name);
                return Results.Created(
                    $"/api/organizations/{id}/projects/{p.Id}",
                    WorkspaceService.ProjectDto(p)
                );
            }
        );
        g.MapPatch(
            "/{id:guid}/projects/{projectId:guid}",
            async (
                Guid id,
                Guid projectId,
                ProjectRequest r,
                HttpContext c,
                WorkspaceService w,
                AppDb db
            ) =>
            {
                var m = await w.Membership(id, c.UserId(), "Admin");
                var p =
                    await db.Projects.SingleOrDefaultAsync(x =>
                        x.Id == projectId && x.OrganizationId == id
                    ) ?? throw new ApiException(404, "Project not found.");
                Apply(p, r);
                w.Audit(id, $"Project updated: {p.Name}", m.User.Name);
                return WorkspaceService.ProjectDto(p);
            }
        );
        g.MapDelete(
            "/{id:guid}/projects/{projectId:guid}",
            async (Guid id, Guid projectId, HttpContext c, WorkspaceService w, AppDb db) =>
            {
                var m = await w.Membership(id, c.UserId(), "Admin");
                var p =
                    await db.Projects.SingleOrDefaultAsync(x =>
                        x.Id == projectId && x.OrganizationId == id
                    ) ?? throw new ApiException(404, "Project not found.");
                db.Projects.Remove(p);
                w.Audit(id, $"Project deleted: {p.Name}", m.User.Name);
                return Results.NoContent();
            }
        );
        g.MapGet(
            "/{id:guid}/members",
            async (Guid id, HttpContext c, WorkspaceService w, AppDb db) =>
            {
                await w.Membership(id, c.UserId());
                return await db
                    .Memberships.Where(x => x.OrganizationId == id)
                    .OrderBy(x => x.JoinedAt)
                    .Select(x => new
                    {
                        x.Id,
                        x.UserId,
                        x.User.Name,
                        x.User.Email,
                        x.Role,
                        x.JoinedAt,
                    })
                    .ToListAsync();
            }
        );
        g.MapPatch(
            "/{id:guid}/members/{memberId:guid}",
            async (
                Guid id,
                Guid memberId,
                RoleRequest r,
                HttpContext c,
                WorkspaceService w,
                AppDb db
            ) =>
            {
                var actor = await w.Membership(id, c.UserId(), "Admin");
                var target =
                    await db.Memberships.SingleOrDefaultAsync(x =>
                        x.OrganizationId == id && x.Id == memberId
                    ) ?? throw new ApiException(404, "Member not found.");
                WorkspaceService.CanManage(actor, target);
                var role = WorkspaceService.Role(r.Role);
                if (actor.Role == "Admin" && role != "Member")
                    throw new ApiException(403, "Only the owner can appoint admins.");
                target.Role = role;
                w.Audit(id, $"Member role changed to {role}", actor.User.Name);
                return Results.NoContent();
            }
        );
        g.MapDelete(
            "/{id:guid}/members/{memberId:guid}",
            async (Guid id, Guid memberId, HttpContext c, WorkspaceService w, AppDb db) =>
            {
                var actor = await w.Membership(id, c.UserId(), "Admin");
                var target =
                    await db.Memberships.SingleOrDefaultAsync(x =>
                        x.OrganizationId == id && x.Id == memberId
                    ) ?? throw new ApiException(404, "Member not found.");
                WorkspaceService.CanManage(actor, target);
                db.Memberships.Remove(target);
                w.Audit(id, "Member removed", actor.User.Name);
                return Results.NoContent();
            }
        );
        g.MapPost(
            "/{id:guid}/transfer-ownership",
            async (Guid id, MemberRequest r, HttpContext c, WorkspaceService w, AppDb db) =>
            {
                var actor = await w.Membership(id, c.UserId(), "Owner");
                var target =
                    await db.Memberships.SingleOrDefaultAsync(x =>
                        x.OrganizationId == id && x.Id == r.MemberId
                    ) ?? throw new ApiException(404, "Member not found.");
                if (target.Id == actor.Id)
                    throw new ApiException(400, "Choose another member.");
                target.Role = "Owner";
                actor.Role = "Admin";
                w.Audit(id, "Ownership transferred", actor.User.Name);
                return Results.NoContent();
            }
        );
        g.MapGet(
            "/{id:guid}/invitations",
            async (Guid id, HttpContext c, WorkspaceService w, AppDb db) =>
            {
                await w.Membership(id, c.UserId(), "Admin");
                return await db
                    .Invitations.Where(x =>
                        x.OrganizationId == id && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow
                    )
                    .Select(x => new
                    {
                        x.Id,
                        x.Email,
                        x.Role,
                        x.ExpiresAt,
                    })
                    .ToListAsync();
            }
        );
        g.MapPost(
            "/{id:guid}/invitations",
            async (
                Guid id,
                InviteRequest r,
                HttpContext c,
                WorkspaceService w,
                AppDb db,
                AuthService auth,
                IConfiguration config
            ) =>
            {
                var m = await w.Membership(id, c.UserId(), "Admin");
                var email = Rules.Email(r.Email);
                var role = WorkspaceService.Role(r.Role);
                if (m.Role == "Admin" && role != "Member")
                    throw new ApiException(403, "Only the owner can invite admins.");
                if (
                    await db.Memberships.AnyAsync(x =>
                        x.OrganizationId == id && x.User.NormalizedEmail == email.ToUpperInvariant()
                    )
                )
                    throw new ApiException(409, "This person is already a member.");
                if (
                    await db.Invitations.AnyAsync(x =>
                        x.OrganizationId == id
                        && x.Email == email
                        && x.UsedAt == null
                        && x.ExpiresAt > DateTime.UtcNow
                    )
                )
                    throw new ApiException(409, "An invitation is already pending.");
                var seats =
                    await db.Memberships.CountAsync(x => x.OrganizationId == id)
                    + await db.Invitations.CountAsync(x =>
                        x.OrganizationId == id && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow
                    );
                if (seats >= w.MemberLimit(m.Organization))
                    throw new ApiException(
                        409,
                        "Member limit reached. Pending invitations reserve seats."
                    );
                var token = Rules.Secret();
                db.Invitations.Add(
                    new Invitation
                    {
                        OrganizationId = id,
                        Email = email,
                        Role = role,
                        TokenHash = Rules.Hash(token),
                        ExpiresAt = DateTime.UtcNow.AddDays(7),
                    }
                );
                auth.Mail(
                    email,
                    $"Join {m.Organization.Name} on Tenlith",
                    $"Accept invitation: {config["App:PublicUrl"] ?? "http://localhost:8080"}/invite?token={token}"
                );
                w.Audit(id, $"Invitation sent to {email}", m.User.Name);
                return Results.Ok(new { message = "Invitation sent." });
            }
        );
        g.MapDelete(
            "/{id:guid}/invitations/{invitationId:guid}",
            async (Guid id, Guid invitationId, HttpContext c, WorkspaceService w, AppDb db) =>
            {
                var m = await w.Membership(id, c.UserId(), "Admin");
                var invite =
                    await db.Invitations.SingleOrDefaultAsync(x =>
                        x.Id == invitationId && x.OrganizationId == id
                    ) ?? throw new ApiException(404, "Invitation not found.");
                if (m.Role == "Admin" && invite.Role != "Member")
                    throw new ApiException(403, "Only the owner can revoke admin invitations.");
                db.Invitations.Remove(invite);
                w.Audit(id, "Invitation revoked", m.User.Name);
                return Results.NoContent();
            }
        );
        app.MapPost(
                "/api/invitations/accept",
                async (AcceptRequest r, HttpContext c, WorkspaceService w, AppDb db) =>
                {
                    var hash = Rules.Hash(r.Token);
                    var invite =
                        await db
                            .Invitations.Include(x => x.Organization)
                            .SingleOrDefaultAsync(x => x.TokenHash == hash)
                        ?? throw new ApiException(400, "Invitation is invalid.");
                    var user = await db.Users.SingleAsync(x => x.Id == c.UserId());
                    if (
                        !user.EmailConfirmed
                        || !string.Equals(
                            user.Email,
                            invite.Email,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        throw new ApiException(
                            403,
                            "Sign in with the verified email address this invitation was sent to."
                        );
                    if (invite.UsedAt != null || invite.ExpiresAt <= DateTime.UtcNow)
                        throw new ApiException(400, "Invitation has expired or was already used.");
                    if (
                        await db.Memberships.AnyAsync(x =>
                            x.OrganizationId == invite.OrganizationId && x.UserId == user.Id
                        )
                    )
                        throw new ApiException(409, "You are already a member.");
                    if (
                        await db.Memberships.CountAsync(x =>
                            x.OrganizationId == invite.OrganizationId
                        ) >= w.MemberLimit(invite.Organization)
                    )
                        throw new ApiException(409, "Member limit reached.");
                    invite.UsedAt = DateTime.UtcNow;
                    db.Memberships.Add(
                        new Membership
                        {
                            OrganizationId = invite.OrganizationId,
                            UserId = user.Id,
                            Role = invite.Role,
                        }
                    );
                    w.Audit(invite.OrganizationId, "Invitation accepted", user.Name);
                    return w.OrganizationDto(invite.Organization, invite.Role);
                }
            )
            .RequireAuthorization()
            .AddEndpointFilter<WriteTransactionFilter>();
    }

    static void Apply(Project p, ProjectRequest r)
    {
        p.Name = Rules.Text(r.Name, "Project name");
        p.Description = r.Description?.Trim() ?? "";
        if (p.Description.Length > 2000)
            throw new ApiException(400, "Description must be at most 2000 characters.");
        p.Color = r.Color ?? "#c8f56b";
        if (!System.Text.RegularExpressions.Regex.IsMatch(p.Color, "^#[0-9a-fA-F]{6}$"))
            throw new ApiException(400, "Color must be a six digit hex color.");
        p.Status = r.Status ?? "Active";
        if (p.Status is not ("Active" or "Archived"))
            throw new ApiException(400, "Status must be Active or Archived.");
    }
}
