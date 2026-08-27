# Self-hosted distribution

Third-party operators need a reproducible deployment, hand-fed upgrades, and auditable backups. This slice adds an admin system console (status, upgrade, backup, restore), public system info, feature flags, a multi-stage `Dockerfile`, and an updated `docker-compose.yml`.

## Endpoints

- `GET /api/v1/system/info` (public) returns the API version. **No secrets, no admin-only fields** are exposed.
- `GET /api/v1/admin/system/status` returns the version, applied migration count, pending migration list. Admin-only.
- `POST /api/v1/admin/system/upgrade` calls `DbContext.Database.MigrateAsync()` to apply pending migrations. Returns `{ "status": "applied", "appliedAt": "..." }`. When the provider doesn't support migrations (for example the in-memory database used by tests), the endpoint short-circuits with `{ "status": "skipped", "reason": "..." }` instead of `502`.
- `POST /api/v1/admin/system/backup { label? }` snapshots a JSON payload of small reference tables (users, guide counts, feature flags) plus a captured timestamp. The record is persisted in `BackupSnapshots` for audit.
- `POST /api/v1/admin/system/restore { payload }` records the restore payload alongside the actor and a generated label; downstream tooling reads the row to apply migrations or restore data.
- `GET /api/v1/admin/feature-flags` returns every flag with `Enabled`, `Value`, and `UpdatedAt`.
- `PUT /api/v1/admin/feature-flags/{key} { enabled, value }` upserts the flag. Duplicate keys reuse the existing row to keep audit history continuous.

## Containers

- `src/Trippify.Api/Dockerfile` is a multi-stage build (sdk 8.0 → aspnet 8.0) that publishes the API into a runtime image and exposes port `8080`.
- `docker-compose.yml` now defines both the PostgreSQL service and the API container. The API container waits for `postgres` to be healthy, then runs migrations on startup (`SelfHosted__RunMigrationsOnStartup=true`).

## Operations

The `Trippify.SelfHosted` meter emits `trippify.selfhosted.commands` with low-cardinality `operation` tags (`upgrade-applied`, `upgrade-skipped`, `backup-recorded`, `restore-recorded`, `feature-flag-upserted`). Bodies, secrets, and operator-supplied payloads are never part of the telemetry. Alert on persistent `upgrade-failed` outcomes or repeated `feature-flag-upserted` against the same key.

## Privacy guarantees

- Public `system/info` exposes only the version — never `connection strings`, secrets, or feature flag values.
- All admin endpoints require the `Administrator` role; non-admins receive `403`.
- Backup snapshots never contain credentials; only metadata (counts) and feature flag state are returned, so the JSON stays small and reviewable.
- Restore accepts a `payload` from the operator but **never** applies it inline — the entry is recorded so a separate restore worker can apply it offline.
