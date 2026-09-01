# Tasks: Add rendered browser and release quality gates

- [x] Select and document the supported Flutter web/browser runner and stable test data/bootstrap strategy.
- [x] Add browser smoke tests for anonymous, traveler, creator, administrator-denied, responsive, and accessibility-critical journeys.
- [x] Add CI reporting/failure rules for skipped tests and Flutter analyzer issues that affect release quality.
- [x] Add disposable PostgreSQL empty-schema and previous-release migration verification.
- [x] Add provider outage and fail-closed evidence tests at API/integration level.
- [x] Add isolated backup capture-delete-restore verification with checksum, encryption-key, and schema mismatch cases.
- [x] Run all release gates, document environment limitations, and archive only after reproducible evidence is captured.

## Subtasks (verifiable claims)

### Skip accountability
- [x] Add `tool/check_flutter_skips.dart` and `tool/skip_allowlist.json` baseline; wire into CI; failing test for unapproved skip.
- [x] Convert the existing 24 `widget_test.dart` skip markers into structured `// allowed-skip: <id>` annotations paired with the allowlist.
- [x] Add a unit test that drives the skip checker with a stubbed file and asserts the unapproved-skip path exits non-zero.

### Browser smoke coverage
- [x] Add `integration_test` dev_dependency and a `integration_test/release_smoke_test.dart` covering anonymous, traveler, creator, denied-administrator, responsive layout, and accessibility-critical journeys.
- [x] Update the Flutter CI step to run the web platform tests; document the Chrome requirement and the local fallback to the existing widget tests.

### Provider outage & fail-closed
- [x] Add `ProviderOutageApiTests.cs` covering remote payment, AI, map, object-storage, email, and evidence-scanner unavailable paths; verify the API fails safely without fabricating success.
- [x] Add `RestorableBackupTests.cs` exercising capture, delete-the-artifact, tamper, wrong-key, and schema-mismatch paths; verify restore never overwrites the live database and surfaces the failure to the operator.

### Migration drill
- [x] Add `PostgresMigrationUpgradeTests.cs` that walks the EF Core migration list and confirms every migration defines a non-empty `Up` body with no shadow migrations.
- [x] Add `scripts/postgres-upgrade-drill.sh` that applies migrations to a disposable PostgreSQL, simulates a previous release by replaying through `dotnet ef database update <previous>`, upgrades to head, and runs a smoke request; the script is invoked from CI in `--if-available` mode.

### CI integration
- [x] Update `.github/workflows/quality.yml` to run the skip check, the Flutter web smoke step, and the Postgres upgrade drill; document the environment limitations in `docs/operations.md`.
