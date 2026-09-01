# flutter-api-contract Specification

## Purpose
TBD - created by archiving change align-flutter-api-methods-and-error-states. Update Purpose after archive.
## Requirements
### Requirement: Flutter SHALL support every declared HTTP method

The Flutter client MUST dispatch `PATCH` requests with JSON body and authentication consistently with the API contract.

#### Scenario: Update trip

- **Given** an authenticated user edits a trip
- **When** the client calls `updateTrip`
- **Then** it sends one authenticated PATCH request to the trip endpoint
- **And** it parses the returned trip

### Requirement: Client errors SHALL be actionable

The client MUST distinguish validation, conflict, authorization, payment, rate-limit, server, and network failures and render a safe actionable state.

#### Scenario: Stale trip update

- **Given** the API returns 409 for a stale update
- **When** the client receives the response
- **Then** it tells the user the data changed and offers reload
- **And** it does not claim the save succeeded

#### Scenario: Protected request receives 401

- **Given** a protected request returns 401
- **When** the client handles the response
- **Then** local credentials are cleared
- **And** the session returns to the anonymous state without an infinite retry loop

