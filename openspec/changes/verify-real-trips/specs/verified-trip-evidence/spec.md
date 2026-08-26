# verified trip evidence Specification

## ADDED Requirements

### Requirement: verified trip evidence
The system SHALL provide evidence review, retention/deletion, badge lifecycle, and opt-in coarse actual metrics.

#### Scenario: An administrator reviews evidence
- **WHEN** an administrator reviews evidence
- **THEN** only authorized reviewers see evidence and the public sees no raw evidence

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
