# Context
Current backup rows contain counts and selected metadata; restore rows are never applied.

# Goals / Non-goals
- Produce recoverable artifacts and prove recovery in automation.
- Keep restore offline or maintenance-gated; do not overwrite a live database inline.

# Decisions
- Stream a versioned archive containing a consistent PostgreSQL dump, required object manifests/content, schema metadata, and checksums to configured backup storage.
- Encrypt artifacts with an operator-supplied key and never persist that key.
- Restore into an empty target after checksum, version, capacity, and key validation; use maintenance mode and explicit operator confirmation for replacement.
- Record audit metadata and status, not archive contents or secrets, in the application database.

# Authorization, privacy, and failure modes
- Only administrators may initiate backup or inspect status; actual restore requires operator-level tooling and filesystem/database authority.
- Partial backup/restore is marked failed and never reported as successful.

# Migration and rollback
- Add forward-only job/artifact metadata. Existing metadata snapshots remain readable but are labeled non-restorable.
