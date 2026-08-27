# Durable background jobs

Trippify persists background work in PostgreSQL before acknowledging it. The queue records a versioned payload, unique idempotency key, next availability, attempt count, bounded lease, and terminal status. Workers claim due rows with `FOR UPDATE SKIP LOCKED`, so multiple API replicas can compete without executing the same lease concurrently.

## Lifecycle and deployment

- `Pending` jobs are available at `AvailableAt`. A worker changes the row to `Running`, increments `Attempts`, and owns it for two minutes.
- A crashed worker's expired lease is reclaimable. Failures use bounded exponential backoff; the fifth failed attempt becomes `DeadLetter`.
- Cancellation returns a claimed job to `Pending`, and graceful host shutdown passes cancellation into the current handler.
- Set `BackgroundJobs__WorkersEnabled=false` to stop execution without deleting queued work. The default is enabled. Run at least one API replica with workers enabled.
- `/health/ready` becomes degraded when dead letters or expired leases require attention.

## Handlers

- `evidence-retention` is scheduled in the same database commit as evidence submission. At the retention deadline it clears private evidence content, soft-deletes the evidence, and recalculates the guide badge from remaining approved evidence. Replay is harmless.
- `notification-delivery` stores recipient and target identifiers behind an idempotency key. The handler re-reads current notification preferences immediately before in-app/email delivery and suppresses opted-out channels. A unique source-job link prevents duplicate in-app rows.

## Operations and privacy

`GET /api/v1/admin/background-jobs` returns counts grouped by low-cardinality job type and status plus the oldest availability timestamp. It never returns payloads, recipients, evidence, notification copy, lease owners, or exception messages. Anonymous and non-administrator callers are denied.

The `Trippify.BackgroundJobs` meter emits `trippify.backgroundjobs.outcomes` with only `type` and `outcome` tags. Logs contain job type, attempt number, and a bounded failure code; payloads are never logged.
