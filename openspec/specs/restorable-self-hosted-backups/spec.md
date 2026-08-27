# restorable-self-hosted-backups Specification

## Purpose
TBD - created by archiving change implement-restorable-self-hosted-backups. Update Purpose after archive.
## Requirements
### Requirement: Recoverable backup artifact
The system SHALL create an encrypted, versioned, integrity-checked artifact containing all supported relational and object-storage data required for recovery.

#### Scenario: A backup completes
- **WHEN** an administrator requests a backup and every source is captured successfully
- **THEN** the artifact is marked restorable with checksums, version metadata, and an audit record

#### Scenario: A backup is partial
- **WHEN** any required database or object capture fails
- **THEN** the artifact is marked failed and is never presented as restorable

### Requirement: Safe restore
The system SHALL validate an artifact before restoring it through an operator-controlled maintenance workflow.

#### Scenario: Valid disaster-recovery drill
- **WHEN** a valid artifact is restored into an empty supported target
- **THEN** relational state, constraints, migrations, and stored-object checksums match the captured source

#### Scenario: Artifact validation fails
- **WHEN** encryption, checksum, version, or capacity validation fails
- **THEN** restore stops before replacing target data and reports a non-secret diagnostic

