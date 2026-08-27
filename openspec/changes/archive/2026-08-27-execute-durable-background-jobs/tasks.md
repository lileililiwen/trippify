# Tasks

## 1. Durable queue
- [x] 1.1 Add versioned job records, unique idempotency keys, leases, attempts, scheduling, terminal status, and a forward-only migration.
- [x] 1.2 Persist enqueue operations atomically with originating state changes.
- [x] 1.3 Implement bounded workers, retries, backoff, dead-letter handling, shutdown, and health checks.

## 2. Handlers and operations
- [x] 2.1 Implement idempotent verified-evidence retention cleanup and badge reconciliation.
- [x] 2.2 Implement idempotent notification fan-out/delivery with user preferences checked at execution time.
- [x] 2.3 Add administrator-safe queue metrics and diagnostics without payload disclosure.

## 3. Verification
- [x] 3.1 Test restart durability, competing workers, lease expiry, retry, poison jobs, cancellation, and duplicate enqueue/execution.
- [x] 3.2 Test retention deadlines and notification opt-out across anonymous/admin boundaries.
- [x] 3.3 Document worker deployment and run backend quality gates plus PostgreSQL migration tests.
