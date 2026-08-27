# Tasks

## 1. Artifact pipeline
- [x] 1.1 Define versioned backup manifest, checksum, encryption, storage, status, and retention contracts.
- [x] 1.2 Implement consistent PostgreSQL and object-storage backup creation with bounded streaming and cancellation.
- [x] 1.3 Add forward-only artifact/job persistence and label legacy snapshots non-restorable.

## 2. Restore workflow
- [x] 2.1 Implement preflight validation and an operator CLI/offline worker that restores only into an empty or explicitly approved target.
- [x] 2.2 Add maintenance-state and progress reporting to the admin API and Flutter console.
- [x] 2.3 Preserve the original deployment when validation or restore fails.

## 3. Verification and operations
- [x] 3.1 Test admin/non-admin boundaries, corrupt archives, wrong keys, unsupported versions, cancellation, and insufficient storage.
- [x] 3.2 Run an automated backup-delete-restore drill and compare relational rows, constraints, and object checksums.
- [x] 3.3 Document key custody, retention, recovery procedure, and quality-gate evidence.
