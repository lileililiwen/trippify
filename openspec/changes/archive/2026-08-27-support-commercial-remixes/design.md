# Context
Commercial derivatives need consent and deterministic revenue.

# Goals / Non-goals
- Deliver only `commercial-remix-revenue`; do not absorb later changes.

# Decisions
- PostgreSQL owns transactional state; ASP.NET Core exposes versioned contracts.
- Provider integrations use adapters; Flutter never decides authorization.
- State transitions are audited and retried work is idempotent.

# Risks / Trade-offs
Authorization, privacy, concurrency, provider failure, and migrations require negative tests.
