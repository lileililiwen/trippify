# Design: Harden cross-origin and runtime secrets

## CORS

When `Cors:AllowedOrigins` is empty in Development, the API may use a documented non-credentialed development policy. In Staging/Production, an empty list MUST fail startup. `AllowCredentials()` MUST never be combined with wildcard origin reflection. Origins SHALL be exact scheme/host/port values and SHALL be normalized before registration.

## Secrets

Production SHALL require non-empty, high-entropy values for object-storage signing, backup encryption/operator protection where configured, payment webhooks, and other signing secrets. Example Compose files SHALL use variable references or visibly unusable placeholders that production startup rejects. Secrets never enter health responses, logs, or client configuration.

## Verification

Use `WebApplicationFactory` for origin behavior and a startup configuration matrix for development versus production. Test allowed, denied, missing-origin, and credentialed preflight requests.
