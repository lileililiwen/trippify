# Design: Wire production provider credentials

## Ownership

`Trippify.Infrastructure` owns provider options and outbound HTTP adapters. `Trippify.Api` owns startup validation and translates adapter failures into safe HTTP results. Tests use a local in-process HTTP handler or fake server and never use real credentials.

## Decisions

Payment and AI adapters SHALL send the configured API key using the selected provider authentication scheme. The S3-compatible adapter SHALL use the configured access/secret credentials through the repository's documented compatible request-signing mechanism; if the current remote protocol is bearer-token based, its bearer value SHALL come from configuration rather than a placeholder. The exact secret SHALL never be logged.

Options SHALL normalize provider names and reject enabled remote providers whose required credential fields are absent. Configuration errors SHALL fail startup before traffic is accepted. Local mode keeps the existing disabled result/503 behavior.

## Failure and privacy

401/403 responses are provider failures, not successful empty results. Logs may contain provider name and status only. Request bodies, Authorization headers, API keys, and signed URLs are excluded from logs and telemetry.

## Rollback

Rollback is configuration-safe: operators can select local mode, but production deployments must not silently fall back to local or placeholder authentication.
