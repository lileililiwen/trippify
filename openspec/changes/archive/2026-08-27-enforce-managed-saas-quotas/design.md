# Context
Quota limits and usage are visible but enforcement is explicitly unwired.

# Goals / Non-goals
- Enforce limits atomically under concurrency and make usage explainable.
- Do not use Flutter state as authorization.

# Decisions
- Define a registry of metered operations and plan defaults; unknown metrics fail closed in administrative configuration.
- Reserve capacity in the same transaction as resource creation or job enqueue, then finalize or release it based on outcome.
- Use PostgreSQL concurrency control so simultaneous requests cannot exceed the limit.
- Return a stable `quota-exceeded` problem with metric, limit, used amount, and reset time but no other-tenant data.

# Authorization, privacy, and failure modes
- Tenant members see only their tenant usage; administrators retain scoped controls and audit.
- Provider or transaction failure does not consume quota permanently.

# Migration and rollback
- Add reservation/usage history through a forward-only migration and seed plan defaults idempotently.
