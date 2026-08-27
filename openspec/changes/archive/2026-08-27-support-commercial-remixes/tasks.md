# Tasks

## 1. Domain model and persistence

- [x] 1.1 Add `LicensePolicy`, `RemixAncestry`, `RemixApproval`, `RevenueShare` entities with EF configurations in `src/Trippify.Infrastructure/CommercialRemixes.cs`.
- [x] 1.2 Register the new `DbSet<>` properties on `AppDbContext`.
- [x] 1.3 Add forward-only EF Core migration `AddCommercialRemixes` with unique indexes and FK cascades.

## 2. ASP.NET Core API surface

- [x] 2.1 Create `src/Trippify.Api/CommercialRemixesEndpoints.cs` mapping:
  - `POST /api/v1/me/license-policies`
  - `GET /api/v1/me/license-policies`
  - `GET /api/v1/creators/{slug}/license` (public)
  - `POST /api/v1/guides/{guideId}/remix/ancestry`
  - `GET /api/v1/guides/{guideId}/ancestry` (public)
  - `POST /api/v1/admin/remix-approvals/{ancestryId}/decide` (admin or parent creator)
  - `GET /api/v1/admin/remix-approvals/queue`
  - `POST /api/v1/admin/revenue-shares` (record)
  - `GET /api/v1/admin/revenue-shares?orderId=`
- [x] 2.2 Wire `MapCommercialRemixes()` in `Program.cs` after `MapAssistedImport()`.
- [x] 2.3 Implement share-summing helper that returns total; reject `400` when shares do not total 100% or currency mismatch.
- [x] 2.4 Emit `Trippify.CommercialRemixes` meter counters with low-cardinality `operation` tags.

## 3. Flutter client experience

- [x] 3.1 Add typed models (`LicensePolicy`, `RemixAncestry`, `RemixApproval`, `RevenueShare`) to `apps/trippify_flutter/lib/api_client.dart`.
- [x] 3.2 Add `upsertMyLicensePolicy`, `getMyLicensePolicy`, `getCreatorLicense`, `declareRemixAncestry`, `getGuideAncestry`, `decideRemixApproval`, `listRemixApprovalQueue`, `recordRevenueShares`, `listRevenueShares` methods.
- [x] 3.3 Build `_LicensePanelScreen` accessible from public creator page.
- [x] 3.4 Cover loading/empty/error states and accessibility (`Semantics`) in `apps/trippify_flutter/test/widget_test.dart`.

## 4. Concurrency, idempotency, privacy, retention, upgrades

- [x] 4.1 Tests in `tests/Trippify.ApiTests/CommercialRemixesApiTests.cs` covering:
  - revenue shares for an order sum to the order total
  - duplicates on the same order yield the same shares
  - non-owners cannot declare ancestry on someone else's guide
  - approve action flips once and remains idempotent
- [x] 4.2 Verify license policy visibility is restricted to public-safe fields.

## 5. OpenAPI, telemetry, documentation, quality gates

- [x] 5.1 Confirm `swashbuckle` documents the new endpoint contracts.
- [x] 5.2 Document `Trippify.CommercialRemixes` meter alerts (sum mismatch, no PII in telemetry).
- [x] 5.3 Author `docs/commercial-remixes.md` and link from `README.md`.
- [x] 5.4 Run `dotnet build`, `dotnet test`, `flutter analyze`, and `flutter test`.
