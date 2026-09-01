# Design: Add rendered browser and release quality gates

## Test layers

Keep unit tests for pure logic, API tests for authorization and persistence, Flutter widget tests for isolated states, and browser tests for rendered navigation and real user journeys. Browser smoke flows SHALL cover anonymous discovery, registration/sign-in, traveler library, creator guide workflow, and denied admin access.

## Database and provider drills

CI or a disposable verification job SHALL apply migrations to an empty PostgreSQL/PostGIS database and upgrade a supported prior schema. Provider tests SHALL verify unavailable payment, AI, map, storage, email, and scanner behavior. Backup tests SHALL capture, delete/lose the artifact, restore to an isolated database, and verify checksum/schema/key behavior.

## Skip policy

Skipped tests require an explicit reason and platform scope. CI SHALL report the count and fail on unapproved new skips. Existing skipped tests are retired only after replacement coverage exists.
