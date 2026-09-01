# runtime-security Specification

## Purpose
TBD - created by archiving change harden-cross-origin-and-runtime-secrets. Update Purpose after archive.
## Requirements
### Requirement: Credentialed CORS SHALL be allowlisted

The API MUST allow credentialed cross-origin requests only from explicitly configured exact origins. It MUST NOT reflect arbitrary origins or combine wildcard origins with credentials.

#### Scenario: Configured origin is accepted

- **Given** `https://app.example` is in the configured origin list
- **When** it sends a credentialed API request
- **Then** the response includes the matching CORS headers

#### Scenario: Unconfigured origin is denied

- **Given** `https://attacker.example` is not configured
- **When** it sends a preflight or credentialed API request
- **Then** the API does not grant credentialed CORS access

### Requirement: Production secrets SHALL be validated

Production MUST reject missing, default, or known-weak signing secrets required for configured features.

#### Scenario: Weak object-storage secret is rejected

- **Given** production uses object storage with the Compose example secret
- **When** the API starts
- **Then** startup fails with a configuration error

