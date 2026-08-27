# Tasks

## 1. Domain model and persistence

- [x] 1.1 Add `Plugin`, `PluginInstallation`, `PluginPermissionGrant`, and `PluginAuditEntry` entities with EF configurations in `src/Trippify.Infrastructure/Plugins.cs`.
- [x] 1.2 Register the new `DbSet<>` properties on `AppDbContext`.
- [x] 1.3 Add forward-only EF Core migration `AddIntegrationPluginSystem` with unique indexes on plugin slug+version and installation (PluginId, UserId).

## 2. ASP.NET Core API surface

- [x] 2.1 Create `src/Trippify.Api/PluginsEndpoints.cs` mapping:
  - `POST /api/v1/admin/plugins` (register manifest, administrator role)
  - `GET /api/v1/plugins` (public catalog, Approved only)
  - `GET /api/v1/plugins/{pluginId}` (public details)
  - `POST /api/v1/me/plugins/{pluginId}/install` (caller)
  - `POST /api/v1/me/plugins/{pluginId}/enable`
  - `POST /api/v1/me/plugins/{pluginId}/disable`
  - `DELETE /api/v1/me/plugins/{pluginId}` (uninstall)
  - `GET /api/v1/me/plugins/installations`
  - `GET /api/v1/admin/plugins/audit`
- [x] 2.2 Wire `MapPlugins()` in `Program.cs` after `MapVersioning()`.
- [x] 2.3 Implement HMAC-SHA256 signature verification (`src/Trippify.Api/PluginSignature.cs`) before persisting a manifest.
- [x] 2.4 Emit `Trippify.Plugins` meter counters with low-cardinality `operation` tags.

## 3. Flutter client experience

- [x] 3.1 Add typed models (`PluginSummary`, `PluginInstallation`, `PluginPermission`, `PluginAuditEntry`) to `apps/trippify_flutter/lib/api_client.dart`.
- [x] 3.2 Add `listPlugins`, `getPlugin`, `installPlugin`, `enablePlugin`, `disablePlugin`, `uninstallPlugin`, `listMyInstallations`, `listAdminPluginAudit` API methods.
- [x] 3.3 Build `_PluginCatalogScreen` (public catalog) and `_MyPluginsScreen` (installations + lifecycle actions).
- [x] 3.4 Cover loading/empty/error states with accessibility (`Semantics`) in `apps/trippify_flutter/test/widget_test.dart`.

## 4. Concurrency, idempotency, privacy, retention, upgrades

- [x] 4.1 Tests in `tests/Trippify.ApiTests/PluginsApiTests.cs` covering:
  - invalid signature rejects registration before secret issuance
  - unrelated user cannot install on behalf of another
  - lifecycle transitions are idempotent (enable a second time → 200, no double audit row)
  - audit log excludes manifest secrets
- [x] 4.2 Verify rate-limiter envelopes plugin install/uninstall paths via existing limiter.

## 5. OpenAPI, telemetry, documentation, quality gates

- [x] 5.1 Confirm `swashbuckle` documents the new endpoint contracts.
- [x] 5.2 Document `Trippify.Plugins` meter alerts (elevated 403, signature failures, no PII in telemetry).
- [x] 5.3 Author `docs/plugins.md` and link from `README.md`.
- [x] 5.4 Run `dotnet build`, `dotnet test`, `flutter analyze`, and `flutter test`.
