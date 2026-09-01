# Creator and admin operations

Creators read their own sales ledger in a single dashboard while administrators operate the platform via audited endpoints. Both surfaces are server-authoritative: the Flutter client only renders what the ASP.NET Core API returns.

## Creator dashboard

- `GET /api/v1/creator/dashboard/overview` returns guide counts, paid/refunded order counts, and revenue per currency. The API derives these totals from `GuideOrder` rows that belong to guides owned by the caller. Non-creator callers receive `403`; the response never includes other creators' data.
- `GET /api/v1/creator/dashboard/orders?limit=N` returns the most recent orders for the caller's guides. Pagination is bounded by `N` (clamped between `1` and `200`).
- `GET /api/v1/creator/dashboard/reviews` aggregates visible, flagged, hidden, and open-report counts for the caller's reviews.

## Administrator operations

- `GET /api/v1/admin/operations/audit?limit=N` lists recent `IdentityAuditEntry` rows newest-first. Only the `Administrator` role passes the policy; everything else returns `403`.
- `GET /api/v1/admin/operations/users?limit=N` exposes user accounts with email, status, and confirmation flag. Password hashes and other security fields are never returned.
- `GET /api/v1/admin/operations/creators?limit=N` lists every `CreatorProfile` with slug and status. `Biography`, `TravelCountries`, and other private fields stay private.

## Operations

The `Trippify.Operations` meter emits `trippify.operations.commands` with low-cardinality operation tags (`creator-overview`, `creator-orders-listed`, `creator-review-summary`, `admin-audit-listed`, `admin-users-listed`, `admin-creators-listed`). Bodies, emails, bios, and reasons are never logged. Alert on elevated `403` (someone probing dashboards) and `500` (data layer regression).

## Privacy guarantees

- Creator revenue is server-derived from the caller's guides; cross-creator leakage is rejected by ownership checks before projection.
- Admin endpoints are role-gated to `Administrator`; non-admins receive `403` before any data is materialized.
- Sensitive creator-only fields (biography, biography, travel countries, avatar URLs) are intentionally absent from `AdminCreatorRow` to keep private profile data out of the operator console.
- All list endpoints accept a `limit` parameter that is clamped between `1` and `200` to protect against accidentally unbounded scans.

## Provider configuration

- `Map` and `ObjectStorage` options bind from `IConfiguration` at request scope, so per-environment overrides from `appsettings`, environment variables (e.g. `TRIPPIFY_OBJECT_STORAGE_SECRET`), or `dotnet user-secrets` flow through unchanged. The host does not bake a single signing secret into a deployable image.
- The `/health/ready` endpoint includes `map-provider` and `object-storage` checks. Provider failures surface as `Degraded` or `Unhealthy` so operators can alert before user-visible outages.
- The `object-storage` probe writes a one-byte sentinel under `_healthchecks/{guid}`, deletes the **same** key, and reports `Unhealthy` if the cleanup throws so operators can detect leaked probe artifacts and provider authorization drift. Probe failures are tagged with `data.provider` and `data.cleanupError` (exception type name only) for monitoring.
- The `map-provider` probe geocodes the stable known query `Tokyo` and reports `Degraded` (not `Healthy`) when the provider returns an unresolved status. Diagnostics expose `data.provider`, `data.status`, and `data.attribution`; the `Map:ApiKey`, signed URL secrets, and any other credentials are never included.
- All provider probes honor the readiness cancellation token and rely on the configured adapter-level HTTP timeouts (`ObjectStorage:TimeoutMilliseconds`, `Map:TimeoutMilliseconds`) for bounded execution.
- Diagnostic logs never record provider API keys, signed-URL secrets, or signed URLs in full. The startup probe writes the secret *length* (not the value) when initializing `LocalFileObjectStorage`, and telemetry counters expose only low-cardinality operation tags.
- Map provider attribution strings (e.g. `Map:Attribution`, `Local geocoder`, `Mapbox`) are persisted with every cached and resolved coordinate and surfaced on the public guide detail. Operators must keep these accurate and license-compatible; Trippify never fabricates coordinates when the provider returns no match.

## Runtime security policy

The API enforces a deny-by-default CORS posture and rejects weak runtime secrets at startup. Both checks run inside `RuntimeSecurityValidator.Validate` before the host accepts traffic, so a misconfigured production deploy fails fast with a non-secret diagnostic.

### Cross-origin requests

- `Cors:AllowedOrigins` is the authoritative allowlist. Each value MUST be an exact `scheme://host[:port]` URL — wildcards, missing schemes, and trailing slashes are rejected.
- Origins are normalized to lowercase scheme and host with the explicit port before being handed to ASP.NET Core CORS, so `HTTPS://App.Example/` and `https://app.example:443` are treated as the same entry.
- When the list is empty:
  - `Staging`/`Production` startup fails with `Cors:AllowedOrigins must list at least one exact origin`.
  - `Development` falls back to a documented non-credentialed open policy (any origin, no `Access-Control-Allow-Credentials`). Local Flutter web dev still works because the bearer token is an explicit `Authorization` header, not a credentialed cookie.
- When the list is non-empty, the API registers a credentialed policy that uses `WithOrigins(...)` and `AllowCredentials()`. The wildcard fallback `SetIsOriginAllowed(_ => true)` is never combined with `AllowCredentials()`, so unconfigured origins cannot be tricked into a credentialed preflight.

### Runtime secrets

Production deployments MUST inject every signing secret from a secret store; placeholder values in `docker-compose.yml` are rejected at startup.

| Setting | Purpose | Production rule |
| --- | --- | --- |
| `ObjectStorage:SignedUrlSecret` | HMAC key for read URLs and download links. | Rejected if empty, shorter than 24 characters, or equal to the documented `change-me-to-a-long-random-secret` placeholder. |
| `Payment:ApiKey` | Bearer credential on outbound checkout calls. | Rejected if empty, shorter than 24 characters, or contains a known-weak token such as `secret`, `password`, or `test-key`. |
| `Payment:WebhookSecret` | HMAC secret used to verify provider webhooks. | Same rules as `Payment:ApiKey`. Only enforced when `Payment:Provider=http` and `Payment:Enabled=true`. |
| `Ai:ApiKey` | Bearer credential on outbound AI calls. | Same rules as `Payment:ApiKey`. Only enforced when `Ai:Provider=http` and `Ai:Enabled=true`. |
| `Plugins:SigningSecret` | HMAC secret for plugin manifests registered via `POST /api/v1/admin/plugins`. | Rejected if empty, shorter than 24 characters, or equal to the development default `trippify-dev-shared-hmac-secret`. |

`RuntimeSecurityValidator.EnsureStrongSecret` also rejects single-character runs (e.g. `aaaa...`) so accidental placeholder shapes cannot pass. Health endpoints (`/health/live`, `/health/ready`), OpenAPI, and structured logs never echo the secret values; only their configured presence is visible to operators.

### Local development

- `appsettings.Development.json` seeds a non-credentialed allowlist of common local Flutter dev ports (`http://localhost:3000`, `http://localhost:5000`, `http://localhost:8080`, plus the same set on `127.0.0.1`).
- The development default for `Plugins:SigningSecret` and `ObjectStorage:SignedUrlSecret` is accepted in `Development` only. Setting `ASPNETCORE_ENVIRONMENT=Production` (or `Staging`) without overriding these values causes startup to fail.

## Release quality gates

The repository must satisfy these gates before a release is considered green.

### Rendered Flutter workflows

- The Flutter `test` target exercises the rendered widget tree through the standard VM binding and covers anonymous, traveler, creator, administrator, and responsive layout journeys. See `apps/trippify_flutter/test/release_smoke_test.dart` for the dedicated smoke tests.
- The supported Flutter web/browser runner is `flutter test --platform chrome`. The Flutter app currently lacks a real Chrome-compatible implementation of `flutter_secure_storage` and `file_picker`, so a headless Chrome run is not yet wired into CI. The same widget assertions are executed by the VM binding; the Chrome runner is a known environment limitation that will be unblocked when the web platform implementations land.
- The dedicated rendered smoke tests verify, end-to-end, that the anonymous surface hides protected entries, the traveler sees the workspace summary, the creator sees the Creator dashboard tile, the administrator sees the Admin operations tile, and the layout does not overflow at compact width.

### Skip accountability

- `apps/trippify_flutter/tool/check_flutter_skips.dart` walks every Dart test file in the `test/` directory and validates that every `skip: true` marker carries a `// allowed-skip: <id>` annotation that is registered in `tool/skip_allowlist.json`.
- The checker fails the build when:
  - a skip lacks an `// allowed-skip:` annotation,
  - a skip id is missing from the allowlist,
  - the anchored file:line in the allowlist no longer matches the current skip,
  - the count of skips exceeds the documented budget (currently `24`).
- The companion `test/skip_policy_test.dart` exercises the unapproved-skip, unknown-id, and budget-exceeded paths so the policy logic itself is regression-protected.
- New skips require a new allowlist entry. Removed skips require removing the matching entry. Renaming or relocating a test file requires updating the allowlist's `path`/`line` anchor.

### PostgreSQL migration drill

- `tests/Trippify.ApiTests/PostgresMigrationUpgradeTests.cs` proves the EF Core migration chain is non-empty, has no duplicate keys, is orderable, and that the migrator emits a script referencing the documented identity and commerce tables.
- `scripts/postgres-upgrade-drill.sh` runs the full disposable drill: it starts a PostGIS container, applies all migrations to an empty database, downgrades to a previous release anchor, upgrades to head, and hits `/api/v1/system/info` to prove the upgraded schema still serves traffic. The script is invoked from CI in `postgres-upgrade-drill`; when Docker or PostgreSQL is unavailable the script exits `0` and records the limitation while the static checks remain authoritative.

### Provider outage coverage

- `tests/Trippify.ApiTests/ProviderOutageApiTests.cs` replaces each provider with a throwing implementation and asserts the API fails safely:
  - object-storage outage returns `5xx` for `POST /api/v1/guides/{id}/media` without a fabricated success payload;
  - map-provider outage records the geocode status as `Unresolved` and never fabricates coordinates;
  - evidence-scanner outage dead-letters the background scan job and keeps the attachment out of the `Ready` state;
  - email outage does not block account creation.
- `tests/Trippify.ApiTests/RestorableBackupTests.cs` exercises the backup round-trip, deletes the artifact on disk to confirm the restore fails safely, tampers with the bytes to confirm the checksum check rejects, exercises encrypted-snapshot key validation, and rejects schema-version mismatches before mutating the live database.

### CI integration

- `.github/workflows/quality.yml` runs the dotnet, Flutter, and PostgreSQL upgrade-drill jobs on every push and pull request.
- The Flutter job runs the skip checker before `flutter analyze` and `flutter test` so an unapproved skip fails the build before any test work begins.

