<p align="center">
  <img src="assets/brand/cover.svg" alt="Tenlith — Your SaaS. Solid from day one." width="100%" />
</p>

<p align="center">
  <strong>An open-source .NET + React foundation for your next SaaS.</strong><br />
  Authentication, organizations, teams and subscriptions — connected in one working product.
</p>

<p align="center">
  <a href="#quick-start">Quick start</a> ·
  <a href="docs/architecture.md">Architecture</a> ·
  <a href="docs/development.md">Development</a> ·
  <a href="docs/deployment.md">Deployment</a> ·
  <a href="CONTRIBUTING.md">Contributing</a>
</p>

---

## Build your product, starting with the product

Clone Tenlith, start the stack, and explore a complete SaaS workflow. Create an
organization, invite your team, manage projects and try a subscription upgrade.
The sample Projects module shows where your own business logic belongs.

**One API. One web app. Your database. No paid account needed to try it locally.**

<img src="assets/screenshots/workspace.png" alt="Tenlith workspace running locally with projects, team and subscription settings" width="100%" />

[Watch the real browser journey](assets/demo.webm) ·
[Landing page](assets/screenshots/landing.png) ·
[Mobile workspace](assets/screenshots/mobile.png)

## Quick start

Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) or Docker
Engine with Compose v2. From a checkout:

```sh
docker compose up --build -d --wait
```

Open **[localhost:8080](http://localhost:8080)** and choose **Explore the demo**. Each
demo session receives its own isolated workspace. No OAuth, email or payment
credentials are needed. To test regular sign-up, open the verification email in
the local **[mail inbox](http://localhost:8025)**.

The first build downloads the images and dependencies. Database and signing
secrets are generated automatically and persist in Docker volumes. The stack
binds published ports to localhost; it is a development environment.

```sh
docker compose logs -f api   # API and background worker
docker compose down        # stop; preserve data
```

See [development](docs/development.md) for running .NET and Vite outside Docker.

## What's inside

| Capability | What you get |
| --- | --- |
| Identity | Email/password, verification, password recovery, JWT access and rotating refresh sessions |
| Social login | Optional GitHub OAuth; regular sign-up works without provider credentials |
| Organizations | Multiple workspaces per account, server-side membership and tenant boundaries |
| Teams | Owner/Admin/Member, expiring invitations and ownership transfer |
| Billing | Stripe Checkout, Customer Portal, signed webhooks and local plan simulation |
| Entitlements | API-enforced project and member quotas for Free and Pro plans |
| Example feature | Tenant-scoped projects with create, edit, archive and delete flows |
| Background work | Database-backed email jobs and subscription reconciliation |
| Interface | Responsive workspace, public landing page, dark/light themes and accessible forms |
| Developer experience | OpenAPI/Scalar, Docker Compose, tests and GitHub Actions |

**Release status:** initial development release. Read [verification and release
status](docs/release-status.md) for exactly what has been exercised and what still
needs external credentials. YuKassa is planned, not implemented. A hosted demo is
not published yet.

## Small enough to understand

```text
src/
  Tenlith.Api/         ASP.NET Core, identity, tenant slices, EF Core and worker
  Tenlith.Web/         React + TypeScript application
tests/
  Tenlith.Api.Tests/   HTTP integration and security regression tests
docs/                 Architecture, setup, billing and operations
scripts/              Container startup and same-origin reverse proxy
```

Vertical slices keep a feature's requests, validation, authorization and handling
close together. The application is a modular monolith: no required Kubernetes,
distributed broker or service mesh. [Explore the architecture →](docs/architecture.md)

## Make it yours

1. Rename the sample product and replace the brand assets.
2. Follow [adding a feature](docs/adding-a-feature.md) to create your first slice.
3. Configure [GitHub OAuth](docs/development.md#github-oauth) and
   [Stripe](docs/billing.md) when you need them.
4. Follow the [deployment guide](docs/deployment.md) before exposing an instance.

The local billing simulator is deliberately restricted to Development. A browser
redirect never grants paid access. Production requires your own keys, email
delivery, HTTPS, backups and operational checks.

## Contribute

Bug reports with a reproducible case, security regression tests, accessibility
improvements and clearer documentation are welcome. Read [CONTRIBUTING](CONTRIBUTING.md)
and [SECURITY](SECURITY.md) first.

If Tenlith saves you setup time, a star helps other .NET developers find it.

## License

[MIT](LICENSE). Use it for personal and commercial projects. The core is free.
