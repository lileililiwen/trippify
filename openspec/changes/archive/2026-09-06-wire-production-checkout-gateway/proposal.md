# Proposal: Wire production checkout gateway

## Problem

The `production-payment-processing` capability claims an eligible buyer can
start a provider-hosted checkout and that an order is persisted only after the
provider accepts session creation. In the running application this is never
true: `Trippify.Api/Program.cs:104` registers `IPaymentGateway` as
`LocalPaymentGateway` **unconditionally**. That implementation
(`Trippify.Infrastructure/LocalProviders.cs:14`) throws
`NotSupportedException("Payments are disabled in local mode.")`.

The real provider client `HttpPaymentGateway` (`PaymentProviders.cs:53`)
implements both `IPaymentGateway` and `IPaymentWebhookVerifier`, but the
startup composition only ever instantiates it as the *webhook verifier*
(`Program.cs:108-118`). `CommerceEndpoints.Checkout` (`CommerceEndpoints.cs:29`)
therefore always receives the throwing local gateway, so every real checkout
returns HTTP 503 and no order is ever created in production.

The defect is masked by the test suite: `CommerceApiTests` removes the real
gateway and injects `FakeGateway` (`CommerceApiTests.cs:29-30`), so the
gateway-throwing path is never exercised end to end. The
`wire-production-provider-credentials` change wired AI and object storage with
a conditional provider pattern but omitted the checkout gateway, leaving the
core commerce claim broken.

## Scope

This change makes `IPaymentGateway` resolve to `HttpPaymentGateway` when the
payment provider is enabled and not in local mode, mirroring the existing AI
and webhook-verifier composition. It includes conditional registration,
startup validation, fail-safe behavior when disabled, and end-to-end checkout
tests that use the real adapter against a request-capturing fake server
(rather than a swapped-in fake gateway). It excludes payment settlement
behavior, webhook processing (already covered), and changes to local-mode
disabled adapters.

## Roles and consequences

Only server-side checkout composition and operators are affected. A disabled
or misconfigured provider MUST fail checkout safely (503 / controlled
unavailable) and MUST NOT create an order or entitlement. Secrets remain
server-only. No client change is required.

## Acceptance

With `Payment:Provider=http` and valid configuration, a checkout against a
fake provider server returns a real opaque checkout reference and persists an
order only after the provider accepts; with the provider disabled or local,
checkout returns a controlled failure and creates no order. Tests exercise the
real `HttpPaymentGateway` path without swapping the registered service.
