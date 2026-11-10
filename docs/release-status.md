# Release status

**Initial development release — local verification completed, 2026-11-10.**

This file records evidence from the local build. It does not claim a public
deployment, independent security audit or completed production-readiness review.

## Verification record

| Check | Evidence |
| --- | --- |
| Docker Compose syntax | `docker compose config --quiet` passed |
| Secret initialization | Fresh `tenlith_secrets` volume generated successfully |
| PostgreSQL | Development Compose database started and became healthy |
| Mailpit | Inbox/SMTP container started; HTTP and SMTP bound to localhost |
| API integration/security suite | 23 passed, 0 failed, 0 skipped with `TENLITH_TEST_POSTGRES` supplied; includes real PostgreSQL advisory-lock concurrency |
| Web behavioral tests and build | 9 Vitest tests passed; TypeScript and Vite production build passed |
| Full container build/start | Both images built; API, web, PostgreSQL and Mailpit healthy |
| Real PostgreSQL/SMTP acceptance journey | `node scripts/verify-stack.mjs --restart-api` passed through the reverse proxy |
| Restart durability | Data, refresh session and a reset token issued before restart survived API restart |
| Production configuration | Development demo/simulation rejected; controlled `--migrate` command completed |
| Browser journey, mobile and screenshots | Playwright desktop and Pixel 7 Chromium journeys passed; no page JavaScript errors or horizontal page overflow |
| Code review | Independent review; important findings fixed and checked, no additional important findings in final pass |
| Stripe test-account checkout and GitHub OAuth | Not exercised: user credentials not supplied |
| Public GitHub repository / hosted demo | Not published |

Validation used Windows, Docker Desktop Linux containers, PostgreSQL 17, .NET 10,
Node 24 and Chromium. The GitHub Actions workflow is supplied but has not yet run
on GitHub. Real browser screenshots and a short unedited screen recording are
included in `assets/`. This is not an independent security audit.

The local verification used port 8090 because port 8080 was occupied by another
project. This override lives in ignored `.env`; the documented default remains
8080. The generated development database and key volumes are retained locally.

## Operational limits

Email jobs are delivered at least once: a crash after SMTP acceptance and before
recording completion can cause duplicate delivery. Stable Message-ID helps mail
systems recognize repeats, but does not promise exactly-once SMTP delivery. Jobs
stop automatic retries after eight attempts; operators must monitor and arrange
dead-letter recovery and retention. The API rate limiter is in memory per process;
multi-instance deployments need an additional shared/edge abuse budget.

## External release gates

Before labeling a release production-ready:

- Validate actual GitHub OAuth with the registered production callback and HTTPS.
- Validate Stripe test-mode checkout, portal, subscription updates, cancellation,
  payment failure and reconciliation using your own account, then review live setup.
- Complete the deployment guide: HTTPS/proxy trust, secret storage, durable keys,
  SMTP delivery, database migrations, backups and a proved restore procedure.
- Review abuse prevention, job retention/dead letters, monitoring and quotas for
  the deployment's expected traffic and tenancy model.
- Publish a real demo only after its isolation, cleanup and abuse policy are set.

Automated fixtures exercise application behavior, but do not prove that a
provider account, sender domain or deployment is configured correctly.
