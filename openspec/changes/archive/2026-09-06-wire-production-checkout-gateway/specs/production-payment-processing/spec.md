# production-payment-processing

## MODIFIED Requirements

### Requirement: Provider-hosted checkout

The system SHALL create a real provider-hosted checkout session for an eligible paid-guide purchase and SHALL persist an order only after the provider accepts session creation. The checkout gateway (`IPaymentGateway`) MUST resolve to the configured HTTP provider gateway when the payment provider is enabled and not in local mode, and MUST NOT be the disabled local gateway through the production checkout path. When the provider is disabled, local, or unavailable, checkout MUST return a controlled unavailable result and MUST NOT create an order, entitlement, or ledger entry.

#### Scenario: Eligible buyer starts checkout

- **WHEN** an authenticated non-owner buys a published paid guide with valid pricing and the payment provider is enabled
- **THEN** the API returns an opaque checkout reference and provider URL without exposing provider credentials
- **AND** an order is persisted only after the provider accepts session creation

#### Scenario: Production gateway resolves to the provider

- **GIVEN** the payment provider is enabled and not in local mode
- **WHEN** the API host starts and a checkout is requested
- **THEN** the registered `IPaymentGateway` is the configured HTTP provider gateway
- **AND** checkout does not raise a disabled-provider exception

#### Scenario: Payment provider is unavailable

- **WHEN** checkout creation times out or the provider rejects the request
- **THEN** the API returns a retryable failure and creates no order or entitlement

#### Scenario: Payment provider disabled in production

- **GIVEN** the payment provider is local or disabled in a Staging/Production host
- **WHEN** the API host starts
- **THEN** startup fails with a configuration error identifying the disabled payment provider
- **AND** the host does not silently serve the throwing local gateway
