# Tasks

## 1. Seeder implementation
- [x] 1.1 Implement scoped, cancellation-aware demo seeding for traveler, creator, and administrator roles plus representative completed-domain aggregates.
- [x] 1.2 Use distinct actors and valid relationships for every unique index, foreign key, lifecycle, entitlement, and privacy invariant.
- [x] 1.3 Skip a non-empty user database without changing data and fail startup visibly on invalid partial seeding.

## 2. Configuration and documentation
- [x] 2.1 Register and run the seeder only in `Development` when `DemoSeed:Enabled=true`, with disabled as the default.
- [x] 2.2 Keep normal API test factories isolated by explicitly disabling demo seeding.
- [x] 2.3 Document enablement, reset procedure, fictional accounts, shared local-only password, and production exclusion in README.

## 3. Verification
- [x] 3.1 Test empty-database population, repeated execution, non-empty-database preservation, role assignment, and representative relationships.
- [x] 3.2 Verify the seeder against migrated PostgreSQL so unique indexes and foreign keys are enforced, not only EF InMemory behavior.
- [x] 3.3 Run strict OpenSpec validation, backend build/tests, formatting/analyzers, and diff checks.
