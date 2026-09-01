# release-quality-gates Specification

## Purpose
TBD - created by archiving change add-rendered-browser-and-release-quality-gates. Update Purpose after archive.
## Requirements
### Requirement: Human workflows SHALL have rendered coverage

Supported Flutter web builds MUST have browser smoke coverage for anonymous discovery, authentication, traveler library, creator authoring, and denied role access.

#### Scenario: Anonymous discovery

- **Given** the web app is loaded without credentials
- **When** the user opens discovery and a guide detail
- **Then** the rendered controls and content are visible and navigable
- **And** protected actions do not grant access

#### Scenario: Creator authoring

- **Given** a seeded creator is authenticated
- **When** the creator creates or edits a draft and opens planning
- **Then** the rendered workflow reaches its save result
- **And** stale/conflict feedback is visible if simulated

### Requirement: Migration and recovery SHALL be verified

Release verification MUST apply migrations to an empty PostgreSQL/PostGIS database, upgrade the supported prior schema, and exercise backup capture/delete/restore in isolation.

#### Scenario: Previous release upgrade

- **Given** a database at the previous supported migration
- **When** the current release migrations apply
- **Then** all migrations succeed
- **And** protected API smoke requests operate against the upgraded schema

#### Scenario: Restore after artifact loss

- **Given** a captured backup artifact is deleted or unavailable
- **When** restore is attempted
- **Then** restore fails safely without overwriting the live database
- **And** the failure is visible to the operator

### Requirement: Skips SHALL be accountable

CI MUST report skipped tests and MUST fail when a skip lacks an approved reason or when the skip count increases unexpectedly.

#### Scenario: Unapproved skip

- **Given** a new test is marked skipped without an allowed platform reason
- **When** CI runs
- **Then** the quality job fails

