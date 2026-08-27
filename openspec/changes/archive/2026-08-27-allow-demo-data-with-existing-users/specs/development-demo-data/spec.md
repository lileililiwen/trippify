# development demo data Specification

## MODIFIED Requirements

### Requirement: Existing-data preservation
The system SHALL preserve unrelated existing users and their data while adding demo fixtures, SHALL make repeated execution safe, and SHALL reject ambiguous partial demo identity sets.

#### Scenario: Database already contains a user
- **WHEN** the enabled Development seeder finds existing users whose normalized emails do not match documented demo identities
- **THEN** it adds the complete demo dataset without updating or deleting any existing record

#### Scenario: Seeder is invoked repeatedly
- **WHEN** a successful demo seed is followed by additional invocations that find the complete documented demo identity set
- **THEN** aggregate counts and relationships remain unchanged

#### Scenario: Partial demo identity set exists
- **WHEN** the seeder finds some but not all documented demo identities
- **THEN** it fails with reset guidance and does not claim that demo data is available
