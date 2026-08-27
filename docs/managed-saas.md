# Managed SaaS hosting

Hosted tenants need isolated provisioning, a clear plan, and observable quota usage. This slice introduces tenants, subscriptions, per-tenant quotas, an export endpoint, and an audited admin console.

## Tenants

- `POST /api/v1/admin/tenants { slug, displayName, primaryDomain? }` provisions a tenant. Slug and (optional) primary domain are unique; duplicates return `409`.
- `GET /api/v1/admin/tenants` lists tenants (slug-sorted).
- `GET /api/v1/admin/tenants/{tenantId}` returns a single tenant.
- `PUT /api/v1/admin/tenants/{tenantId} { displayName, primaryDomain, brandingJson }` updates branding and custom domain.
- `POST /api/v1/admin/tenants/{tenantId}/suspend { reason }` flips the tenant's status to `Suspended` after validating that a non-empty reason is supplied.

## Subscriptions and quotas

- Every caller has a tenant. `GET /api/v1/me/tenant` lazily provisions `Tenant + Subscription (Free, Active) + TenantMember (Owner)` on first hit, returning both tenant and subscription in a single payload.
- `POST /api/v1/me/tenant/subscription { plan }` upgrades or downgrades. Idempotent for the same plan; emits a single `subscription-updated` audit row on actual changes.
- `PUT /api/v1/admin/tenants/{tenantId}/quotas/{metric} { limit }` upserts the quota cap for the metric in the current period. The endpoint normalizes negative limits to `0` and rejects in the response.
- `GET /api/v1/me/tenant/quotas` returns the caller's quota rows with `used`, `limit`, `periodStart`, and `periodEnd`. The quota check that triggers enforced rejection lives in `MapGuides` integration tests once wired; this slice surfaces the per-tenant roster.

## Export and deletion

- `POST /api/v1/me/tenant/export` returns a JSON dump containing the caller's display name, locale, and an array of purchase rows (without exposing other tenants' data). The endpoint records a `tenant-export` audit row.
- `POST /api/v1/me/tenant/deletion { reason }` requires a non-empty reason and records a `deletion-requested` audit row.

## Audit

- `GET /api/v1/admin/tenants/{tenantId}/audit` returns the most recent `TenantAuditEntry` rows. The reasons field is the only free-form text returned; no manifests, secrets, or sensitive configuration are exposed.

## Operations

The `Trippify.ManagedSaas` meter emits `trippify.managedsaas.commands` with low-cardinality `operation` tags (`tenant-created`, `tenant-updated`, `tenant-suspended`, `quota-set`, `subscription-updated`, `tenant-export`, `tenant-deletion-request`). Manifests, secrets, and passwords are never part of the telemetry. Alert on elevated `403` (someone probing tenant admin) or quota-set errors (operators tuning beyond usage).

## Privacy guarantees

- `GET /api/v1/me/tenant` and `/quotas` always return the **caller's** tenant data; the admin-only routes are isolated under `Administrator`-role gates.
- Export payloads include only the caller's purchases; cross-tenant data is structurally impossible.
- Audit entries store only reasons, never sensitive payloads, ensuring leak risk stays minimal even when an admin role is compromised.
