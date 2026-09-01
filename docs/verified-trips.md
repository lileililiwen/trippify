# Verified trip evidence

Buyers who actually travelled with a guide can opt in to a privacy-preserving proof signal. The badge aggregates evidence without exposing bodies; only authorized reviewers and the submitter ever see raw evidence, attachments, and the short-lived download URLs that gate them, and opt-in coarse metrics surface only after at least five travelers participate.

## Evidence lifecycle

- `POST /api/v1/guides/{guideId}/evidence { kind, body, redactedReference?, attachmentIds? }` accepts one evidence submission per user per guide. Bodies must contain 50–4000 characters, and the redacted reference (if supplied) is capped at 200 characters. Only paid-entitlement holders or free-guide readers may submit.
- Each submission gets a 90-day `RetentionDeadline`. Background cleanup deletes the evidence, its attachments, and decrements the badge count once the deadline passes.
- `POST /api/v1/admin/evidence/{evidenceId}/review { decision, reason }` lets administrators approve or reject evidence. Decisions are restricted to `Approved` or `Rejected` and reasons are capped at 500 characters. Reviewers may not rule on their own evidence, and the guide creator is forbidden from reviewing their own guide (` (`403`).
- `DELETE /api/v1/admin/evidence/{evidenceId}` enforces retention: `DeletedAt` is set, linked attachments are deleted from object storage, and, when an `Approved` badge existed, the count is decremented (badge revokes once it hits zero).

## Attachment lifecycle

- `POST /api/v1/evidence/attachments { fileName, contentType, sizeBytes, sha256 }` stages a private, owner-scoped attachment in `evidence_attachments` with a 24-hour expiry and a server-issued `storageKey`. The submitter must be authenticated; the response never exposes other users' metadata.
- Allowed content types are `image/jpeg`, `image/png`, `image/webp`, and `application/pdf`. Per submission travelers may attach up to **five** files; the per-user staging quota is **20 files per 24 hours** and **25 MB of active bytes**. `sha256` must be exactly 64 hexadecimal characters.
- `PUT /api/v1/evidence/attachments/{attachmentId}/content` accepts `application/octet-stream`. The server re-validates the MIME magic head plus the `WEBP` trailer, recomputes the SHA-256 checksum against the staged value, scans for unsafe signatures (`MZ`, `ELF`, encrypted ZIP, embedded `<script>`), enforces the staged size, and persists the bytes via `IObjectStorage`. Disguised, oversized, mismatched, unsafe, or expired uploads are rejected and the staged row is marked `Rejected` with `DeletedAt` set so cleanup can purge it.
- A successful upload enqueues an `evidence-attachment-scan` background job. Once the job transitions the row to `Ready`, the attachment can be linked into a pending evidence submission via `POST /api/v1/guides/{guideId}/evidence/{evidenceId}/attachments { attachmentIds }` (or by including `attachmentIds` on the initial submission). Only attachments owned by the submitter, in state `Ready`, not yet linked, and not deleted, are accepted. Up to five attachments may be linked per evidence submission.
- `GET /api/v1/evidence/{evidenceId}/attachments` returns the submitter's view of attachments (id, filename, content type, size, state, scan failure code, created/linked timestamps).
- `DELETE /api/v1/evidence/attachments/{attachmentId}` removes the submitter's own attachment; if it was linked to a pending evidence submission the link is severed and the bytes are purged from object storage.
- `GET /api/v1/evidence/attachments/{attachmentId}/download` returns a signed, **10-minute** download URL plus its `expiresAt`. The endpoint refuses any attachment whose state is not `Ready` or whose parent evidence is deleted. Signed URLs are produced by the configured `ObjectStorage` adapter; production deployments configure an S3-compatible remote store with HMAC-signed URLs, and `local` mode writes bytes to `ObjectStorage:LocalRoot` and signs with `ObjectStorage:SignedUrlSecret`. Provider keys and the signing secret are server-side only and never appear in responses.

## Reviewer access

- `GET /api/v1/admin/evidence/{evidenceId}/attachments` lists the attachments linked to an evidence submission for authorized administrators. The creator is forbidden (` (`403`).
- `GET /api/v1/admin/evidence/attachments/{attachmentId}/download` issues a **10-minute** signed download URL for the reviewer. The signed URL is not stored on the client and the reviewer surface never caches it past `expiresAt`.

## Retention, scan, and cleanup jobs

- The `evidence-attachment-scan` job is the **only** path that transitions an attachment out of `Scanning`. The job calls `IEvidenceScanner.ScanAsync` with the storage key, declared content type, SHA-256, and size. `Clean` is the only outcome that yields `Ready`; `Infected` and `Invalid` transition the attachment to `Rejected` with `scanFailureCode = "malware:{code}"` or `"invalid:{code}"`. `Unavailable` outcomes (and any scanner exception) raise `EvidenceScannerUnavailableException`, which the background-job pipeline treats as retryable; once the job exhausts `MaxAttempts` the attachment is marked `Rejected` with `scanFailureCode = "scanner-unavailable:{failureCode}"` and the job is dead-lettered.
- Scanner selection and policy live in `EvidenceScannerOptions`. The local adapter (`Provider = local`) is labeled `local-noop` and is explicitly disallowed by `EnsureEnvironmentPolicy("Production")`; production deploys must configure a real `http` provider (`EvidenceScanner:Endpoint`, `EvidenceScanner:ApiKey`, `EvidenceScanner:TimeoutMilliseconds`). `LocalEvidenceScanner` MUST NOT silently claim production-grade scanning — its only effect in development is to return `Clean` (or `Unavailable` when disabled).
- The `evidence-attachment-scan` job keeps the existing state-machine invariant: replay is a no-op because the handler re-checks `attachment.State == Scanning` before invoking the scanner and before persisting the outcome. There are no second transitions, no duplicate side effects, and no extra download access.
- Logging and operator diagnostics are sanitised. Logs include only the `ProviderName`, `FailureCode`, and attachment id; the storage key, declared content type, file name, and any payload bytes never reach `ILogger` calls. The `/api/v1/admin/background-jobs/evidence-scans` endpoint lists dead-lettered scan jobs with their `failureCode` and the affected attachment id+state — it never returns the job `Payload` or the storage key. The `trippify.evidence.scans` counter records `outcome` and `provider` tags for clean/infected/invalid transitions.
- The `evidence-attachment-cleanup` job runs at most every 15 minutes and idempotently deletes: attachments that are already `Rejected`, `Staged` attachments whose `ExpiresAt` has passed, `Scanning` attachments that have timed out, and `Ready` attachments that never linked to an evidence submission within 24 hours. Each deletion removes the object storage bytes (best-effort) and sets `DeletedAt`.
- The `evidence-retention` job marks expired evidence as deleted, marks every linked attachment as deleted, removes the underlying bytes from object storage, then reconciles the verified badge count without double-counting replayed runs.

## Badge lifecycle

- A `verified_guide_badges` row appears the first time any evidence is approved and refreshes `LastGrantedAt` on each subsequent approval. The unique index on `GuideId` keeps the badge singular.
- `DELETE /api/v1/admin/guides/{guideId}/badge` lets administrators revoke the badge explicitly (for example, when a creator unpublishes the guide). Revoked badges no longer satisfy public visitors who call the badge endpoint.
- `GET /api/v1/guides/{guideId}/evidence/badge` returns `{ verified, approvedEvidenceCount, firstGrantedAt, lastGrantedAt }` to the public. No evidence bodies or attachment identifiers are included.

## Coarse actual metrics

- `POST /api/v1/guides/{guideId}/insights { partySize, tripDays, totalCostMinorUnits, currencyCode }` records one opt-in submission per user per guide (`409` on duplicates). Creators cannot share insights about their own guides.
- `GET /api/v1/guides/{guideId}/insights` returns the public aggregate (`Median` and `Average` per dominant currency). Responses are withheld until at least 5 travelers opt in (`meetsKAnonymity = false`); the API never reveals precise low-count aggregates to protect privacy.

## Operations

The `Trippify.Verified` meter emits `trippify.verified.commands` with low-cardinality `operation` tags (`evidence-submitted`, `evidence-attachments-linked`, `evidence-attachment-removed`, `evidence-attachment-downloaded`, `evidence-attachment-downloaded-reviewer`, `evidence-reviewed`, `evidence-deleted`, `badge-revoked`, `insight-submitted`) and `trippify.verified.attachments` with `attachment-staged` and `attachment-content-uploaded`. The `Trippify.BackgroundJobs` meter emits `trippify.evidence.scans` with `outcome` and `provider` tags (`Clean`, `Infected`, `Invalid`, `Unavailable`). Bodies, redacted references, attachment URLs, scan payloads, and reasons are not part of the telemetry. Alert on elevated 409 (duplicate evidence/insights), elevated 400 (disguised/oversized/mismatched attachments), 403 (unrelated access or creator self-review attempts), elevated `Invalid`/`Infected` scan outcomes, and any `DeadLetter` rows returned by `/api/v1/admin/background-jobs/evidence-scans`. Retention cleanup is a job separate from the API; it issues a delete using the existing `evidence-deleted` operation so dashboards stay consistent.

## Privacy guarantees

- Evidence bodies and attachment metadata (filename, content type, size, scan state, signed URLs) are accessible only to the submitter (via guide moderation scopes) and to the `Administrator` role. The guide creator, other creators, and unrelated travelers always see `403`/`404`.
- Public badge responses include only counts and timestamps; attachment identifiers, names, content types, sizes, scan states, and download URLs are never returned to anonymous callers.
- Insights responses are gated by k-anonymity (`>= 5` submissions). Until then the only response is `meetsKAnonymity = false` with `submissionCount`.
- Download URLs are short-lived (10 minutes) and the reviewer Flutter surface does not persist them past their `expiresAt`. Production object stores should refuse the URL once the underlying attachment is deleted or moved into a rejected state.
- Cascade deletes from `travel_guides`, `users`, and `purchase_entitlements` remove related evidence, attachments, and audit entries; verified badges ride along with their guides.