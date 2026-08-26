# Why
The product needs a deployable, secure base before domain features can be delivered without coupling clients to infrastructure choices.

# What Changes
- Create a modular ASP.NET Core API with PostgreSQL/PostGIS persistence and OpenAPI.
- Create Flutter application shells consuming generated or typed API contracts.
- Establish security, media, jobs, telemetry, health, and configuration abstractions.

# Capabilities
## New Capabilities
- `platform-foundation`: A production-oriented runtime and development baseline.

# Dependencies and Non-goals
- Dependencies: none.
- Non-goals: user journeys, guide behavior, marketplace behavior, and provider-specific integrations.

# Impact
Introduces solution structure, CI, local orchestration, baseline migrations, and client shells.
