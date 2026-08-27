# Context
The proposed seeder spans many domain aggregates and uses known credentials. It must never run in production or overwrite developer data.

# Goals / Non-goals
- Make an empty local PostgreSQL database immediately usable for demonstrations.
- Keep demo creation opt-in and fail visibly when fixtures violate the real schema.

# Decisions
- Register and execute the seeder only when the host environment is `Development` and `DemoSeed:Enabled=true`; the default is disabled.
- Resolve the seeder in a startup scope with its scoped EF Core and Identity dependencies.
- Skip all writes when any user exists. This gives one simple, auditable empty-database boundary rather than attempting partial reconciliation.
- Create distinct users wherever uniqueness or domain policy requires distinct actors.
- Treat a seeding failure as Development startup failure rather than logging and continuing with partial state.

# Authorization, privacy, and failure modes
- Demo credentials are documented as local-only and are never enabled by Production configuration.
- The dataset contains fictional content only and no provider secrets.
- Cancellation or constraint failure is surfaced; a subsequent run against any partially populated database skips instead of adding more rows, so developers should recreate the local database after a failed seed.

# Migration and rollback
- No migration. Disable `DemoSeed:Enabled` or recreate the local development database to remove demo data.
