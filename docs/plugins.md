# Integration plugin system

The marketplace must accept extensions without giving them the keys to the kingdom. This slice adds versioned plugin manifests, signed registration, scoped permissions, lifecycle transitions, a public catalog, and an audited admin console, all gated by the existing rate limiter and role checks.

## Manifests and signatures

- `POST /api/v1/admin/plugins { slug, displayName, version, publisher, manifest, signature }` registers a manifest. The administrator role is required and the request carries an HMAC-SHA256 signature (`sha256=<hex>`) computed with the configured shared secret. **The endpoint rejects the request before persisting anything** when the signature fails verification (`400`).
- Each `(slug, version)` pair is unique; duplicate manifests return `409 Conflict`.

## Catalog

- `GET /api/v1/plugins?limit=N` returns the public catalog (only `Approved` plugins). `limit` is clamped between `1` and `200`.
- `GET /api/v1/plugins/{pluginId}` returns the manifest summary by id.

## Installation and lifecycle

- `POST /api/v1/me/plugins/{pluginId}/install { scopes }` records a `PluginInstallation` for the caller and snapshots the requested `PluginPermissionScope` values into `PluginPermissionGrant` rows. The endpoint is idempotent: a second install request returns the existing installation rather than creating duplicates.
- `POST /api/v1/me/plugins/{pluginId}/enable` and `/disable` flip the installation between `Installed`, `Enabled`, and `Disabled`. Repeated calls are idempotent (a second enable returns the same `204 No Content` without a new audit row).
- `DELETE /api/v1/me/plugins/{pluginId}` uninstalls the plugin, marks the lifecycle `Disabled`, sets `uninstalledAt`, and revokes every scope.
- `GET /api/v1/me/plugins/installations` returns the caller's installations with their current lifecycle and granted scopes.

## Audit

- `GET /api/v1/admin/plugins/audit?limit=N` returns recent `PluginAuditEntry` rows with actor, action, and reason. The manifest body and signature are **never** exposed in audit responses.

## Operations

The `Trippify.Plugins` meter emits `trippify.plugins.commands` with low-cardinality `operation` tags (`plugin-registered`, `plugins-listed`, `plugin-installed`, `plugin-enabled`, `plugin-disabled`, `plugin-uninstalled`, `plugin-audit-listed`). Manifests, signatures, and secrets are never part of the telemetry. Alert on elevated signature-failure responses (someone probing registration) or unauthorized install attempts.

## Privacy guarantees

- Scopes travel only as enums; the manifest body is stored once and never echoed back through audit or listing endpoints.
- An installation can only be queried by its owner (no `/plugins/{pluginId}` shortcut exposes another user's installation).
- Audit entries are restricted to the `Administrator` role; non-admins receive `403` before any rows are materialized.
- All write endpoints reuse the existing `api` rate limiter, so a buggy installer cannot bypass the global limiter.
