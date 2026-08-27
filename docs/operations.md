# Creator and admin operations

Creators read their own sales ledger in a single dashboard while administrators operate the platform via audited endpoints. Both surfaces are server-authoritative: the Flutter client only renders what the ASP.NET Core API returns.

## Creator dashboard

- `GET /api/v1/creator/dashboard/overview` returns guide counts, paid/refunded order counts, and revenue per currency. The API derives these totals from `GuideOrder` rows that belong to guides owned by the caller. Non-creator callers receive `403`; the response never includes other creators' data.
- `GET /api/v1/creator/dashboard/orders?limit=N` returns the most recent orders for the caller's guides. Pagination is bounded by `N` (clamped between `1` and `200`).
- `GET /api/v1/creator/dashboard/reviews` aggregates visible, flagged, hidden, and open-report counts for the caller's reviews.

## Administrator operations

- `GET /api/v1/admin/operations/audit?limit=N` lists recent `IdentityAuditEntry` rows newest-first. Only the `Administrator` role passes the policy; everything else returns `403`.
- `GET /api/v1/admin/operations/users?limit=N` exposes user accounts with email, status, and confirmation flag. Password hashes and other security fields are never returned.
- `GET /api/v1/admin/operations/creators?limit=N` lists every `CreatorProfile` with slug and status. `Biography`, `TravelCountries`, and other private fields stay private.

## Operations

The `Trippify.Operations` meter emits `trippify.operations.commands` with low-cardinality operation tags (`creator-overview`, `creator-orders-listed`, `creator-review-summary`, `admin-audit-listed`, `admin-users-listed`, `admin-creators-listed`). Bodies, emails, bios, and reasons are never logged. Alert on elevated `403` (someone probing dashboards) and `500` (data layer regression).

## Privacy guarantees

- Creator revenue is server-derived from the caller's guides; cross-creator leakage is rejected by ownership checks before projection.
- Admin endpoints are role-gated to `Administrator`; non-admins receive `403` before any data is materialized.
- Sensitive creator-only fields (biography, biography, travel countries, avatar URLs) are intentionally absent from `AdminCreatorRow` to keep private profile data out of the operator console.
- All list endpoints accept a `limit` parameter that is clamped between `1` and `200` to protect against accidentally unbounded scans.
