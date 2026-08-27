# Context
An unrelated user currently prevents all demo data from being created.

# Goals / Non-goals
- Preserve unrelated development accounts while making opt-in demo data usable.
- Avoid silently adopting or overwriting an account whose email collides with a known demo identity.

# Decisions
- Query only the documented demo email set at startup.
- Seed when none of those identities exists, regardless of unrelated users.
- Skip when the complete demo identity set already exists, preserving repeat-run idempotency.
- Fail with reset guidance when only part of the demo identity set exists; automatic repair could connect fictional commercial or private records to an unintended account.

# Authorization, privacy, and failure modes
- Existing users and their related data are never updated or deleted.
- The rule remains Development-only and opt-in.
- Identity uniqueness failures remain visible and do not produce a success claim.

# Migration and rollback
- No migration. Reverting restores the overly restrictive guard but does not alter seeded data.
