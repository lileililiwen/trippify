# Tasks: Ship Flutter workspace UX entries

- [x] Enumerate every `skip: true` test in `apps/trippify_flutter/test` and group by deferred entry (discover chain, planning states/totals, notifications, plugins, tenant, import, license, self-hosted, creator search, release history, profile, guide-workspace reorder, anonymous protected tiles).
- [x] Add the deferred home/shell navigation entries for the authenticated role(s) they belong to, reusing existing bottom-navigation surfaces where applicable.
- [x] Ensure unauthorized deep links render an access-denied state; server authorization remains authoritative.
- [x] Implement responsive and accessible layouts for the affected screens (readable at mobile and web widths, keyboard/semantics, contrast).
- [x] Convert each orphaned `skip: true` test into a passing, non-skipped assertion and remove its `allowed-skip` reason referencing the archived change.
- [x] Run `flutter test`; confirm the skip count does not increase and the CI skip-accountability gate stays green.
- [x] Run `flutter analyze` and fix warnings introduced by the new navigation entries.
