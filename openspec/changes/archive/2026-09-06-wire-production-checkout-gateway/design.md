# Design: Wire production checkout gateway

## Ownership

`Trippify.Api` owns DI composition and startup validation. `Trippify.Infrastructure`
owns `HttpPaymentGateway` (already implemented) and `PaymentProviderOptions`.
No new provider adapter is required; the existing `HttpPaymentGateway` is wired
where it is currently missing.

## Decisions

`IPaymentGateway` SHALL be resolved with the same conditional pattern already
used for `IAiAssistant` and `IPaymentWebhookVerifier` (`Program.cs:108-130`):
when `PaymentProviderOptions.Provider` is not `local` and `Enabled` is true,
register `HttpPaymentGateway` as `IPaymentGateway`; otherwise register
`LocalPaymentGateway`. The webhook-verifier registration is unchanged.

`PaymentProviderOptions.Validate` SHALL reject `Provider=local` (or
`Enabled=false`) in Staging/Production so a misconfigured host fails fast
instead of silently serving the disabled local gateway. This closes the gap
where the validator currently forbids weak secrets for CORS but not a disabled
payment provider in production.

## Failure and privacy

A disabled or unavailable provider MUST return a controlled unavailable result
(503) and MUST NOT create an order, entitlement, or ledger entry. Provider
auth rejection, timeout, and transport failure are treated as provider
failures, not successful empty results. The configured API key MUST NOT appear
in logs, telemetry, or HTTP responses.

## Rollback

Rollback is configuration-safe: operators can select local mode, but production
deployments MUST NOT silently fall back to the throwing local gateway. A bad
release is recoverable by redeploying prior build; no migration or data change
is involved.

## Test strategy

Replace the `RemoveAll<IPaymentGateway>(); AddSingleton<FakeGateway>` swap in
`CommerceApiTests` with a real `HttpPaymentGateway` pointed at a
request-capturing in-process fake server, and assert the outbound request
carries the configured credential and the returned order exists only after the
fake provider accepts. Keep the existing 503 / unavailable assertions by
running with the provider disabled.
