# self hosted distribution Specification

## ADDED Requirements

### Requirement: self hosted distribution
The system SHALL provide containers, migrations, bootstrap, backup/restore, upgrades, feature flags, and provider adapters.

#### Scenario: An operator upgrades from the previous release
- **WHEN** an operator upgrades from the previous release
- **THEN** data is preserved and migrations run once

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
