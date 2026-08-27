# Guide commerce and entitlements

Paid guides are sold through authoritative, webhook-confirmed orders; access is granted through durable entitlements enforced server-side.

## Purchase flow

- `POST /api/v1/commerce/checkout { guideId, discountCode? }` validates the guide is published as `Paid`, rejects creators buying their own guide, applies an active per-guide discount code, and calls the payment gateway adapter. The adapter is selected by configuration: `Payment:Provider=local` keeps the legacy `NotSupportedException` flow used by self-hosted and tests; `Payment:Provider=http` activates `HttpPaymentGateway` (production) and validates `Payment:Endpoint`, `Payment:ApiKey`, `Payment:WebhookSecret` at startup.
- The HTTP adapter issues `POST /v1/checkouts` to the configured payment provider, returns a typed `CheckoutSession` (`Url`, `Reference`, `AmountMinorUnits`, `CurrencyCode`), persists the order with the `ProviderReference` and `ProviderName`, and returns the checkout URL to the Flutter client. Gateway outages or rejection translate to `503 Payments are unavailable.` without persisting an order.
- Orders start `Pending`. The gateway confirms through `POST /api/v1/commerce/webhook` (anonymous route). Production webhooks are verified over the **raw request body** by `HttpPaymentGateway.VerifySignature` (HMAC-SHA256 with `v1=` prefix), keyed off `Payment:WebhookHeader`. Local/self-hosted deployments without a verifier continue to honour the legacy `X-Webhook-Secret` constant-time comparison so on-prem operators are not forced to wire an external provider.
- Webhooks are idempotent twice over: event IDs are deduplicated in `payment_webhook_events`, and status transitions only move forward (`Pending → Paid → Refunded`). Delivering a valid webhook twice yields exactly one paid order and one entitlement.
- Confirmation writes three ledger rows — gross, commission (rate snapshot from `Commerce:CommissionRate`, default 0.10), and creator net — so payouts reconcile even if rates change later.
- `payment.refunded` marks the order refunded, revokes the entitlement, and appends a refund ledger row.

## Access boundaries

- Entitlements live in `purchase_entitlements` with a unique `(GuideId, UserId)` index; revoked rows keep history via `RevokedAt`.
- Paid guide URLs return preview fields plus purchase metadata until the requester owns the guide or holds an active entitlement; then the full outline is returned with `unlocked: true`. Anonymous visitors always receive previews only.
- Buyers list their orders and active entitlements through `/api/v1/commerce/orders` and `/api/v1/commerce/entitlements`; both are scoped to the authenticated user.
- Discount codes are created by the guide owner only (`POST /api/v1/commerce/guides/{id}/discounts`), unique per guide, 1–100 percent off.
- The Flutter client surfaces the provider checkout URL returned by the API; declined, cancelled, and offline provider responses map to specific status text (see `docs/operations.md`).

## Configuration

| Setting | Description |
| --- | --- |
| `Payment:Provider` | `local` (default) keeps the on-prem disabled path; `http` activates the production adapter. |
| `Payment:Endpoint` | Base URL for the production payment provider. Required when `Provider=http`. |
| `Payment:ApiKey` | Server-side credential; never returned to clients. Required when `Provider=http`. |
| `Payment:WebhookSecret` | Shared HMAC secret used by `HttpPaymentGateway.VerifySignature`. Required when `Provider=http`. |
| `Payment:WebhookHeader` | Header name carrying the signature (default `X-Provider-Signature`). |
| `Payment:SignatureScheme` | Prefix prepended to the signature header (default `v1`). |
| `Payment:TimeoutMilliseconds` | HTTP timeout for provider calls (default 10 000 ms). |
| `Payment:Enabled` | Allows temporary shutdown of paid checkout without removing credentials. |
| `Commerce:WebhookSecret` | Legacy shared secret for the local `X-Webhook-Secret` path. |
| `Commerce:CommissionRate` | Decimal commission rate snapshotted at confirmation (default 0.10). |

## Operations

The `Trippify.Commerce` meter emits `trippify.commerce.commands` with low-cardinality `operation` tags (`checkout`, `webhook:payment.paid`, `webhook:payment.refunded`, `discount-created`). Alert on elevated 503 (gateway outage) and repeated non-replay webhook rejections. Never record buyer identity, amounts, codes, or references in telemetry payloads beyond order identifiers. Provider credentials, raw webhook bodies, and discount codes are never logged.

Apply the forward-only EF Core migration before the matching API version. Verify foreign keys restrict deletes on orders, the unique index on `(OrderId, Kind)` ledger entries holds after deployment, and that `ProviderReference` / `ProviderName` populate when the production adapter is enabled.