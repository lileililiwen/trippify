# Tasks

## 1. Providers and persistence
- [x] 1.1 Implement configured map and S3-compatible storage adapters plus functional local/test adapters.
- [x] 1.2 Add typed startup validation, timeouts, retries, cancellation, health checks, and secret-safe diagnostics.
- [x] 1.3 Add forward-only media/checksum/geocode metadata and indexes.

## 2. API and Flutter
- [x] 2.1 Enforce ownership, content type, size, checksum, visibility, object-key, and quota rules for uploads.
- [x] 2.2 Geocode route locations server-side and return coordinates with attribution and explicit unresolved state.
- [x] 2.3 Replace static Flutter map/media placeholders with loading, success, unavailable, denied, offline, and retry states.

## 3. Verification
- [x] 3.1 Test byte persistence/retrieval, private URL expiry, cross-user denial, oversized/mismatched media, duplicate upload, and provider outage.
- [x] 3.2 Test geocode cache, unresolved address, timeout, provider attribution, and absence of client secrets.
- [x] 3.3 Update deployment/privacy documentation and run backend, migration, and Flutter gates.
