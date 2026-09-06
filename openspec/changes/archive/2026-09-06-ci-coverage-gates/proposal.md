# Proposal: Add coverage gates to CI
## Why
quality.yml runs dotnet+Postgres, flutter analyze/test, and a migration up/down drill, but has no coverage gate for either the dotnet or Flutter code.
## What Changes
- Add Coverlet to the dotnet test projects with a threshold, and add Flutter coverage to the flutter job.

## Capabilities
### New Capabilities
- `ci-coverage-gates`: coverage is enforced across the dotnet and Flutter code.

### Modified Capabilities
None.

## Impact
Affects: quality.yml, dotnet test csprojs.
