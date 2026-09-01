# Proposal: Make commerce checkout idempotent

## Problem

Checkout derives its idempotency key from the current timestamp. Retrying a timed-out request can create multiple provider sessions and pending orders for one buyer and guide.

## Scope

Introduce a caller-supplied or server-stable idempotency key, persist the association, deduplicate concurrent retries, and preserve webhook replay safety. Include amount/currency/guide validation for reused keys. This excludes refunds policy redesign and provider-specific checkout UI.

## Acceptance

Repeated requests with the same key return the original checkout; conflicting reuse is rejected; concurrent requests create one order/provider session; payment and refund webhooks remain idempotent.
