# Security policy

Tenlith is currently an initial development release. No release line is designated
as a long-term security-supported version yet. Check the release status before
deploying to real customers.

## Reporting a vulnerability

Do not publish exploit details, tokens or customer data in a public issue. Once
this project is published on GitHub, use **Security → Report a vulnerability** if
private vulnerability reporting is enabled. If unavailable, contact the repository
owner privately through their published profile contact. The maintainer must
enable a private reporting channel before inviting public security reports.

Include affected version/commit, prerequisites, a minimal reproduction and impact.
Use accounts and organizations you control. Never test against someone else's
deployment without permission.

## Areas requiring particular care

Tenant boundaries, authorization, invitation/token replay, OAuth account linking,
refresh sessions, webhook verification, quotas under concurrency and job claiming
are security-relevant. Provider integration tests alone do not replace staging
validation with actual credentials. Configure HTTPS, backups, logs and secrets
according to [deployment](docs/deployment.md).
