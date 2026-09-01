# Proposal: Wire production provider credentials

## Problem

The HTTP payment, AI, and S3-compatible storage adapters validate that credentials exist but send the literal value `REDACTED` in their Authorization headers. A configured production provider therefore cannot authenticate requests. The current behavior is especially dangerous because configuration validation gives operators the impression that the integration is ready.

## Scope

This change replaces placeholder outbound authentication with provider-specific credential injection for payment, AI, and object storage. It includes configuration validation, redacted logging, and deterministic adapter tests. It excludes provider-specific SDKs, payment settlement behavior, and changes to local-mode disabled adapters.

## Roles and consequences

Only server-side adapters and operators are affected. Secrets remain server-only and must never appear in responses, telemetry, exceptions, or test snapshots. A credential rejection or missing credential must fail the operation safely and must not fabricate success.

## Acceptance

Configured providers authenticate successfully against a request-capturing fake server; wrong or missing credentials produce a controlled provider failure; local providers remain explicitly disabled.
