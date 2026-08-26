# Context
The repository is empty and must support an open-source deployment and a hosted product without premature microservices.

# Goals / Non-goals
- Goal: modular, testable API and Flutter foundations with replaceable external services.
- Non-goal: distributed services or provider lock-in.

# Decisions
- Use a modular monolith on supported LTS ASP.NET Core, EF Core, PostgreSQL, and PostGIS.
- Define module boundaries and OpenAPI contracts; Flutter depends on contracts, not database shapes.
- Use abstractions for object storage, mail, maps, payments, AI, clock, and background jobs.
- Apply structured logs, traces, metrics, health checks, secret validation, and problem-details errors.

# Risks / Trade-offs
- Module boundaries require architecture tests; PostGIS availability requires startup checks and documented local provisioning.
