# API contract

Base `/api`. JSON camelCase. Errors: RFC ProblemDetails with `detail` and optional
`errors`. Success POST normally 200/201; commands may return 204. Protected routes
use `Authorization: Bearer <accessToken>`. Refresh cookie same-origin HttpOnly.
Web mutations always send `X-Tenlith-Client: web`; server checks this plus Origin
against configured public origin. Integration clients can send this header too.
Never persist access tokens in localStorage/sessionStorage.

## Session

- GET `/auth/config` -> `{githubEnabled:boolean,demoEnabled:boolean}`.
- POST `/auth/register` `{name,email,password}` -> `{message}` (email verification).
- POST `/auth/verify-email` `{userId,token}` -> `{message}`.
- POST `/auth/login` `{email,password}` -> session below, sets refresh cookie.
- POST `/auth/refresh` `{}` -> session below, rotates cookie.
- POST `/auth/logout` `{}` -> 204, revokes session and deletes cookie.
- GET `/auth/me` -> `{id,name,email}`.
- POST `/auth/forgot-password` `{email}` -> generic `{message}`.
- POST `/auth/reset-password` `{email,token,password}` -> `{message}`.
- GET `/auth/github` -> OAuth challenge, callback redirects to `/app` with refresh
  cookie; SPA exchanges it for an access token via refresh. No token in URL.
- POST `/auth/demo` `{}` -> a fresh isolated seeded workspace/session, only in
  Development when `Demo:Enabled=true`; no shared writable demo tenant.

Session: `{accessToken,user:{id,name,email}}`.

## Organizations

Organization: `{id,name,slug,role,plan,createdAt}`. Roles `Owner|Admin|Member`,
plans `Free|Pro`. All IDs are UUID strings.

- GET `/organizations` -> organization array.
- POST `/organizations` `{name}` -> organization.
- PATCH `/organizations/{id}` `{name}` -> organization (Owner).
- DELETE `/organizations/{id}` -> 204 (Owner).
- GET `/organizations/{id}/overview` -> `{projectCount,memberCount,plan,projectLimit,
  memberLimit,activity:[{id,action,actorName,createdAt}]}`.
- GET `/organizations/{id}/projects` -> project array.
- POST `/organizations/{id}/projects` `{name,description,color}` -> project.
- PATCH `/organizations/{id}/projects/{projectId}` `{name,description,color,status}`
  -> project. Status `Active|Archived`. Admin/Owner writes; Member reads.
- DELETE `/organizations/{id}/projects/{projectId}` -> 204 (Admin/Owner).

Project: `{id,name,description,color,status,createdAt}`. color is a validated hex
color; default `#c8f56b`. Free project/member limits 3/3; Pro 50/25, configurable.

## Team

- GET `/organizations/{id}/members` -> `[{id,userId,name,email,role,joinedAt}]`.
- PATCH `/organizations/{id}/members/{memberId}` `{role}` -> 204. Owner changes
  Admin/Member; ownership transfer is separate. Admin cannot mutate Admin/Owner.
- DELETE `/organizations/{id}/members/{memberId}` -> 204; same role rules.
- POST `/organizations/{id}/transfer-ownership` `{memberId}` -> 204 (Owner only).
- GET `/organizations/{id}/invitations` -> `[{id,email,role,expiresAt}]` (managers).
- POST `/organizations/{id}/invitations` `{email,role}` -> `{message}` (send email).
- DELETE `/organizations/{id}/invitations/{invitationId}` -> 204 (managers).
- POST `/invitations/accept` `{token}` -> organization; verified recipient only.

## Billing

- GET `/organizations/{id}/billing` -> `{plan,status,provider,projectLimit,memberLimit,
  canManage,configured}`; provider `simulation|stripe|unconfigured`.
- POST `/organizations/{id}/billing/simulate` `{plan}` -> same billing payload;
  only Development with demo simulation enabled, Owner only.
- POST `/organizations/{id}/billing/checkout` `{}` -> `{url}` (Owner).
- POST `/organizations/{id}/billing/portal` `{}` -> `{url}` (Owner).
- POST `/billing/webhook` -> Stripe signed webhook; excluded from client header
  requirement, uses its own signature verification.

## Email links

Public web origin routes `/verify-email?userId=...&token=...`,
`/reset-password?email=...&token=...`, `/invite?token=...`.
Use URL-encoded Identity tokens. Local inbox: http://localhost:8025.

## Runtime configuration

API `ConnectionStrings:Database`, `App:PublicUrl` (http://localhost:8080),
`Jwt:Key`, `Demo:Enabled`, `Billing:Simulation`, `Billing:FreeProjectLimit`,
`Billing:FreeMemberLimit`, `Billing:ProProjectLimit`, `Billing:ProMemberLimit`,
`Stripe:SecretKey`, `Stripe:WebhookSecret`, `Stripe:PriceId`,
`GitHub:ClientId`, `GitHub:ClientSecret`, `Mail:Host`, `Mail:Port`, `Mail:From`.
API HTTP port 8080 in container; web port 80, host 8080. PostgreSQL service `db`,
database/user `tenlith`; password and Jwt key are generated on first Compose start.
