# structured-guide-authoring Specification

## Purpose
TBD - created by archiving change author-structured-travel-guides. Update Purpose after archive.
## Requirements
### Requirement: structured guide authoring
The system SHALL provide guide ownership, metadata, days, ordered place/activity nodes, prose sections, media, and lifecycle states.

#### Scenario: A creator saves and reorders a multi-day draft
- **WHEN** a creator saves and reorders a multi-day draft
- **THEN** the structure is returned in the new order without exposure to unrelated users

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content

