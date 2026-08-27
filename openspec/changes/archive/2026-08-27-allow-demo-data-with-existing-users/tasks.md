# Tasks

## 1. Seeder behavior
- [x] 1.1 Replace the any-user guard with a normalized known-demo-identity check.
- [x] 1.2 Seed alongside unrelated users without updating or deleting their records.
- [x] 1.3 Skip a complete existing demo set and reject a partial/conflicting set with actionable reset guidance.

## 2. Verification and documentation
- [x] 2.1 Test unrelated-user coexistence, repeated execution, complete-demo skip, and partial-demo rejection.
- [x] 2.2 Verify migrated PostgreSQL seeding and documented demo login/catalog access with a pre-existing user.
- [x] 2.3 Update README troubleshooting and run strict OpenSpec, build, test, formatting, and diff gates.
