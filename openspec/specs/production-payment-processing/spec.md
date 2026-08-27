# production-payment-processing Specification

## Purpose
TBD - created by archiving change integrate-production-payment-gateway. Update Purpose after archive.
## Requirements
### Requirement: Provider-hosted checkout
The system SHALL create a real provider-hosted checkout session for an eligible paid-guide purchase and SHALL persist an order only after the provider accepts session creation.

#### Scenario: Eligible buyer starts checkout
- **WHEN** an authenticated non-owner buys a published paid guide with valid pricing
- **THEN** the API returns an opaque checkout reference and provider URL without exposing provider credentials

#### Scenario: Payment provider is unavailable
- **WHEN** checkout creation times out or the provider rejects the request
- **THEN** the API returns a retryable failure and creates no order or entitlement

### Requirement: Authoritative payment events
The system SHALL verify provider signatures and process each payment event idempotently before granting or revoking access.

#### Scenario: A signed paid event is replayed
- **WHEN** the same valid paid event is delivered more than once
- **THEN** exactly one paid order, entitlement, and set of ledger entries exists

#### Scenario: A webhook signature is invalid
- **WHEN** a webhook fails provider signature verification
- **THEN** it is rejected without recording an event or changing commerce state

