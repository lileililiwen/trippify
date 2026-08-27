# commercial remix revenue Specification

## ADDED Requirements

### Requirement: commercial remix revenue
The system SHALL provide license policies, ancestry, approval, attribution, and multi-party revenue allocation.

#### Scenario: A licensed remix sale settles
- **WHEN** a licensed remix sale settles
- **THEN** all immutable revenue shares total the sale amount

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
