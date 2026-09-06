## ADDED Requirements
### Requirement: Coverage enforcement
CI SHALL enforce coverage thresholds for the dotnet and Flutter code.
#### Scenario: Coverage low
- **WHEN** coverage is below the threshold
- **THEN** CI fails the coverage gate
#### Scenario: Coverage ok
- **WHEN** coverage meets the threshold
- **THEN** CI passes
