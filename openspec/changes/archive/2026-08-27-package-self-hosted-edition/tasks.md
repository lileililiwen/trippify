# Tasks

## 1. Domain model and persistence

- [x] 1.1 Add `FeatureFlag` and `BackupSnapshot` entities with EF configurations in `src/Trippify.Infrastructure/SelfHosted.cs`.
- [x] 1.2 Register the new `DbSet<>` properties on `AppDbContext`.
- [x] 1.3 Add forward-only EF Core migration `AddSelfHostedDistribution` with unique indexes.

## 2. ASP.NET Core API surface

- [x] 2.1 Create `src/Trippify.Api/SelfHostedEndpoints.cs` mapping:
  - `GET /api/v1/system/info` (public, exposes version + applied migration count)
  - `GET /api/v1/admin/system/status` (admin)
  - `POST /api/v1/admin/system/upgrade` (admin, applies pending migrations)
  - `POST /api/v1/admin/system/backup` (admin, returns JSON dump)
  - `POST /api/v1/admin/system/restore` (admin, accepts JSON dump)
  - `GET /api/v1/admin/feature-flags`
  - `PUT /api/v1/admin/feature-flags/{key}` { enabled, value }
- [x] 2.2 Wire `MapSelfHosted()` in `Program.cs` after `MapCommercialRemixes()`.
- [x] 2.3 Use `DbContext.Database.MigrateAsync` to apply pending migrations; capture errors as a `skipped` status when the provider is non-relational.
- [x] 2.4 Emit `Trippify.SelfHosted` meter counters with low-cardinality `operation` tags.
- [x] 2.5 Add `Dockerfile` and update `docker-compose.yml` to expose the new env vars and the upgrade/backup endpoints.

## 3. Flutter client experience

- [x] 3.1 Add typed models (`SystemInfo` aliased to `SystemDistributionInfo`, `SystemStatus`, `BackupSnapshot`, `FeatureFlag`) to `apps/trippify_flutter/lib/api_client.dart`.
- [x] 3.2 Add `getSystemInfo`, `getSystemStatus`, `triggerUpgrade`, `triggerBackup`, `restoreFromBackup`, `listFeatureFlags`, `upsertFeatureFlag` methods.
- [x] 3.3 Build `_SystemStatusScreen` accessible from the system menu showing version, migrations, and feature flags with toggle.
- [x] 3.4 Cover loading/empty/error states and accessibility (`Semantics`) in `apps/trippify_flutter/test/widget_test.dart`.

## 4. Concurrency, idempotency, privacy, retention, upgrades

- [x] 4.1 Tests in `tests/Trippify.ApiTests/SelfHostedApiTests.cs` covering:
  - upgrade is idempotent after migrations are already applied
  - non-admin caller cannot trigger upgrades or backups
  - feature flag toggle persists across calls and is restricted to admins
  - restore without the admin role is rejected
- [x] 4.2 Verify that the public system info endpoint never exposes secrets, only the version.

## 5. OpenAPI, telemetry, documentation, quality gates

- [x] 5.1 Confirm `swashbuckle` documents the new endpoint contracts.
- [x] 5.2 Document `Trippify.SelfHosted` meter alerts (upgrade failures, no PII in telemetry).
- [x] 5.3 Author `docs/self-hosted.md` and link from `README.md`.
- [x] 5.4 Run `dotnet build`, `dotnet test`, `flutter analyze`, and `flutter test`.
- [x] 5.5 Validate the `Dockerfile` parses by visually reviewing it (docker build deferred to deployment).
