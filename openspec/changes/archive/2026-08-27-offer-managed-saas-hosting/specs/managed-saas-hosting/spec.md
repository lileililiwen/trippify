# managed saas hosting Specification

## ADDED Requirements

### Requirement: managed saas hosting
The system SHALL provide tenant provisioning, subscriptions, quotas, metering, branding/domains, export/deletion, and SLO tooling.

#### Scenario: A tenant exceeds a quota
- **WHEN** a tenant exceeds a quota
- **THEN** only that tenant's limited operation is rejected

### Requirement: Server-authoritative access
The system SHALL enforce ownership, role, visibility, and entitlement rules in the ASP.NET Core API and return only authorized fields.

#### Scenario: Unauthorized operation
- **WHEN** an anonymous or unrelated user attempts a protected operation
- **THEN** it is rejected without changing data or revealing protected content
