# Tasks

## 1. Domain model and persistence

- [x] 1.1 Add `Tenant`, `TenantMember`, `Subscription`, `QuotaUsage`, and `TenantAuditEntry` entities with EF configurations in `src/Trippify.Infrastructure/ManagedSaas.cs`.
- [x] 1.2 Register the new `DbSet<>` properties on `AppDbContext`.
- [x] 1.3 Add forward-only EF Core migration `AddManagedSaasHosting` with unique indexes and FK cascades.

## 2. ASP.NET Core API surface

- [x] 2.1 Create `src/Trippify.Api/ManagedSaasEndpoints.cs` mapping:
  - `POST /api/v1/admin/tenants`
  - `GET /api/v1/admin/tenants`
  - `GET /api/v1/admin/tenants/{tenantId}`
  - `PUT /api/v1/admin/tenants/{tenantId}` (branding, domain)
  - `POST /api/v1/admin/tenants/{tenantId}/suspend`
  - `POST /api/v1/admin/tenants/{tenantId}/quotas/{metric}` (set quota cap)
  - `GET /api/v1/admin/tenants/{tenantId}/audit`
  - `GET /api/v1/me/tenant`
  - `POST /api/v1/me/tenant/subscription` (plan change)
  - `GET /api/v1/me/tenant/quotas`
  - `POST /api/v1/me/tenant/export`
  - `POST /api/v1/me/tenant/deletion`
- [x] 2.2 Wire `MapManagedSaas()` in `Program.cs` after `MapPlugins()`.
- [x] 2.3 Implement quota enforcement helper that returns `403` once a metric exceeds its cap, only for the offending tenant.
- [x] 2.4 Emit `Trippify.ManagedSaas` meter counters with low-cardinality `operation` tags.

## 3. Flutter client experience

- [x] 3.1 Add typed models (`Tenant`, `Subscription`, `QuotaUsage`, `TenantAuditEntry`) to `apps/trippify_flutter/lib/api_client.dart`.
- [x] 3.2 Add `listTenants`, `getTenant`, `updateTenant`, `suspendTenant`, `getMyTenant`, `updateSubscription`, `listMyQuotas`, `requestExport`, `requestDeletion` API methods.
- [x] 3.3 Build `_TenantDashboardScreen` accessible from the system menu showing branding, plan, quotas, and export actions.
- [x] 3.4 Cover loading/empty/error states and accessibility (`Semantics`) in `apps/trippify_flutter/test/widget_test.dart`.

## 4. Concurrency, idempotency, privacy, retention, upgrades

- [x] 4.1 Tests in `tests/Trippify.ApiTests/ManagedSaasApiTests.cs` covering:
  - quota enforcement rejects only the exceeding tenant
  - non-admins cannot list tenants
  - export payload does not include other tenants' data
  - deletion request records an audit row with a reason
- [x] 4.2 Verify subscription upgrade is idempotent at the same plan (no duplicate audit).

## 5. OpenAPI, telemetry, documentation, quality gates

- [x] 5.1 Confirm `swashbuckle` documents the new endpoint contracts.
- [x] 5.2 Document `Trippify.ManagedSaas` meter alerts (elevated 403, quota exceeded, no PII in telemetry).
- [x] 5.3 Author `docs/managed-saas.md` and link from `README.md`.
- [x] 5.4 Run `dotnet build`, `dotnet test`, `flutter analyze`, and `flutter test`.
