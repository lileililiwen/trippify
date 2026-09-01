# Tasks: Align Flutter API methods and error states

- [x] Add a failing request-dispatch test proving `updateTrip` currently cannot send PATCH.
- [x] Implement PATCH and add tests for JSON body, authorization, empty response, 401 token clearing, and non-2xx mapping.
- [x] Extend `AppError` for validation, conflict, and rate-limit states and update affected screen copy/actions.
- [x] Add retry/cancellation/disposal tests for trip, discovery, checkout, upload, and import flows.
- [x] Review the 32 skipped widget tests; implement prerequisites and remove skips or document concrete unsupported platform reasons.
- [x] Run `flutter format`, `flutter analyze`, `flutter test`, and API contract tests; archive after verification.
