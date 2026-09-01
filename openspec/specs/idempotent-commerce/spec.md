# idempotent-commerce Specification

## Purpose
TBD - created by archiving change make-commerce-checkout-idempotent. Update Purpose after archive.
## Requirements
### Requirement: Checkout retries SHALL be idempotent

For an authenticated buyer, repeated checkout requests with the same valid idempotency key and identical guide/commercial inputs MUST return the same order and provider session without creating another order or payment session.

#### Scenario: Sequential retry

- **Given** the first checkout request created order A
- **When** the buyer retries with the same key and payload
- **Then** the API returns order A
- **And** the payment gateway is called only once

#### Scenario: Concurrent retry

- **Given** two requests for the same buyer, guide, and key arrive concurrently
- **When** both attempt checkout
- **Then** exactly one order is persisted
- **And** both successful responses identify that order

### Requirement: Conflicting key reuse SHALL be rejected

Reusing a key with a different guide, amount, currency, discount, or buyer MUST return a conflict and MUST NOT mutate the original order.

#### Scenario: Payload changes after retry

- **Given** a key is bound to a 1000 JPY checkout
- **When** it is reused for a different guide or amount
- **Then** the API returns 409
- **And** no second provider session or order is created

### Requirement: Payment events SHALL remain replay-safe

Duplicate or concurrent signed payment events MUST create at most one entitlement and one set of sale/refund ledger entries.

#### Scenario: Duplicate paid webhook

- **Given** a paid event has already been processed
- **When** the same event is delivered again
- **Then** the response reports replay
- **And** entitlement and ledger counts remain unchanged

