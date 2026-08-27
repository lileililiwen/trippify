# Why
Paid-guide checkout is exposed by the API and Flutter client, but the registered payment gateway always rejects checkout. The marketplace cannot complete a real purchase.

# What Changes
Add a production payment adapter, provider-hosted checkout creation, signed webhook verification, configuration validation, and operational documentation while preserving the explicit local-disabled mode.

# Capabilities
## New Capabilities
- `production-payment-processing`: real checkout sessions and authoritative, replay-safe payment events.

# Dependencies and Non-goals
- Dependencies: `guide-commerce-entitlements` and provider-secret configuration.
- Non-goals: subscriptions, multi-currency settlement, disputes, and creator payouts.

# Impact
Changes payment adapter registration, commerce configuration, webhook handling, Flutter checkout redirection, tests, and deployment documentation.
