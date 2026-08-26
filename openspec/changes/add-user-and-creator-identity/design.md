# Context
Identity data includes private account details and public creator metadata with different disclosure rules.

# Goals / Non-goals
- Goal: secure account lifecycle and discoverable creator identity.
- Non-goal: social graph or marketplace payouts.

# Decisions
- Use ASP.NET Core Identity-compatible primitives and short-lived tokens with revocation/rotation.
- Separate private account data from public profile projections.
- Model creator and account status explicitly; server policies remain authoritative.

# Risks / Trade-offs
- Enumeration and abuse are limited with uniform responses, verification, audit records, and rate limits.
