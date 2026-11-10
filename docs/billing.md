# Billing

Tenlith has Free and Pro plans with project/member quotas. Defaults are 3/3 and
50/25. Plan limits are configuration values. The API enforces them; hiding a
button in React is not a permission or entitlement check.

## Local simulation

The development Compose stack enables `Billing:Simulation`. Organization owners
can switch Free/Pro through Billing, exercising the real entitlement path without
a card or Stripe account. It is explicitly labeled simulated. Production must
reject simulation configuration. Simulated state must not be migrated into live
customer subscription state without an explicit migration decision.

## Stripe test integration

1. Create a recurring price in a Stripe test account. Enable Customer Portal.
2. Supply `Stripe:SecretKey`, `Stripe:PriceId` and `Stripe:WebhookSecret` to the API.
3. Copy `.env.example` to `.env`, supply the Stripe values and set
   `TENLITH_BILLING_SIMULATION=false`. Recreate the API with
   `docker compose up -d api` to test actual provider behavior. For native .NET,
   set `Billing__Simulation=false` instead. Do not use real cards in test mode.
4. Forward events with Stripe CLI:

```sh
stripe listen --forward-to localhost:5080/api/billing/webhook
```

Use the CLI's printed signing secret for this local endpoint. A CLI secret is
different from the signing secret for a dashboard-registered endpoint. In
Production register the exact HTTPS webhook URL and use that endpoint's secret.

5. Complete Checkout using Stripe's documented test cards. Confirm subscription
   updates through the webhook/reconciliation flow, then try cancellation and
   payment failure through Customer Portal/test tooling.

Subscribe to the subscription lifecycle events consumed by the implementation;
inspect the billing module and test create/update/delete before going live. Never
assume a successful browser return proves that a payment has cleared.

## Correctness checks

- Verify the signature over the original raw request body.
- Repeated event IDs must not repeat a state transition or effect.
- Out-of-order events must not roll back the current provider subscription state.
- A customer/subscription belongs to an existing organization; client metadata
  alone must not allow it to be reassigned to an arbitrary organization.
- Failed, unpaid, canceled or unknown states must not silently grant Pro access.
- Provider calls are external I/O; retries must preserve idempotency and failed
  reconciliation must remain visible to operators.

The supplied release report distinguishes automated fixtures from real Stripe
test-account validation. Real keys/cards are never included in this repository.

Official references: [webhooks](https://docs.stripe.com/webhooks),
[subscriptions](https://docs.stripe.com/billing/subscriptions/overview),
[testing](https://docs.stripe.com/testing).

## Additional providers

YuKassa is deferred. Add a provider only after its checkout, verified callback,
subscription lifecycle and reconciliation contracts have tests. Keep provider
details outside the Projects module and never fork entitlement rules by UI route.
