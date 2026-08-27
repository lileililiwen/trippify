# flutter navigation shell Specification

## ADDED Requirements

### Requirement: Valid role-aware destination mapping
The Flutter shell SHALL derive navigation selection and callbacks from the same visible role-aware destination list and SHALL never pass an out-of-range selected index.

#### Scenario: Creator route opens before summary resolves
- **WHEN** `/guides` renders while the authenticated creator summary is still loading
- **THEN** the page does not throw an index error or show an incorrect role-dependent destination

#### Scenario: Creator selects Create
- **WHEN** a resolved creator taps the visible Create destination
- **THEN** the shell emits `SignedInDestination.create` and selects the Create destination

#### Scenario: Traveler selects Plan
- **WHEN** a resolved non-creator taps the visible Plan destination
- **THEN** the shell emits `SignedInDestination.plan` and selects the Plan destination
