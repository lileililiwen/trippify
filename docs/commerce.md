# Guide commerce and entitlements

Paid guides are sold through authoritative, webhook-confirmed orders; access is granted through durable entitlements enforced server-side.

## Purchase flow

- `POST /api/v1/commerce/checkout { guideId, discountCode? }` validates the guide is published as `Paid`, rejects creators buying their own guide, applies an active per-guide discount code, and calls the payment gateway adapter. Gateway outages return `503` without persisting an order.
- Orders start `Pending`. The gateway confirms through `POST /api/v1/commerce/webhook` (anonymous route) guarded by a constant-time `X-Webhook-Secret` comparison against injected configuration; production must inject the secret and never commit it.
- Webhooks are idempotent twice over: event IDs are deduplicated in `payment_webhook_events`, and status transitions only move forward (`Pending → Paid → Refunded`). Delivering a valid webhook twice yields exactly one paid order and one entitlement.
- Confirmation writes three ledger rows — gross, commission (rate snapshot from `Commerce:CommissionRate`, default 0.10), and creator net — so payouts reconcile even if rates change later.
- `payment.refunded` marks the order refunded, revokes the entitlement, and appends a refund ledger row.

## Access boundaries

- Entitlements live in `purchase_entitlements` with a unique `(GuideId, UserId)` index; revoked rows keep history via `RevokedAt`.
- Paid guide URLs return preview fields plus purchase metadata until the requester owns the guide or holds an active entitlement; then the full outline is returned with `unlocked: true`. Anonymous visitors always receive previews only.
- Buyers list their orders and active entitlements through `/api/v1/commerce/orders` and `/api/v1/commerce/entitlements`; both are scoped to the authenticated user.
- Discount codes are created by the guide owner only (`POST /api/v1/commerce/guides/{id}/discounts`), unique per guide, 1–100 percent off.

## Operations

The `Trippify.Commerce` meter emits `trippify.commerce.commands` with low-cardinality `operation` tags (`checkout`, `webhook:payment.paid`, `webhook:payment.refunded`, `discount-created`). Alert on elevated 503 (gateway outage) and repeated non-replay webhook rejections. Never record buyer identity, amounts, codes, or references in telemetry payloads beyond order identifiers.

Apply the forward-only EF Core migration before the matching API version. Verify foreign keys restrict deletes on orders and that ledger uniqueness `(OrderId, Kind)` holds after deployment.
