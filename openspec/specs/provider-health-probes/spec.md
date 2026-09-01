# provider-health-probes Specification

## Purpose
Readiness probes for object storage and the map provider MUST verify the configured provider against a single, namespaced probe artifact, MUST report cleanup failures as unhealthy, MUST NOT leak credentials in the health response, and MUST honor caller cancellation. The contract covers the `/health/ready` checks `object-storage` and `map-provider`.
## Requirements
### Requirement: Object-storage probes SHALL verify the same artifact

An object-storage readiness probe MUST use one namespaced unique key for its write, verification, and delete operations and MUST report cleanup failure as unhealthy.

#### Scenario: Successful probe cleans up

- **Given** object storage accepts writes, existence checks, and deletes
- **When** readiness executes
- **Then** the same probe key is used for write and delete
- **And** the key does not remain afterward

#### Scenario: Delete fails

- **Given** the provider accepts the write but rejects deletion
- **When** readiness executes
- **Then** the check is unhealthy or degraded according to the readiness policy
- **And** the failure is observable to operators

### Requirement: Probes SHALL not expose secrets

Health responses and logs MUST omit credentials, authorization headers, private object URLs, and user content.

#### Scenario: Provider returns an authenticated failure

- **Given** a provider rejects a probe request
- **When** the health result is emitted
- **Then** it contains provider/status diagnostics only
- **And** no secret or authorization value is present

### Requirement: Map probes SHALL report degraded when the known query is unresolved

The map-provider readiness probe MUST use a single stable known query, MUST report a `Degraded` result (not `Healthy`) when the provider returns an unresolved status, and MUST include the provider name and resolution status in the diagnostic data.

#### Scenario: Known query resolves

- **Given** the map provider returns a resolved status for the probe query
- **When** readiness executes
- **Then** the check is `Healthy`
- **And** the data includes the provider name and `Resolved` status

#### Scenario: Known query is unresolved

- **Given** the map provider returns an unresolved status for the probe query
- **When** readiness executes
- **Then** the check is `Degraded`
- **And** the data includes the provider name and `Unresolved` status

### Requirement: Provider probes SHALL honor cancellation and adapter timeouts

Provider probe implementations MUST propagate `OperationCanceledException` when the supplied cancellation token is cancelled and MUST rely on adapter-level bounded timeouts rather than introducing a new timeout.

#### Scenario: Caller cancels the probe

- **Given** the caller cancels the readiness token
- **When** the probe runs
- **Then** the call throws `OperationCanceledException`
- **And** the framework does not record the result as `Unhealthy`

