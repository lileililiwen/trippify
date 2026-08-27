# Tasks

## 1. Domain model and persistence

- [x] 1.1 Add `TripEvidence`, `EvidenceReview`, `VerifiedBadge`, `ActualTripMetric` entities and EF configurations in `src/Trippify.Infrastructure/Verified.cs`.
- [x] 1.2 Register the new `DbSet<>` properties on `AppDbContext` in `src/Trippify.Infrastructure/Persistence.cs`.
- [x] 1.3 Add forward-only EF Core migration `AddVerifiedTripEvidence` with unique indexes, foreign keys, and 90-day `RetentionDeadline` index for cleanup.
- [x] 1.4 Wire `LocalProviders`, `IClock`, and the new entities into `src/Trippify.Api/Program.cs`.

## 2. ASP.NET Core API surface

- [x] 2.1 Create `src/Trippify.Api/VerifiedTripEndpoints.cs` with command/query endpoints, role gates, validation, audit entries, and idempotency tokens.
- [x] 2.2 Expose `MapVerifiedTrips()` from `Program.cs` after `MapReview()`.
- [x] 2.3 Authorize evidence visibility so only the submitter, the guide creator, and `Administrator` role can read raw evidence and bodies.

## 3. Flutter client experience

- [x] 3.1 Add evidence, badge, and insight endpoints to `apps/trippify_flutter/lib/api_client.dart` with typed models.
- [x] 3.2 Add the "Verify trip" submit form, badge display, and metrics widget to `PublicGuideScreen` in `apps/trippify_flutter/lib/main.dart` (loading, empty, success, error, denied states).
- [x] 3.3 Cover the new screens with accessibility-aware widgets tests in `apps/trippify_flutter/test/widget_test.dart`.

## 4. Concurrency, idempotency, privacy, retention, upgrades

- [x] 4.1 Add negative-path tests in `tests/Trippify.ApiTests/VerifiedTripApiTests.cs` for duplicate evidence, reviewer conflicts, retention deletion, role denial, and forbidden raw evidence reads.
- [x] 4.2 Cover coarse metrics aggregation rules (min party size, min count, k-anonymity bucket) and badge revocation on guide unpublish.

## 5. OpenAPI, telemetry, documentation, quality gates

- [x] 5.1 Confirm `swashbuckle` surfaces the verified-trip endpoints with proper request/response schemas.
- [x] 5.2 Emit `Trippify.Verified` meter counters with operation tags and assert bodies never appear in telemetry.
- [x] 5.3 Author `docs/verified-trips.md` and reference it from `README.md`.
- [x] 5.4 Run `dotnet build` and `dotnet test`, plus `flutter analyze` and `flutter test`, and resolve any failures.
