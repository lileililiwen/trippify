# Context
`GeocodeAsync` returns `null`; `PutAsync` returns a file URI without writing the supplied stream.

# Goals / Non-goals
- Persist media and obtain useful coordinates without exposing provider keys.
- Make disabled/outage states explicit rather than pretending success.

# Decisions
- Configure independent map and S3-compatible storage adapters with typed validation and local test implementations that actually persist test bytes.
- Validate media type, size, checksum, owner, visibility, and object key server-side; issue short-lived access URLs for private objects.
- Cache normalized geocoding results with provider attribution and bounded retention.
- Reserve tenant quota before upload and release it on failed persistence.

# Authorization, privacy, and failure modes
- Private objects require an authorized projection; raw internal keys and credentials never reach public responses.
- Provider timeout returns an explicit retryable error and records no successful media/geocode state.

# Migration and rollback
- Add media/checksum/geocode metadata forward-only. Existing references are migrated or marked unavailable, never silently treated as verified objects.
