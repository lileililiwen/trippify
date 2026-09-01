# Tasks: Fix provider health probes

- [x] Add failing tests asserting object-storage PUT and DELETE receive the same key.
- [x] Add tests for existence failure, delete failure, cancellation, concurrent probes, and no leaked artifacts.
- [x] Implement one-key probe lifecycle with bounded cleanup and provider-safe diagnostics.
- [x] Review map health behavior for unresolved and timeout outcomes and add focused tests.
- [x] Run health, API, architecture, and full solution tests; update operations documentation and archive after verification.
