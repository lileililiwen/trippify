# Tasks: Make commerce checkout idempotent

- [x] Choose and document the idempotency contract, retention period, unique index, and conflict response.
- [x] Add red API/database tests for same-key retry, conflicting payload, concurrent checkout, and provider timeout ambiguity.
- [x] Implement persistence and stable provider key propagation with transaction-safe duplicate handling.
- [x] Add webhook concurrency/replay tests proving one entitlement and one ledger sale per paid order.
- [x] Add Flutter request support for the key and user-visible retry behavior without duplicate purchase messaging.
- [ ] Run PostgreSQL-focused tests, full solution tests, and documentation validation; archive only after completion.
