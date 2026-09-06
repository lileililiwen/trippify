# Repository hygiene

## REMOVED Requirements

### Requirement: OpenSpec artifacts SHALL live only at the repository root

Planning artifacts MUST NOT be duplicated inside client or module trees, so
the OpenSpec CLI resolves a single root and spec tooling is not leaked into
build outputs.

#### Scenario: Nested OpenSpec folder in client tree

- **Given** a planning folder exists under `apps/<client>/openspec/`
- **When** a contributor or agent runs the OpenSpec CLI
- **Then** only the repository-root OpenSpec folder is authoritative
- **And** the nested copy is removed so the two cannot drift
