# Tasks

## 1. Domain model and persistence

- [x] 1.1 Add `GuideRelease` entity and EF configuration in `src/Trippify.Infrastructure/Versioning.cs`.
- [x] 1.2 Register the new `DbSet<>` on `AppDbContext`.
- [x] 1.3 Add forward-only EF Core migration `AddGuideReleases` with unique `(GuideId, VersionNumber)` index and `PublishedAt` lookup index.

## 2. ASP.NET Core API surface

- [x] 2.1 Create `src/Trippify.Api/VersioningEndpoints.cs` mapping:
  - `POST /api/v1/guides/{guideId}/releases` (creator only)
  - `GET /api/v1/guides/{guideId}/releases` (public, paginated)
  - `GET /api/v1/releases/{releaseId}` (public by id)
  - `GET /api/v1/guides/{guideId}/freshness` (public)
- [x] 2.2 Wire `MapVersioning()` in `Program.cs` after `MapNotifications()`.
- [x] 2.3 Auto-create an initial `GuideRelease` when a guide is published inside `PublishingEndpoints`.
- [x] 2.4 Emit `NewGuidePublished` notifications to buyers with valid `PurchaseEntitlement` via `NotificationFanOut`.

## 3. Flutter client experience

- [x] 3.1 Add typed models (`GuideRelease`, `FreshnessSignal`) to `apps/trippify_flutter/lib/api_client.dart`.
- [x] 3.2 Add `listGuideReleases`, `getGuideRelease`, `getGuideFreshness`, `publishGuideRelease` methods.
- [x] 3.3 Add a "Release history" section to `PublicGuideScreen` and a "Freshness" chip near the title (`apps/trippify_flutter/lib/main.dart`).
- [x] 3.4 Cover loading/empty/error states and accessibility in `apps/trippify_flutter/test/widget_test.dart`.

## 4. Concurrency, idempotency, privacy, retention, upgrades

- [x] 4.1 Tests in `tests/Trippify.ApiTests/VersioningApiTests.cs` covering:
  - creator publishes a release; prior release is immutable
  - buyers see release list and freshness
  - non-creator cannot publish
  - re-publishing the same content is idempotent (no duplicate release)
- [x] 4.2 Verify release payload doesn't leak creator private fields; freshness endpoint is rate-limited via existing middleware.

## 5. OpenAPI, telemetry, documentation, quality gates

- [x] 5.1 Confirm `swashbuckle` documents the new endpoint contracts.
- [x] 5.2 Emit `Trippify.Versioning` meter counters with low-cardinality `operation` tags.
- [x] 5.3 Author `docs/versioning.md` and link from `README.md`.
- [x] 5.4 Run `dotnet build`, `dotnet test`, `flutter analyze`, and `flutter test`.
