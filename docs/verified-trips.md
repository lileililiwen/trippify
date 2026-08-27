# Verified trip evidence

Buyers who actually travelled with a guide can opt in to a privacy-preserving proof signal. The badge aggregates evidence without exposing bodies; only authorized reviewers and the submitter ever see raw evidence, and opt-in coarse metrics surface only after at least five travelers participate.

## Evidence lifecycle

- `POST /api/v1/guides/{guideId}/evidence { kind, body, redactedReference? }` accepts one evidence submission per user per guide. Bodies must contain 50–4000 characters, and the redacted reference (if supplied) is capped at 200 characters. Only paid-entitlement holders or free-guide readers may submit.
- Each submission gets a 90-day `RetentionDeadline`. Background cleanup deletes the evidence and decrements the badge count once the deadline passes.
- `POST /api/v1/admin/evidence/{evidenceId}/review { decision, reason }` lets administrators approve or reject evidence. Decisions are restricted to `Approved` or `Rejected` and reasons are capped at 500 characters. Reviewers may not rule on their own evidence, and the guide creator is forbidden from reviewing their own guide (`403`).
- `DELETE /api/v1/admin/evidence/{evidenceId}` enforces retention: `DeletedAt` is set and, when an `Approved` badge existed, the count is decremented (badge revokes once it hits zero).

## Badge lifecycle

- A `verified_guide_badges` row appears the first time any evidence is approved and refreshes `LastGrantedAt` on each subsequent approval. The unique index on `GuideId` keeps the badge singular.
- `DELETE /api/v1/admin/guides/{guideId}/badge` lets administrators revoke the badge explicitly (for example, when a creator unpublishes the guide). Revoked badges no longer satisfy public visitors who call the badge endpoint.
- `GET /api/v1/guides/{guideId}/evidence/badge` returns `{ verified, approvedEvidenceCount, firstGrantedAt, lastGrantedAt }` to the public. No evidence bodies are included.

## Coarse actual metrics

- `POST /api/v1/guides/{guideId}/insights { partySize, tripDays, totalCostMinorUnits, currencyCode }` records one opt-in submission per user per guide (`409` on duplicates). Creators cannot share insights about their own guides.
- `GET /api/v1/guides/{guideId}/insights` returns the public aggregate (`Median` and `Average` per dominant currency). Responses are withheld until at least 5 travelers opt in (`meetsKAnonymity = false`); the API never reveals precise low-count aggregates to protect privacy.

## Operations

The `Trippify.Verified` meter emits `trippify.verified.commands` with low-cardinality `operation` tags (`evidence-submitted`, `evidence-reviewed`, `evidence-deleted`, `badge-revoked`, `insight-submitted`). Bodies, redacted references, and reasons are not part of the telemetry. Alert on elevated 409 (duplicate evidence/insights) and 403 (creator self-review attempts). Retention cleanup is a job separate from the API; it issues a delete using the existing `evidence-deleted` operation so dashboards stay consistent.

## Privacy guarantees

- Evidence bodies are accessible only to the submitter (via guide moderation scopes) and to the `Administrator` role. Public badge responses include only counts and timestamps.
- Insights responses are gated by k-anonymity (`>= 5` submissions). Until then the only response is `meetsKAnonymity = false` with `submissionCount`.
- Cascade deletes from `travel_guides`, `users`, and `purchase_entitlements` remove related evidence and audit entries; verified badges ride along with their guides.
