# Tasks

## 1. Domain model and persistence

- [x] 1.1 Reuse existing `GuideOrder`, `CommerceLedgerEntry`, `PurchaseEntitlement`, `GuideReview`, `ReviewReport`, and `IdentityAuditEntry` entities (no schema change). Surface aggregations via DTOs only.
- [x] 1.2 Add `src/Trippify.Infrastructure/Operations.cs` with DTO records (`RevenueTotals`, `RevenueByCurrency`, `ReviewModerationSummary`, `OrderAuditTrailEntry`).

## 2. ASP.NET Core API surface

- [x] 2.1 Create `src/Trippify.Api/OperationsEndpoints.cs` mapping:
  - `GET /api/v1/creator/dashboard/overview`
  - `GET /api/v1/creator/dashboard/orders`
  - `GET /api/v1/creator/dashboard/reviews`
  - `GET /api/v1/admin/operations/audit`
  - `GET /api/v1/admin/operations/users`
  - `GET /api/v1/admin/operations/creators`
- [x] 2.2 Wire `MapOperations()` in `Program.cs` after `MapVerifiedTrips()`.
- [x] 2.3 Enforce ownership and Administrator role; ensure creator revenue numbers are derived from orders ledgers and never expose others' data; admin endpoints never leak creator private profile fields.
- [x] 2.4 Emit `Trippify.Operations` meter counters with low-cardinality operation tags.

## 3. Flutter client experience

- [x] 3.1 Add typed models (`CreatorDashboard`, `CreatorOrder`, `CreatorReviewSummary`, `AdminAuditEntry`, `AdminUserRow`, `AdminCreatorRow`) to `apps/trippify_flutter/lib/api_client.dart`.
- [x] 3.2 Add `getCreatorDashboardOverview`, `listCreatorOrders`, `getCreatorDashboardReviews`, `listAdminAudit`, `listAdminUsers`, `listAdminCreators` API methods.
- [x] 3.3 Build `_CreatorDashboardScreen` (overview, orders, reviews) accessible from `CreatorWorkspaceScreen` via a new button.
- [x] 3.4 Build `_AdminOperationsScreen` (audit log, users, creators) reachable via the system menu and gated on `Administrator` role.
- [x] 3.5 Cover loading/empty/success/error/denied states and accessibility (`Semantics`) in `apps/trippify_flutter/test/widget_test.dart`.

## 4. Concurrency, idempotency, privacy, retention, upgrades

- [x] 4.1 Tests in `tests/Trippify.ApiTests/OperationsApiTests.cs` covering:
  - creator receives only own ledger totals
  - related creator cannot read another creator's dashboard
  - admin endpoints deny non-admin role
  - audit log returns 200 only for Administrator
  - refunds reduce revenue totals correctly
- [x] 4.2 Verify that ordering endpoints paginate deterministically (limit, before-id cursor) and reject negative limits.

## 5. OpenAPI, telemetry, documentation, quality gates

- [x] 5.1 Confirm `swashbuckle` documents the new endpoint contracts.
- [x] 5.2 Document `Trippify.Operations` meter alerts (elevated 403, no PII in telemetry).
- [x] 5.3 Author `docs/operations.md` and link from `README.md`.
- [x] 5.4 Run `dotnet build`, `dotnet test`, `flutter analyze`, and `flutter test`.
