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
- `Guides`, `AiImports`, `MediaMegabytes`, and `BackgroundJobs` are the supported metered operations. Free, Pro, and Enterprise plans receive idempotently seeded monthly defaults; unknown metric configuration fails closed.
- Guide creation, AI import/translation, and media upload reserve capacity before changing domain state. A successful operation finalizes usage; provider or transaction failure releases it. PostgreSQL row locks and serializable transactions prevent concurrent requests from consuming the same final unit. The background-job metric is enforced by the same reservation service and is ready for job enqueue paths introduced by the durable-jobs change.
- `PUT /api/v1/admin/tenants/{tenantId}/quotas/{metric} { limit }` sets a custom current-period limit. `POST /api/v1/admin/tenants/{tenantId}/quotas/{metric}/adjustments { amount, reason }` appends an audited adjustment; it never rewrites history and cannot make usage negative.
- `GET /api/v1/me/tenant/quotas` returns only the caller's current and historical tenant periods with `used`, `limit`, `periodStart`, and `periodEnd`. A denied operation returns a `403` Problem Details response with stable `quota-exceeded` or `tenant-suspended` code, metric, used/reserved amount, limit, and reset time.

## Export and deletion

- `POST /api/v1/me/tenant/export` returns a JSON dump containing the caller's display name, locale, and an array of purchase rows (without exposing other tenants' data). The endpoint records a `tenant-export` audit row.
- `POST /api/v1/me/tenant/deletion { reason }` requires a non-empty reason and records a `deletion-requested` audit row.

## Audit

- `GET /api/v1/admin/tenants/{tenantId}/audit` returns the most recent `TenantAuditEntry` rows. The reasons field is the only free-form text returned; no manifests, secrets, or sensitive configuration are exposed.

## Operations

The `Trippify.ManagedSaas` meter emits `trippify.managedsaas.commands` with low-cardinality operation tags. Tenant identifiers, quota values, manifests, secrets, and passwords are never telemetry tags. Alert on elevated quota denials and administrative adjustment errors.

## Privacy guarantees

- `GET /api/v1/me/tenant` and `/quotas` always return the **caller's** tenant data; the admin-only routes are isolated under `Administrator`-role gates.
- Export payloads include only the caller's purchases; cross-tenant data is structurally impossible.
- Audit entries store only reasons, never sensitive payloads, ensuring leak risk stays minimal even when an admin role is compromised.
