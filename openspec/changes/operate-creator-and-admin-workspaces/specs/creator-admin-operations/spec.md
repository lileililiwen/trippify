# creator admin operations Specification

## ADDED Requirements

### Requirement: creator admin operations
The system SHALL provide creator sales/income dashboards and audited role-scoped administration.

#### Scenario: A creator requests revenue totals
- **WHEN** a creator requests revenue totals
- **THEN** only that creator's ledger-derived data is included

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
