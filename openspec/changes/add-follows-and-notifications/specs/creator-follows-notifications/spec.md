# creator follows notifications Specification

## ADDED Requirements

### Requirement: creator follows notifications
The system SHALL provide creator follows and preference-aware in-app/email notifications.

#### Scenario: A user disables email updates
- **WHEN** a user disables email updates
- **THEN** events remain in-app but email is not sent

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
