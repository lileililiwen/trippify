# verified evidence attachments Specification

## ADDED Requirements

### Requirement: Private evidence attachment lifecycle
The system SHALL accept validated image or document attachments for evidence and SHALL keep them private, scanned, retained, and deleted with the parent evidence lifecycle.

#### Scenario: Eligible traveler submits a valid attachment
- **WHEN** an eligible traveler attaches an owned, safe, allowed file to evidence
- **THEN** the attachment is linked privately and awaits the same review as the evidence

#### Scenario: Attachment validation or scan fails
- **WHEN** a file exceeds limits, disguises its type, or is classified unsafe
- **THEN** it cannot be reviewed or approved and its stored bytes are deleted according to policy

### Requirement: Restricted evidence access
The system SHALL expose evidence attachment metadata and expiring download access only to the submitter and authorized administrators.

#### Scenario: Public badge is requested
- **WHEN** any visitor requests verified badge or insight projections
- **THEN** no attachment identifiers, names, metadata, URLs, or bytes are returned

#### Scenario: Evidence retention expires
- **WHEN** the parent evidence reaches its deletion deadline
- **THEN** attachment records and stored bytes are deleted idempotently
