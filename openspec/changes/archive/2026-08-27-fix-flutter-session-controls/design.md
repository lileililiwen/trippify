# Context
One sign-out action calls `login("dummy", "dummy")` despite a correct `logout()` client method being available.

# Goals / Non-goals
- Ensure every sign-out entry point performs the same safe operation and returns to anonymous UI.
- Keep server revocation best-effort when offline while always clearing local credentials.

# Decisions
- Centralize sign-out in a session controller shared by home and signed-in shell.
- Call server logout when possible, clear secure access/refresh state in `finally`, clear cached private models, and replace navigation history with the anonymous route.
- Treat `401` from protected calls as session expiration and require reauthentication without retry loops.

# Authorization, privacy, and failure modes
- Never use fabricated credentials to simulate sign-out.
- Private screens must not remain reachable through back navigation after local credential removal.

# Migration and rollback
- No database migration. The change is isolated to Flutter session state and tests.
