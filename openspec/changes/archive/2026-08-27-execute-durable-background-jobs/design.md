# Context
`EnqueueAsync` currently returns success without retaining or executing work.

# Goals / Non-goals
- Never acknowledge durable work before it is persisted.
- Provide at-least-once execution with idempotent handlers, not exactly-once transport claims.

# Decisions
- Store jobs transactionally in PostgreSQL with type, payload version, idempotency key, availability, attempts, lease, and terminal status.
- Workers claim jobs with bounded leases and exponential backoff; exhausted jobs enter a dead-letter state.
- Register explicit handlers for evidence retention and notification delivery/fan-out.
- Redact job payloads from logs and expose only low-cardinality operational metadata.

# Authorization, privacy, and failure modes
- Admin job inspection excludes private evidence and notification content.
- Worker crashes release through lease expiry; re-execution relies on handler idempotency.

# Migration and rollback
- Add a forward-only job table and indexes. Disabling workers stops execution without losing queued work.
