# Design: Ship Flutter workspace UX entries

## Ownership

`apps/trippify_flutter/lib` owns the home/shell navigation, workspace screens,
and widget tests. No backend change is required; existing endpoints already
serve creator, admin, tenant, notifications, plugins, import, licensing, and
self-hosted data.

## Decisions

The home/shell navigation SHALL expose the deferred entries listed in the
proposal as discoverable tiles or bottom-navigation items appropriate to the
authenticated role, reusing the existing bottom-navigation surfaces where the
reason text already states the entry "is exposed via the bottom navigation."
Server authorization stays authoritative: entries for unauthorized capabilities
MUST be hidden or render an access-denied state, never client-gated.

Each orphaned `skip: true` test in `widget_test.dart` and the other Flutter
test files SHALL be un-skipped and made to pass once its entry is wired. The
`allowed-skip` reason that references the archived `complete-flutter-workspace-ux`
change SHALL be removed as part of enabling the test.

## Failure and privacy

Navigation to an unauthorized deep link MUST show an access-denied state with a
safe return action and MUST NOT render protected data. No PII is introduced by
these UI entries.

## Rollback

UI-only change; rollback is a client redeploy. No migration or data change.

## Test strategy

Convert the skipped widget tests to passing assertions for each entry
(discover chain, planning states/totals, notifications, plugins, tenant,
import, license, self-hosted, creator search, release history, profile,
guide-workspace reorder, protected anonymous tiles). Run `flutter test` and
confirm the skip count does not increase and CI skip-accountability remains
green.
