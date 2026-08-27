# Tasks

## 1. Flutter implementation
- [x] 1.1 Use a single role-aware destination mapping for rendering, selected index, and tap callbacks.
- [x] 1.2 Suppress role-dependent navigation while an authenticated summary is unresolved and provide a valid fallback for incompatible routes.

## 2. Verification
- [x] 2.1 Test `/guides` first-frame loading without range errors and creator Create selection after summary resolution.
- [x] 2.2 Test that tapping Create emits `SignedInDestination.create` and non-creator Plan still emits `plan`.
- [x] 2.3 Run strict OpenSpec validation, Flutter analyze/tests, formatting, and diff gates.
