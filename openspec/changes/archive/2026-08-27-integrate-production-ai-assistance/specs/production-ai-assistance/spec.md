# production ai assistance Specification

## ADDED Requirements

### Requirement: Provider-backed private drafts
The system SHALL create validated, attributed private drafts from eligible import or translation requests through a configured server-side AI provider.

#### Scenario: Provider returns valid structured output
- **WHEN** an owned request completes within quota and its output matches the required schema
- **THEN** a private draft with source and provider provenance awaits human review

#### Scenario: Provider output is invalid
- **WHEN** provider output fails schema or safety validation
- **THEN** the job reports a retryable or terminal failure and creates no approved or published content

### Requirement: Human-controlled publication
The system SHALL label AI-derived content and require explicit authorized approval before it changes a guide or becomes public.

#### Scenario: AI processing completes
- **WHEN** a generated draft becomes ready
- **THEN** it remains private until its owner reviews and approves it

#### Scenario: Unrelated user attempts approval
- **WHEN** a user who does not own the draft attempts to approve it
- **THEN** the API rejects the action without revealing or changing the draft
