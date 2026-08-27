# Why
The development seeder skips whenever any user exists. A developer who registered a normal account before enabling demo data receives neither demo credentials nor catalog fixtures.

# What Changes
Allow demo fixtures to be added alongside unrelated existing users while preserving every existing row, retain repeat-run idempotency, and fail clearly on a partial/conflicting demo identity set.

# Capabilities
## Modified Capabilities
- `development-demo-data`: replace the any-user skip boundary with demo-identity-aware coexistence.

# Dependencies and Non-goals
- Dependencies: `development-demo-data`.
- Non-goals: merging demo fixtures into existing accounts, overwriting profiles, or repairing a partially seeded demo dataset automatically.

# Impact
Changes seeder guards, tests, and README reset/troubleshooting guidance. No schema or production behavior changes.
