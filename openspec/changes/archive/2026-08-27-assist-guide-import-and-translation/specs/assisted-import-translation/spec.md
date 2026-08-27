# assisted import translation Specification

## ADDED Requirements

### Requirement: assisted import translation
The system SHALL provide queued text/photo/video imports, replanning, linked translations, provenance, quotas, and review.

#### Scenario: An import finishes
- **WHEN** an import finishes
- **THEN** a private attributed draft awaits human approval and is not auto-published

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
