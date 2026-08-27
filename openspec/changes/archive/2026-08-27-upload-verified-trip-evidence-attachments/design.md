# Context
Evidence currently accepts text and an optional redacted reference; Flutter's attachment picker is a placeholder.

# Goals / Non-goals
- Allow useful proof files without making sensitive evidence public.
- Couple object deletion to evidence retention and moderation.

# Decisions
- Upload through an authenticated staged-object flow, then attach by opaque ID in the evidence transaction.
- Allow a documented image/PDF set with server-verified MIME, signature, size, checksum, and per-submission count limits.
- Hold new files in a private quarantined state until asynchronous malware scanning completes.
- Permit only submitter and authorized administrators to receive short-lived download URLs.

# Authorization, privacy, and failure modes
- Guide creators, public users, and unrelated purchasers cannot read attachment metadata or bytes.
- Rejected, deleted, expired, unattached, or scan-failed objects are deleted by idempotent jobs.

# Migration and rollback
- Add attachment metadata and evidence linkage forward-only; disabling uploads preserves existing private attachments through retention.
