# Why
The registered background queue silently discards work, leaving retention cleanup and asynchronous notifications without execution or retry guarantees.

# What Changes
Add durable job persistence, workers, leases, retry/backoff, dead-letter handling, idempotency, and operational visibility.

# Capabilities
## New Capabilities
- `durable-background-processing`: persistent, retry-safe execution for retention and notification work.

# Dependencies and Non-goals
- Dependencies: PostgreSQL persistence, verified trips, notifications, and provider adapters.
- Non-goals: a separate microservice or arbitrary user-authored jobs.

# Impact
Changes the job abstraction implementation, hosted workers, database schema, admin observability, and deployment lifecycle.
