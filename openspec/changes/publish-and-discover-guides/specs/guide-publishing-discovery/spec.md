# guide publishing discovery Specification

## ADDED Requirements

### Requirement: guide publishing discovery
The system SHALL provide publication validation, previews, taxonomy, faceted search, author pages, SEO URLs, metadata, and sharing.

#### Scenario: A visitor opens a paid guide URL
- **WHEN** a visitor opens a paid guide URL
- **THEN** only configured preview fields and purchase metadata are returned

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
