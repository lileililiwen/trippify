# Context
`LocalProviders.CreateCheckoutAsync` throws for every request, although commerce orders and webhook state already exist.

# Goals / Non-goals
- Create usable paid-guide checkout without making the client authoritative.
- Preserve deterministic local/test adapters; do not fabricate successful payment.

# Decisions
- Select the adapter from explicit configuration and fail production startup when paid commerce is enabled without valid provider settings.
- Return a provider checkout URL and opaque reference; never expose provider secrets.
- Verify the raw webhook body and provider signature before persistence, then reuse the existing event-inbox and forward-only transition rules.
- Treat timeouts and provider rejection as `503` without persisting an order.

# Authorization, privacy, and failure modes
- Checkout remains authenticated and creator self-purchase remains forbidden.
- Logs and telemetry exclude tokens, payment details, discount codes, and webhook bodies.
- Duplicate provider events are acknowledged without duplicating orders, entitlements, or ledger rows.

# Migration and rollback
- Add only fields/indexes required for provider identifiers through a forward-only migration; rollback is configuration-based by disabling paid checkout.
