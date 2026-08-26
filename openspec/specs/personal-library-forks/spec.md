# personal library forks Specification

## Purpose
TBD - created by archiving change manage-personal-library-and-forks. Update Purpose after archive.
## Requirements
### Requirement: personal library forks
The system SHALL provide favorites, My Trips, trip status, and permission-aware non-commercial forks with provenance.

#### Scenario: An entitled user forks an allowed guide
- **WHEN** an entitled user forks an allowed guide
- **THEN** an independent private plan with source attribution is created

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
