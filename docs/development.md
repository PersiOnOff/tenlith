# Development

## Docker workflow

```sh
docker compose up --build -d --wait
```

| Service | Local address |
| --- | --- |
| Web | http://localhost:8080 |
| API health | http://localhost:5080/health |
| Scalar (Development) | http://localhost:5080/scalar |
| OpenAPI (Development) | http://localhost:5080/openapi/v1.json |
| Mailpit inbox | http://localhost:8025 |
| PostgreSQL | localhost:5435, database/user `tenlith` |

The worker is part of the API process. The SMTP endpoint inside the network is
`mailpit:1025`. Compose generates its own database password and JWT signing key in
the `secrets` volume. These are development credentials, not production defaults.

To rebuild after changes: `docker compose up --build -d --wait`.
If port 8080 is occupied, copy `.env.example` to `.env` and set
`TENLITH_WEB_PORT=8090` and `TENLITH_PUBLIC_URL=http://localhost:8090` together.
Set `PLAYWRIGHT_BASE_URL` and `TENLITH_TEST_URL` to that URL when running checks.
To stop without deleting persistent data: `docker compose down`.
Deleting the database volume is a destructive reset; back up anything valuable
before doing so. Keep its generated credentials volume with the database volume.

## Native .NET and Vite

Install .NET SDK 10 and Node.js 24. The root `global.json` selects .NET 10 and
allows compatible feature bands. Use your own PostgreSQL database and SMTP server
or start the development infrastructure:

```sh
docker compose up -d db mailpit
```

Supply a connection string, a random signing key of at least 32 bytes, the web
origin and SMTP settings through .NET user secrets or environment variables.
The database password is generated in Docker; retrieve it locally without pasting
it into issues, logs or source control. Alternatively use a separate local database
with a password you control.

```sh
dotnet user-secrets init --project src/Tenlith.Api
dotnet user-secrets set "ConnectionStrings:Database" "Host=localhost;Port=5435;Database=tenlith;Username=tenlith;Password=YOUR_PASSWORD" --project src/Tenlith.Api
dotnet user-secrets set "Jwt:Key" "YOUR_RANDOM_SIGNING_KEY_AT_LEAST_32_BYTES" --project src/Tenlith.Api
dotnet user-secrets set "App:PublicUrl" "http://localhost:5173" --project src/Tenlith.Api
```

Run the API in Development at port 5080. Enable `Demo__Enabled=true` and
`Billing__Simulation=true` only when you want the isolated demo workflow. Vite
proxies `/api` to `http://localhost:5080`.

```sh
dotnet run --project src/Tenlith.Api --urls http://localhost:5080
```

In a second terminal:

```sh
cd src/Tenlith.Web
npm ci
npm run dev
```

On PowerShell set environment variables with `$env:NAME = 'value'`; on a POSIX
shell use `export NAME=value`. For native execution, set
`ASPNETCORE_ENVIRONMENT=Development`, `Mail__Host=localhost`, `Mail__Port=1025`
and the public URL matching Vite. Compose publishes SMTP at `127.0.0.1:1025`.

## GitHub OAuth

Create a GitHub OAuth App with the web homepage URL and callback
`http://localhost:8080/api/auth/github/callback`. Configure `GitHub:ClientId` and
`GitHub:ClientSecret` (Compose `.env`: `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET`).
For Vite use `http://localhost:5173/api/auth/github/callback`; its `/api` proxy
already routes the callback to the API.
Use separate OAuth apps/secrets per environment. Never send OAuth secrets to Vite.

## Checks

```sh
dotnet test tests/Tenlith.Api.Tests/Tenlith.Api.Tests.csproj -c Release
```

The advisory-lock concurrency test uses a real database. Set
`TENLITH_TEST_POSTGRES` to a local PostgreSQL connection string with permission
to create temporary databases. The test creates a uniquely named database,
applies migrations, tests competing provider reconciliations and drops that
database afterward. Without this setting it is explicitly skipped; CI supplies it.

From `src/Tenlith.Web`:

```sh
npm test -- --run
npm run build
npx playwright install chromium
npx playwright test
```

Browser tests target the Compose app at port 8080. Override
`PLAYWRIGHT_BASE_URL` to point to another local test instance. See
[release status](release-status.md) for actual results rather than assuming that
the presence of a test means it has run successfully.

For the real PostgreSQL/SMTP security journey, from the repository root run:

```sh
node scripts/verify-stack.mjs
# Also verify state and account-token durability across a Compose API restart:
node scripts/verify-stack.mjs --restart-api
```

This creates disposable local test accounts, organizations and emails. It checks
tenant denial, concurrent quotas, refresh replay, verified registration,
invitations, roles and password recovery. It refuses non-localhost targets.
