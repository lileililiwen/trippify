# Guide commerce and entitlements

Paid guides are sold through authoritative, webhook-confirmed orders; access is granted through durable entitlements enforced server-side.

## Purchase flow

- `POST /api/v1/commerce/checkout { guideId, discountCode? }` validates the guide is published as `Paid`, rejects creators buying their own guide, applies an active per-guide discount code, and calls the payment gateway adapter. The adapter is selected by configuration: `Payment:Provider=local` keeps the legacy `NotSupportedException` flow used by self-hosted and tests; `Payment:Provider=http` activates `HttpPaymentGateway` (production) and validates `Payment:Endpoint`, `Payment:ApiKey`, `Payment:WebhookSecret` at startup. The configured `Payment:ApiKey` is sent as the `Authorization: Bearer …` credential on every checkout request — placeholders, hard-coded values, or empty tokens are never accepted.
- **Idempotent checkout.** Clients MAY send an `Idempotency-Key` header (8–200 characters of letters, digits, `-`, `_`, or `.`). The API persists the key together with buyer, guide, amount, currency, discount code, and order reference in `checkout_idempotency_keys`; a unique index on `(BuyerUserId, Scope, Key)` scopes the key to the authenticated buyer and the `checkout` operation. Repeated requests with the same valid key and identical guide/commercial inputs return the original order and never re-call the payment gateway. Reusing a key with a different guide, amount, currency, or discount returns `409 idempotency-key-reuse` and does not mutate the original order. When clients omit the header, the server derives a stable key from the buyer, guide, and applied discount so concurrent retries of the same intent still coalesce. A concurrent first request is resolved by the unique index: one request commits the new order and key, every other losing request reloads the winner and returns its order, so exactly one provider session and one `guide_orders` row is created per idempotent attempt.
- The HTTP adapter issues `POST /v1/checkouts` to the configured payment provider, sends the same `Idempotency-Key` so the provider can dedupe, returns a typed `CheckoutSession` (`Url`, `Reference`, `AmountMinorUnits`, `CurrencyCode`), persists the order with the `ProviderReference` and `ProviderName`, and returns the checkout URL to the Flutter client. Gateway outages, 401/403 rejections, or transport timeouts translate to `503 Payments are unavailable.` without persisting an order. The exact API key is never logged or echoed in responses.
- Orders start `Pending`. The gateway confirms through `POST /api/v1/commerce/webhook` (anonymous route). Production webhooks are verified over the **raw request body** by `HttpPaymentGateway.VerifySignature` (HMAC-SHA256 with `v1=` prefix), keyed off `Payment:WebhookHeader`. Local/self-hosted deployments without a verifier continue to honour the legacy `X-Webhook-Secret` constant-time comparison so on-prem operators are not forced to wire an external provider.
- **Webhook replay safety.** Event IDs are deduplicated in `payment_webhook_events` (primary key on `EventId`). The webhook handler also catches the unique-constraint violation thrown by concurrent deliveries and returns `{ replayed: true }` without re-applying the order transition. The combined pre-check plus primary-key backstop means duplicate or concurrent signed payment events create at most one entitlement and one set of sale/refund ledger entries. Status transitions only move forward (`Pending → Paid → Refunded`).
- Confirmation writes three ledger rows — gross, commission (rate snapshot from `Commerce:CommissionRate`, default 0.10), and creator net — so payouts reconcile even if rates change later.
- `payment.refunded` marks the order refunded, revokes the entitlement, and appends a refund ledger row.

## Access boundaries

- Entitlements live in `purchase_entitlements` with a unique `(GuideId, UserId)` index; revoked rows keep history via `RevokedAt`.
- Idempotency keys live in `checkout_idempotency_keys` with a unique `(BuyerUserId, Scope, Key)` index and a unique `OrderId` index; they are append-only and removed only by cascading deletes on the underlying order.
- Paid guide URLs return preview fields plus purchase metadata until the requester owns the guide or holds an active entitlement; then the full outline is returned with `unlocked: true`. Anonymous visitors always receive previews only.
- Buyers list their orders and active entitlements through `/api/v1/commerce/orders` and `/api/v1/commerce/entitlements`; both are scoped to the authenticated user.
- Discount codes are created by the guide owner only (`POST /api/v1/commerce/guides/{id}/discounts`), unique per guide, 1–100 percent off.
- The Flutter client surfaces the provider checkout URL returned by the API; declined, cancelled, and offline provider responses map to specific status text (see `docs/operations.md`). A 409 response from the API renders "This checkout attempt was already used for a different purchase. Start a new checkout to continue." so the buyer can recover without producing a duplicate paid order.

## Configuration

| Setting | Description |
| --- | --- |
| `Payment:Provider` | `local` (default) keeps the on-prem disabled path; `http` activates the production adapter. |
| `Payment:Endpoint` | Base URL for the production payment provider. Required when `Provider=http`. |
| `Payment:ApiKey` | Server-side credential sent as the `Authorization: Bearer …` value on every checkout request. Required when `Provider=http`. Never logged or returned to clients. |
| `Payment:WebhookSecret` | Shared HMAC secret used by `HttpPaymentGateway.VerifySignature`. Required when `Provider=http`. |
| `Payment:WebhookHeader` | Header name carrying the signature (default `X-Provider-Signature`). |
| `Payment:SignatureScheme` | Prefix prepended to the signature header (default `v1`). |
| `Payment:TimeoutMilliseconds` | HTTP timeout for provider calls (default 10 000 ms). |
| `Payment:Enabled` | Allows temporary shutdown of paid checkout without removing credentials. Startup validation is skipped when `Enabled=false`; the legacy `local` provider is not affected. |
| `Commerce:WebhookSecret` | Legacy shared secret for the local `X-Webhook-Secret` path. |
| `Commerce:CommissionRate` | Decimal commission rate snapshotted at confirmation (default 0.10). |

## Operations

The `Trippify.Commerce` meter emits `trippify.commerce.commands` with low-cardinality `operation` tags (`checkout`, `webhook:payment.paid`, `webhook:payment.refunded`, `discount-created`). Alert on elevated 503 (gateway outage) and repeated non-replay webhook rejections. Never record buyer identity, amounts, codes, or references in telemetry payloads beyond order identifiers. Provider credentials, raw webhook bodies, and discount codes are never logged.

Apply the forward-only EF Core migration before the matching API version. Verify foreign keys restrict deletes on orders, the unique index on `(OrderId, Kind)` ledger entries holds after deployment, and that `ProviderReference` / `ProviderName` populate when the production adapter is enabled.