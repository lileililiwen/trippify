# managed saas quota enforcement Specification

## ADDED Requirements

### Requirement: Atomic quota enforcement
The system SHALL enforce configured tenant limits on every metered server operation using atomic reservation and consumption.

#### Scenario: Two requests compete for the last unit
- **WHEN** concurrent operations reserve a tenant's final available quota unit
- **THEN** at most one succeeds and recorded usage does not exceed the limit

#### Scenario: A reserved operation fails
- **WHEN** a provider or transaction fails after quota reservation
- **THEN** the reservation is released and durable usage is not increased

### Requirement: Tenant-safe quota feedback
The system SHALL return stable, scoped quota state and denial information without exposing another tenant's usage.

#### Scenario: Tenant exceeds a limit
- **WHEN** a member requests an operation with no remaining quota
- **THEN** the API rejects it with the metric, current usage, limit, and reset time and changes no domain state

#### Scenario: Member queries usage
- **WHEN** an authenticated member requests quota status
- **THEN** only that member's tenant periods and metrics are returned
