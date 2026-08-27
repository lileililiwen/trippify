# development demo data Specification

## ADDED Requirements

### Requirement: Explicit development-only seeding
The system SHALL seed demo data only when the host runs in the Development environment with demo seeding explicitly enabled.

#### Scenario: Demo seeding is explicitly enabled in Development
- **WHEN** an empty migrated database starts with `DemoSeed:Enabled=true` in Development
- **THEN** fictional traveler, creator, and administrator accounts and representative product data are created

#### Scenario: Host is not Development
- **WHEN** demo seeding is configured in Production or another non-Development environment
- **THEN** no demo seeder is registered or executed and no known demo credentials are created

### Requirement: Existing-data preservation
The system SHALL avoid modifying a database that already contains any user and SHALL make repeated execution safe.

#### Scenario: Database already contains a user
- **WHEN** the demo seeder runs against a database with one or more users
- **THEN** it exits without adding, deleting, or updating demo or existing records

#### Scenario: Seeder is invoked repeatedly
- **WHEN** a successful demo seed is followed by additional invocations
- **THEN** aggregate counts and relationships remain unchanged

### Requirement: Schema-valid representative dataset
The system SHALL create demo records that satisfy the migrated PostgreSQL schema and represent major roles and completed product surfaces.

#### Scenario: Demo data is seeded into PostgreSQL
- **WHEN** the seeder runs against an empty fully migrated PostgreSQL database
- **THEN** all writes satisfy foreign keys and unique constraints and the dataset can be queried through representative API surfaces

#### Scenario: Seed data is invalid
- **WHEN** any demo write violates a constraint or Identity operation fails
- **THEN** Development startup reports the failure instead of claiming that demo data is available
