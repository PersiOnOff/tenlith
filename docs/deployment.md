# Deployment

The supplied `compose.yaml` is a **local Development stack**. It enables demo
accounts and billing simulation and exposes a mail inbox. Do not deploy it as-is
to a public server. The application is an initial development release; review
[release status](release-status.md) before planning a production launch.

## Configuration contract

.NET environment variables use `__` in place of `:`. Use the platform's secret
manager for secrets and keep them out of frontend build variables.

| Setting | Purpose |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` on a public deployment |
| `ConnectionStrings__Database` | Private PostgreSQL connection string |
| `App__PublicUrl` | Exact HTTPS browser origin; email URLs and origin checks |
| `Jwt__Key` | Cryptographically random signing key, at least 32 bytes |
| `Demo__Enabled` | `false` in production |
| `Billing__Simulation` | `false` in production |
| `Mail__Host`, `Mail__Port`, `Mail__From` | Your SMTP delivery provider |
| `Mail__UseTls`, `Mail__Username`, `Mail__Password` | SMTP transport security and credentials |
| `DataProtection__KeysPath` | Persistent directory writable by the API user |
| `Proxy__KnownProxies__0` | Exact trusted reverse-proxy IP; add numbered entries for more |
| `GitHub__ClientId`, `GitHub__ClientSecret` | Optional OAuth application |
| `Stripe__SecretKey`, `Stripe__WebhookSecret`, `Stripe__PriceId` | Optional live billing |
| `Billing__FreeProjectLimit`, `Billing__FreeMemberLimit` | Free quotas; defaults 3/3 |
| `Billing__ProProjectLimit`, `Billing__ProMemberLimit` | Pro quotas; defaults 50/25 |

## Operational checklist

- Build immutable API/web images from a reviewed commit. Use a supported PostgreSQL
  version, private networking and your own credentials; restrict database access.
- Terminate HTTPS at a trusted proxy. Forward the actual public host/protocol and
  configure the API to trust only known proxies. Never blindly trust arbitrary
  `X-Forwarded-*` headers from the internet. Add HSTS at the public edge.
- Serve browser/API on one origin. Refresh cookies must be Secure in Production.
  Keep the custom mutation header and origin validation enabled.
- Persist Data Protection keys across restarts and instances, restrict permissions
  and protect the key ring at rest. Persisting only PostgreSQL is insufficient for
  account tokens and OAuth continuity.
- Apply reviewed EF migrations in a controlled release step before routing traffic.
  Take a database backup first. Verify migrations on a restored staging database.
  Do not run concurrent schema migration jobs across replicas.
- Configure SMTP delivery, TLS/authentication, sender-domain records and monitor
  bounces. Confirm email delivery outside Mailpit before inviting real users.
- Configure Stripe in test mode first. Verify live keys, price and webhook endpoint
  separately before enabling paid actions. Never enable simulation in Production.
- Configure log retention, availability checks and alerts for worker failures,
  dead-letter jobs, provider failures and database capacity. Do not log passwords,
  bearer tokens, OAuth secrets or complete email action URLs.
- Plan rate limiting at the public edge and API. Local in-memory limits alone do
  not provide a shared budget across replicas.
- Expose readiness through `/health`; test database connectivity and traffic routing.
  Run the smoke journey against staging after every release.

## Backups and recovery

The API supports a one-shot migration command with the same production
configuration/secrets as the service:

```sh
dotnet Tenlith.Api.dll --migrate
```

Run this from the published API directory (or as a release job in its image).
Development applies migrations at startup; Production uses this explicit step.

Use automated PostgreSQL backups with retention outside the application host.
Prefer managed point-in-time recovery for paid deployments. Back up the Data
Protection key ring securely. Set recovery-time and recovery-point targets and
prove them with restore drills; a successful backup command is not a restore test.

Local database-only snapshot (the file may contain personal data):

```sh
docker compose exec -T db pg_dump -U tenlith -d tenlith -Fc > tenlith-backup.dump
```

Use a binary-safe shell or the database vendor's tooling when redirecting binary
output. Restore into an **empty, separate database** first, validate counts,
membership isolation, login and billing reconciliation, then plan any cutover.
Do not test recovery by overwriting your only live database.

## Upgrade and rollback

Retain the preceding image and configuration. Review schema compatibility before
rolling back code. A destructive schema migration requires its own recovery plan;
restarting the old container does not undo a database change. Reconcile provider
subscriptions after an outage rather than replaying redirects from users.
