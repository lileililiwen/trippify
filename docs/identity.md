# Identity operations and privacy

## Account lifecycle

- Registration returns the same success response for an accepted or already-known email. Confirmation links use the configured email adapter.
- Login requires confirmed email, active account status, and a strong password. Five failed attempts trigger Identity lockout.
- Refresh validates expiry, Security Stamp, and account status.
- Logout stores only a SHA-256 token digest and rotates the Security Stamp, invalidating access and refresh credentials.
- Forgot-password responses do not reveal whether an account exists. Reset codes are single-use Identity tokens.

## Data exposure

`/api/v1/me/profile` is authenticated and contains private email and account state. `/api/v1/creators/{slug}` is anonymous but returns only explicitly public creator fields. It never projects email, verification tokens, status history, roles, or audit data.

## Session summary projection

`GET /api/v1/me/summary` is an authenticated projection consumed by the Flutter home surface to decide which tiles to render. It returns:

- `email`, `displayName`, `avatarUrl`
- `roles` — claim names (e.g. `Administrator`)
- `isCreator` — true when the caller has an active creator profile
- `accountStatus` — `Active`, `Suspended`, etc.
- `emailConfirmed`

The endpoint is `[Authorize]`-protected and returns `401` for anonymous calls without disclosing account data. The Flutter client treats the projection as the source of truth for visibility: it hides creator and administrator tiles for visitors who lack the corresponding role or creator status, and it surfaces an email-unverified warning banner with a `Resend` action while `emailConfirmed` is false. The server still enforces every authorization rule independently — the projection only informs navigation.

## Administration

Account and creator status changes require the `Administrator` role, a reason, and an audit entry. Suspended accounts are rejected by middleware. Pending or suspended creators are hidden publicly and cannot use creator-only endpoints.

## Retention

Expired revoked-token digests must be removed by scheduled maintenance once background jobs are enabled. Audit entries follow the deployment's legal retention policy.
