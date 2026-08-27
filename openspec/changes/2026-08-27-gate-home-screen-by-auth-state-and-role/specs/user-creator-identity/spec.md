# User and Creator Identity Specification

## Purpose
TBD

## ADDED Requirements

### Requirement: Session summary projection
The system SHALL expose an authenticated endpoint that returns the current
user's display name, email, avatar URL, roles, active creator status, account
status, and email-confirmed flag in a single projection suitable for client
navigation gating.

#### Scenario: Authenticated summary
- **WHEN** a signed-in user calls the session summary endpoint with a valid
  access token
- **THEN** the response includes the user's email, display name, avatar URL,
  role list, active creator flag, account status, and email-confirmed flag

#### Scenario: Anonymous request
- **WHEN** an anonymous client calls the session summary endpoint
- **THEN** the API returns 401 and discloses no account information

#### Scenario: Role change visibility
- **WHEN** an administrator is granted or revoked the `Administrator` role
  and the user re-fetches the session summary
- **THEN** the response reflects the updated role list without requiring the
  client to re-authenticate

## Requirements
### Requirement: Secure account lifecycle
The system SHALL support registration, verified sign-in, token renewal, logout, and account recovery without disclosing whether unrelated accounts exist.

#### Scenario: Revoked session
- **WHEN** a user logs out and reuses the revoked credential
- **THEN** the API denies access

### Requirement: Private and public profiles
The system SHALL allow users to manage private profile data while exposing only explicitly public creator fields.

#### Scenario: Anonymous creator view
- **WHEN** a visitor opens an active creator profile
- **THEN** only public biography, avatar, travel summary, and later-published aggregate fields are returned

### Requirement: Controlled creator status
The system SHALL allow eligible users to enroll as creators and administrators to suspend or restore creator privileges with an audit trail.

#### Scenario: Suspended creator
- **WHEN** a suspended creator attempts a creator-only command
- **THEN** the API denies the command without altering owned content
