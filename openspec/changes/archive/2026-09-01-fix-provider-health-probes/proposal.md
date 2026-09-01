# Proposal: Fix provider health probes

## Why

The object-storage readiness check writes one random key and deletes another, so it does not verify cleanup and leaves probe artifacts behind. The map check returns no diagnostics and silently swallows cancellation. Production operators need clean-up verification, observable failure modes, and probe contracts that do not leak credentials.

## What Changes

- Object-storage probe uses one key for its full write/delete lifecycle, reports cleanup failure as unhealthy, and surfaces provider name + cleanup error type in the diagnostic data.
- Map probe reports `Degraded` (not `Healthy`) for an unresolved known query, surfaces provider/status/attribution in the data, and rethrows `OperationCanceledException` when the caller cancels the readiness token.
- `IObjectStorage` gains a `ProviderName` property so the probe can report the configured provider without reflection or try/cast. Both production implementations and the test fake are updated in the same change.
- Diagnostics NEVER include credentials, signed-URL secrets, access keys, or user content.

## Problem

The object-storage readiness check writes one random key and deletes another, so it does not verify cleanup and leaves probe artifacts behind.

## Scope

Use one deterministic per-check key for the complete write/read-or-existence/delete lifecycle, preserve cancellation, and add provider-specific assertions. This excludes a new monitoring platform.

## Acceptance

Successful probes leave no object; failed delete/read operations degrade or fail readiness; concurrent probes use isolated keys; diagnostics expose provider/status without secrets.
