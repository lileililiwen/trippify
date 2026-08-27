# Tasks

## 1. Attachment lifecycle
- [x] 1.1 Add private staged attachment entities, evidence links, scan/retention states, indexes, and a forward-only migration.
- [x] 1.2 Implement authenticated staged upload and atomic evidence attachment with server-side type, signature, size, checksum, count, ownership, and quota validation.
- [x] 1.3 Implement idempotent scan and deletion jobs for rejected, expired, unattached, deleted, and unsafe files.

## 2. Access and Flutter
- [x] 2.1 Return expiring download access only to the submitter and authorized administrators; exclude attachment details from public badge/insight projections.
- [x] 2.2 Replace the Flutter placeholder with accessible mobile/web file picking, progress, remove, validation, offline, retry, and scan states.
- [x] 2.3 Show safe reviewer previews/downloads without caching private URLs beyond expiry.

## 3. Verification
- [x] 3.1 Test anonymous, creator, unrelated buyer, submitter, and administrator access plus malformed, oversized, disguised, unsafe, duplicate, and expired files.
- [x] 3.2 Verify evidence deletion/retention removes bytes and replay does not corrupt badge counts.
- [x] 3.3 Update verified-trip/privacy documentation and run backend, migration, storage, and Flutter gates.
