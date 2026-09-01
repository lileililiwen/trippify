# Design: Make commerce checkout idempotent

## Data flow

The checkout request gains an `Idempotency-Key` header (8–200 characters of letters, digits, `-`, `_`, or `.`). The API stores the key in `checkout_idempotency_keys` with buyer, scope (`checkout`), guide, amount, currency, discount code, order reference, and provider name. A unique database constraint scopes the key to the authenticated buyer and the `checkout` operation. A second unique index on `OrderId` keeps the record one-to-one with `guide_orders`.

Before calling the provider, the API returns a prior completed checkout for the same key when the key, guide, amount, currency, and discount code all match. Conflicting reuse returns `409` and does not mutate the original order. For concurrent first requests, a database uniqueness conflict is handled by reloading the winning order and returning it; the provider call SHALL use the same stable key so the provider can dedupe at its end. The server falls back to a derived key (`guide:{id}:buyer:{id}:{discountCode|"-"}`) when the client omits the header so two retries of the same intent still coalesce even without client cooperation.

## Financial safety

The order and idempotency record are persisted in the same `SaveChangesAsync`; a unique violation on the index indicates a concurrent winner whose order is reloaded and returned. The provider is only called on the first winning request. The order is created only after a provider session is successfully created, and payment authority remains the signed webhook. No entitlement is granted during checkout. Ledger and entitlement creation remain replay-safe under duplicate events and concurrent webhook delivery through a pre-check on `payment_webhook_events` plus a primary-key backstop that catches the unique-constraint violation and returns `{ replayed: true }` without re-applying the order transition.

## Tests

Cover sequential retry, invalid header format, conflicting payload, concurrent checkout (with a pre-existing key to exercise the reload path under the InMemory test fixture), scope isolation across buyers, provider timeout retry, duplicate paid webhook, signed webhook replay, and cross-user isolation. Concurrent unique-constraint handling for `checkout_idempotency_keys` is enforced in production by PostgreSQL; the InMemory test fixture cannot enforce alternate unique indexes, so the concurrent behavior is covered through deterministic pre-seed scenarios plus the sequential retry contract.
