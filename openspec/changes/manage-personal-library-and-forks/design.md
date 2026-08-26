# Context
Users need acquired guides and editable personal copies.

# Goals / Non-goals
- Deliver only `personal-library-forks`; do not absorb later changes.

# Decisions
- PostgreSQL owns transactional state; ASP.NET Core exposes versioned contracts.
- Provider integrations use adapters; Flutter never decides authorization.
- State transitions are audited and retried work is idempotent.

# Risks / Trade-offs
Authorization, privacy, concurrency, provider failure, and migrations require negative tests.
