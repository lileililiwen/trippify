# durable background processing Specification

## ADDED Requirements

### Requirement: Durable job lifecycle
The system SHALL persist background work before acknowledging it and SHALL execute due work with leases, bounded retries, and terminal failure visibility.

#### Scenario: API process restarts after enqueue
- **WHEN** a committed job has not completed before the process restarts
- **THEN** an available worker later claims and executes that job

#### Scenario: A handler repeatedly fails
- **WHEN** a job exhausts its configured attempts
- **THEN** it enters a dead-letter state without being reported as completed

### Requirement: Idempotent domain handlers
The system SHALL make retention and notification handlers safe under at-least-once execution.

#### Scenario: An expired-evidence cleanup is replayed
- **WHEN** the same cleanup job executes more than once
- **THEN** evidence is removed once and badge counts remain correct

#### Scenario: A user disables notifications before execution
- **WHEN** a queued notification becomes due after its recipient opts out
- **THEN** delivery is suppressed according to the current preference
