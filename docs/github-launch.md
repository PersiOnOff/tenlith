# GitHub launch package

Publish after the [release gates](release-status.md) have evidence. This file is
copy for a future launch, not a claim that the repository or hosted demo is live.

## Repository settings

Name: `tenlith`

Description:

> Open-source .NET + React SaaS starter. Authentication, multi-tenancy, teams,
> Stripe billing and Docker — connected in one working product.

Suggested topics:

`dotnet`, `aspnet-core`, `react`, `typescript`, `saas`, `saas-boilerplate`,
`multi-tenancy`, `vertical-slice-architecture`, `stripe`, `docker`, `postgresql`.

Enable **Template repository** so people can use the starter without forking its
history. Enable private vulnerability reporting, protect the main branch with
passing checks and create a tagged pre-release with actual verification notes.
Set a homepage link only once there is a real hosted demo or documentation site.

## Show the complete journey

Use the genuine workspace screenshot in the README and record a short browser
clip: launch the demo, create a project, invite a teammate and simulate Pro.
Clearly label the demo billing. Avoid fake revenue, customer logos, stars or claims
of an audit. A full screen of badges communicates less than a working workflow.

For a public demo, isolate sessions, rate-limit creation, arrange cleanup and
remove private data. The supplied Compose configuration is local-only and must
not be exposed as the public demo configuration.

## Announcement draft

> I built Tenlith, an MIT-licensed .NET + React SaaS starter. It connects the pieces
> I usually have to wire together: account verification, organizations, roles,
> invitations and subscription entitlements. It runs locally with Docker and needs
> no payment credentials for the isolated demo. Projects is a small example slice
> you can replace with your own business logic. I'd especially appreciate feedback
> on tenant isolation, the developer experience and what a starter should leave out.

Add the actual repository/demo links and a real demo clip before posting. Tailor
the post to each community's rules and include meaningful technical tradeoffs.
Publication and messages to communities are not performed by this local build.

## First release notes

Include the exact tag/commit, install command, tested platforms, actual test results,
known limitations and upgrade instructions. Separate implemented behavior from
credential-dependent integrations and roadmap items. Make a few small, specific
issues available to new contributors instead of an unbounded feature wishlist.
