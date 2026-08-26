# guide reviews feedback Specification

## ADDED Requirements

### Requirement: guide reviews feedback
The system SHALL provide verified-purchaser ratings, comments, author replies, moderation, reports, and update feedback.

#### Scenario: A non-purchaser submits a rating
- **WHEN** a non-purchaser submits a rating
- **THEN** the request is rejected and aggregates do not change

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
