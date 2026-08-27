# Commercial remix revenue

Commercial derivatives need clear licenses, attribution, approval, and deterministic revenue allocation. This slice adds per-creator license policies, ancestry declarations with decisions, audited approvals, and immutable revenue share records that total the order amount.

## License policies

- `POST /api/v1/me/license-policies { slug, displayName, allowCommercial, requireApproval, royaltyPercent }` upserts a license for the caller. `royaltyPercent` must be `0`-`100`.
- `GET /api/v1/me/license-policies` returns the caller's policies.
- `GET /api/v1/creators/{slug}/license` returns the **public** policies for a creator, but only the ones flagged `allowCommercial = true`. Sensitive settings stay private.

## Ancestry and approval

- `POST /api/v1/guides/{guideId}/remix/ancestry { parentGuideId, licensePolicyId, attributionJson? }` declares ancestry from the **child** guide. Only the child guide's owner may do so (`403` otherwise). When the policy's `requireApproval = true`, the ancestry lands in `Pending`; otherwise it auto-approves.
- `GET /api/v1/guides/{guideId}/ancestry` returns the public ancestry record (or `404` when none exists).
- `POST /api/v1/admin/remix-approvals/{ancestryId}/decide { decision, reason? }` flips the ancestry from `Pending` to `Approved` or `Rejected` and writes an immutable `RemixApproval` row. Subsequent calls short-circuit on already-decided rows so the operation is idempotent.
- `GET /api/v1/admin/remix-approvals/queue` returns only the pending ancestry rows.

## Revenue shares

- `POST /api/v1/admin/revenue-shares { orderId, shares[] }` records **immutable** revenue share rows for an order. The endpoint rejects (`400`) when:
  - shares' currency mismatches the order currency,
  - shares' percent doesn't total `100`,
  - shares' amounts don't total `order.AmountMinorUnits`,
  - shares are already recorded for the order (idempotent return instead of double-bookkeeping).
- `GET /api/v1/admin/revenue-shares?orderId=` returns the shares for a single order or all shares when the query parameter is omitted.

## Operations

The `Trippify.CommercialRemixes` meter emits `trippify.commercialremixes.commands` with low-cardinality `operation` tags (`license-policy-upserted`, `ancestry-declared`, `ancestry-decided`, `revenue-shares-recorded`). Manifests, secrets, and signed approvals are never part of the telemetry. Alert on elevated `400` (share mismatch) and `409` (duplicate ancestry declaration).

## Privacy guarantees

- License policies' list endpoints never expose creator-internal fields (biography, country code, settings) — only the commercial-relevant subset.
- Ancestry declaration requires the child guide's owner; the approval flow lives behind the `Administrator` role.
- Revenue share recording fails closed: mismatched currency, percent, or amount always returns `400` before any rows are written.
