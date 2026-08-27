# Why
Managed SaaS stores quota rows but does not reject over-limit operations, so configured plans have no effective limits.

# What Changes
Add atomic tenant quota reservation, consumption, release, period rollover, plan defaults, and enforcement across metered operations.

# Capabilities
## New Capabilities
- `managed-saas-quota-enforcement`: server-authoritative quota checks for tenant resources and provider usage.

# Dependencies and Non-goals
- Dependencies: managed SaaS tenants/subscriptions and durable background processing for reconciliation.
- Non-goals: billing invoices, overage charging, or client-side enforcement.

# Impact
Changes guide/import/media operations, quota persistence, tenant/admin APIs, Flutter denied states, and telemetry.
