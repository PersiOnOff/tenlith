# Tenlith — first release design

Status: approved for implementation on 2026-11-10; renamed to Tenlith by user approval.

## Purpose

Build a free, open-source .NET + React SaaS starter for independent developers,
freelancers and small product teams. The principal demonstration is a complete
journey: register, create an organization, invite a colleague, assign a role,
subscribe to a plan and access a feature allowed by that plan.

The repository should be visually polished and useful enough to earn adoption.
GitHub stars are an ambition, not an acceptance criterion or a promised outcome.

## Decisions and assumptions

The user selected .NET + React TypeScript, a SaaS starter, all three audiences,
and accepted the proposed Tenlith name and graphite/lime visual direction.
The following implementation choices are proposed by the builder, not yet
individually agreed: MIT license, English-first content, PostgreSQL, Stripe as
the first live payment integration, and the scope below.

The previous working name Launchlane was rejected after finding existing
software businesses at https://launchln.com/ and https://www.launchlane.com/.
Do not claim uniqueness, domain ownership or trademark clearance. Keep public
repository creation and branding publication outside this local design step.

## Product and architecture

- One modular monolith API and one React TypeScript application.
- ASP.NET Core, EF Core and PostgreSQL. Select a supported stable .NET version
  and compatible packages during implementation; pin the SDK and dependencies.
- Vertical slices within modules: Identity, Organizations, Billing and Projects.
  Endpoints use explicit request validation and authorization. Avoid generic
  repositories, a plugin framework or a mandatory distributed message broker.
- React with Vite, accessible reusable components and a query cache. Exact
  library versions are implementation decisions, verified before installation.
- Organization-scoped Projects provide a small real example of CRUD, role
  checks and plan quotas. They are replaceable sample business functionality.
- API reference through OpenAPI and Scalar in development. Persistent background
  jobs handle email delivery and billing reconciliation, with bounded retries.

## Identity and tenant boundaries

Use ASP.NET Core Identity for password hashing and account tokens. Support email
verification, password reset, GitHub OAuth, short-lived JWT access tokens and
rotating, revocable refresh tokens. OAuth requires deployment-specific secrets;
ordinary email registration must work without an OAuth account configuration.

Keep access tokens in memory and refresh tokens in HttpOnly cookies. Persist
hashed refresh tokens, detect replay and revoke token families on reuse. Explicit
origin, CSRF, cookie, OAuth state and callback protections are release requirements.

An organization is a tenant, and users may belong to several. Resolve the tenant
from the requested organization and verified membership, never solely from a
client-supplied header. Apply isolation to reads, writes, background jobs, foreign
keys and resource identifiers. Tenant-owned relationships cannot cross tenants.

Roles: Owner, Admin and Member. Owners manage billing, transfer ownership and
delete the organization; Admins manage ordinary members and projects; Members
access the organization's projects. Prevent removal of the last Owner. Invitations
are expiring, single-use and bound to the verified recipient email. Record audit
events for membership, role, ownership and billing changes, excluding secrets.

## Billing and local development

Stripe Checkout and Customer Portal operate in test mode during development.
Verify webhook signatures against raw bodies, deduplicate events and reconcile
out-of-order deliveries against provider subscription state. Never grant paid
access based on a checkout redirect. Check plan entitlements and quotas on the API.

Free and Pro plans demonstrate project and member limits. Price IDs and limits
are configuration, not hard-coded amounts. A clearly labeled local simulation
allows the complete demo without payment credentials. Simulation cannot be
enabled in production. An unconfigured live provider fails closed for paid actions.

YuKassa is deferred to a subsequent release; do not advertise it as implemented.
Use a narrow provider boundary around checkout, portal and entitlement updates,
without building a speculative general-purpose payments framework.

## User experience and repository presentation

English UI, README and primary docs. Responsive desktop/mobile layouts, keyboard
navigation, visible focus, sufficient contrast, dark and light themes. Graphite,
lime accents and a simple two-lane arrow mark form the initial visual direction.

Screens: landing page, sign-in and recovery, onboarding, organization switcher,
overview, projects, team/invitations, billing and account/organization settings.
Include loading, empty, validation, error and forbidden states. Demo activity
must come from seeded records or actual events; do not fabricate revenue charts.

README: original SVG cover, real product screenshot, short demo recording,
one-command quick start, feature/status table, architecture, deployment links,
security boundaries and contributor entry points. Do not include fake badges,
test results, customer claims, hosted demo links or star counts.

Provide MIT license, CONTRIBUTING, SECURITY, issue/PR templates, CI, architecture
decisions, environment configuration, deployment, backup/restore, billing setup
and a guide to adding a vertical slice. No paid core or required vendor account
for the local demo. Hosted demo and GitHub publication are separate deliverables
that require an available host/account and the user's publication instruction.

## Release acceptance

1. From a clean checkout, the documented Docker Compose command builds and starts
   web, API, database, development mail inbox and worker processing. Health checks
   gate readiness; initial data setup is repeatable. No production default secrets.
2. Registration, verification, sign-in, refresh, logout and password recovery work.
   OAuth works with configured credentials and is clearly unavailable otherwise.
3. The full organization/invitation/project journey persists after restart.
   Integration tests prove cross-tenant denial, role rules and last-owner safety.
4. Stripe test checkout, verified webhooks, duplicate/out-of-order handling,
   cancellation and reconciliation enforce the expected subscription state.
5. Tests cover invitation replay/expiry, token replay/revocation, quota enforcement
   under concurrent writes, and background job retry/idempotency behavior.
6. CI runs backend tests, frontend type checks/build, relevant UI tests and a
   browser smoke journey. Docker images build. Record what was actually executed;
   unavailable provider credentials must be reported as verification gaps.
7. Deployment configuration covers HTTPS/proxy handling, secrets, migrations,
   durable jobs, structured logs, health checks, rate limiting and backups.
8. Screenshots and quick-start instructions match the implemented application.
   Use 'production-ready' only with documented operational checks and limitations.

## Delivery boundaries

Deliver one complete starter before expanding providers or modules. No Kubernetes,
microservices, marketplace, AI integration, complex metering or mobile app in v1.
Follow design review with an implementation plan, then working code and verification.
The public launch package can be prepared locally; publishing is a separate action.

Tenlith is the approved name. Initial GitHub repository search found no matches; domain and trademark availability are not asserted.
