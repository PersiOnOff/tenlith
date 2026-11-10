# Add your first feature

Use Projects as the reference slice. A feature should own its routes, input/output
models, validation and persistence behavior. Share security and infrastructure,
not a generic abstraction for every possible future feature.

For an example `Documents` feature:

1. Define the observable behavior and add a failing integration test: an Owner
   creates a document, a Member can read it, another organization's user cannot.
2. Add a tenant-owned entity with an explicit `OrganizationId`. Use composite
   foreign keys for relationships to other tenant-owned objects.
3. Map endpoints beneath `/api/organizations/{organizationId}/documents`. Check
   membership before resolving resources and include the organization in queries.
4. Validate lengths, allowed values and required fields on the API. Return useful
   ProblemDetails without database or secret details.
5. For a plan-limited create operation, acquire the same organization's quota
   guard before checking usage and writing. Test simultaneous requests at the cap.
6. Add an EF migration and review its SQL. Verify it on an empty database and a
   database containing earlier application data.
7. Add the typed client calls and UI to the web app. Reuse session, error and
   organization selection behavior. Keep secrets and authorization logic on the API.
8. Add empty, denied, validation and server-error states. Exercise the flow in a
   real browser and rerun backend tests and the frontend build.

If work must outlive the request, enqueue a durable job with a tenant ID and an
idempotency key. Do not use a fire-and-forget task holding a request-scoped DbContext.
Document the retry policy and what happens after the last attempt.
