# guide versioning freshness Specification

## ADDED Requirements

### Requirement: guide versioning freshness
The system SHALL provide immutable releases, changelogs, buyer updates, freshness indicators, and place-change alerts.

#### Scenario: A creator publishes an update
- **WHEN** a creator publishes an update
- **THEN** the prior release remains immutable and buyers see the changelog

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
