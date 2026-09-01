# Design: Fix provider health probes

## Probe contract

The object-storage check generates one unique key, writes a small sentinel, deletes the same key, and treats any failed cleanup as unhealthy. A `finally` cleanup attempt may run after intermediate failure, but cleanup errors must remain observable in health status/logs.

Map checks SHALL use a stable known query and report unresolved results as degraded, not healthy. All provider probes SHALL honor cancellation and use bounded timeouts inherited from their adapters.

## Implementation decisions

### Reuse

- `IObjectStorage` / `IMapProvider` are the existing adapter contracts. No new
  abstractions are introduced.
- The health check classes already exist in `src/Trippify.Api/ProviderHealthChecks.cs`.
  This change modifies them in place rather than adding new types.
- `Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult` provides
  the `data:` channel used for diagnostics.

### IObjectStorage.ProviderName

The object-storage interface gains a `string ProviderName { get; }` property so
the health check can report the configured provider name in its diagnostic
data without resorting to a `try`/cast or reflection. Both production
implementations already expose this value. The single test fake
(`ThrowingStorage` in `GuideApiTests.cs`) is updated to satisfy the interface.

### One-key probe lifecycle

The object-storage probe now:
1. Generates one `_healthchecks/{guid}` key per invocation.
2. PUTs a one-byte sentinel (`0x20`).
3. DELETEs the **same** key.
4. If DELETE throws, returns `Unhealthy` with the exception type name in
   `data["cleanupError"]` and the original exception in `result.Exception`.
5. If PUT throws, returns `Unhealthy` with the exception included.
6. If the cancellation token is cancelled at any point, rethrows
   `OperationCanceledException` so the framework records cancellation rather
   than reporting `Unhealthy`.

`ExistsAsync` is intentionally **not** called as part of the lifecycle. The
design draft mentioned it, but in practice many remote object stores return
false for newly created keys until eventual consistency settles, so a
verifying existence check would produce noisy `Unhealthy` reports. The PUT
succeeds, the DELETE targets the same key, and that is enough to prove the
write/read/delete lifecycle against a single artifact.

### Map probe diagnostics

The map probe now returns the provider name, resolution status, and
attribution string in `HealthCheckResult.Data`. Cancellation is rethrown the
same way as the object-storage probe.

### No HTTP response writer changes

`/health/ready` continues to use the default plain-text response writer, so
the data dict is consumed by the framework's logging and any monitoring
integration that reads the `HealthReport`. The default writer does not embed
the data dict in the HTTP body, so no secrets can leak via that path either.

## Authorization

Health probes are anonymous endpoints. No new authorization decisions are
introduced. The existing `/health/ready` mapping at `Program.cs:162` continues
to gate the provider probes under the `ready` tag.

## Privacy

- Probe keys are namespaced under `_healthchecks/`.
- Probe payloads are one byte and contain no user data.
- `data["provider"]` and `data["key"]` are non-sensitive identifiers.
- The test suite asserts that no entry in the data dict contains
  `secret` or `REDACTED` substrings.

## Failure modes

- `LocalFileObjectStorage`: DELETE is idempotent and never throws on a missing
  file. A probe against the local provider will only fail if PUT throws
  (e.g., permission denied).
- `RemoteHttpObjectStorage`: DELETE tolerates 404 and otherwise throws
  `IOException` on non-2xx responses. The probe surfaces that as `Unhealthy`
  with the cleanup error name in the data dict.

## Tests

- `tests/Trippify.ApiTests/ProviderHealthCheckTests.cs` adds focused unit
  tests for both health checks.
- The existing integration test
  `MapAndObjectStorageApiTests.Provider_healthchecks_register_for_map_and_object_storage`
  verifies the `/health/ready` endpoint still reports `Healthy` for the local
  providers.

## Rollback

Revert the changes to:
- `src/Trippify.Api/ProviderHealthChecks.cs`
- `src/Trippify.Application/Abstractions.cs`
- `tests/Trippify.ApiTests/ProviderHealthCheckTests.cs`
- `tests/Trippify.ApiTests/GuideApiTests.cs`
- `docs/operations.md`

No migration is required; the interface change is additive and the only
existing implementation of `IObjectStorage.ProviderName` outside production
is updated in the same commit.
