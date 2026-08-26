# Platform Foundation Specification

## ADDED Requirements

### Requirement: Deployable application foundation
The system SHALL provide a versioned ASP.NET Core API, PostgreSQL/PostGIS persistence, and Flutter client shells that can be built and run from documented commands.

#### Scenario: Clean environment startup
- **WHEN** an operator follows the documented clean-install procedure
- **THEN** migrations complete, readiness succeeds, and the Flutter client can call the API

### Requirement: Enforced module and contract boundaries
The system SHALL prevent domain modules from bypassing declared application contracts and SHALL publish an OpenAPI contract for client integration.

#### Scenario: Boundary violation
- **WHEN** code introduces a forbidden cross-module dependency
- **THEN** an automated architecture gate fails

### Requirement: Secure and observable defaults
The system SHALL deny unauthorized access, validate configuration without exposing secrets, return consistent errors, and emit correlated logs, traces, metrics, and health signals.

#### Scenario: Invalid production configuration
- **WHEN** a required secret or provider configuration is missing
- **THEN** startup fails with a non-secret diagnostic
