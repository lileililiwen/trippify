# Why
Developers need an opt-in, realistic dataset to exercise the completed product surfaces without manually creating every aggregate. The partial seeder is not yet covered by a specification or verified against PostgreSQL constraints.

# What Changes
Add development-only demo data with documented credentials, explicit configuration, empty-database protection, deterministic coverage of major roles and product surfaces, and PostgreSQL-backed verification.

# Capabilities
## New Capabilities
- `development-demo-data`: safe, opt-in, idempotent demo fixtures for local development.

# Dependencies and Non-goals
- Dependencies: the completed foundation, marketplace, trust, and distribution schemas.
- Non-goals: production bootstrap, test data for load testing, schema migration, or modifying a populated database.

# Impact
Adds an infrastructure seeder, Development startup wiring, API tests, test isolation configuration, and README documentation. No production behavior or database schema changes.
