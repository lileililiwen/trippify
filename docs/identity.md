# Identity operations and privacy

## Account lifecycle

- Registration returns the same success response for an accepted or already-known email. Confirmation links use the configured email adapter.
- Login requires confirmed email, active account status, and a strong password. Five failed attempts trigger Identity lockout.
- Refresh validates expiry, Security Stamp, and account status.
- Logout stores only a SHA-256 token digest and rotates the Security Stamp, invalidating access and refresh credentials.
- Forgot-password responses do not reveal whether an account exists. Reset codes are single-use Identity tokens.

## Data exposure

`/api/v1/me/profile` is authenticated and contains private email and account state. `/api/v1/creators/{slug}` is anonymous but returns only explicitly public creator fields. It never projects email, verification tokens, status history, roles, or audit data.

## Administration

Account and creator status changes require the `Administrator` role, a reason, and an audit entry. Suspended accounts are rejected by middleware. Pending or suspended creators are hidden publicly and cannot use creator-only endpoints.

## Retention

Expired revoked-token digests must be removed by scheduled maintenance once background jobs are enabled. Audit entries follow the deployment's legal retention policy.
