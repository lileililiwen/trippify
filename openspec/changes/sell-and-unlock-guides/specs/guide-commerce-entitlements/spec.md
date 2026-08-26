# guide commerce entitlements Specification

## ADDED Requirements

### Requirement: guide commerce entitlements
The system SHALL provide pricing, discounts, checkout, webhook-confirmed orders, refunds, commission snapshots, ledgers, and entitlements.

#### Scenario: A valid payment webhook is delivered twice
- **WHEN** a valid payment webhook is delivered twice
- **THEN** one paid order and one entitlement exist

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
