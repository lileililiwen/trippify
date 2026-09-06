# Proposal: Ship Flutter workspace UX entries

## Problem

The archived change `complete-flutter-workspace-ux` (commit `d96e212`) claims
creator, admin, tenant, notification, import, and licensing workflows are
discoverable and responsive. In the client, those workflows are still
unimplemented: `apps/trippify_flutter/test/widget_test.dart` contains 24
`skip: true` tests, and several other test files contain additional skipped
tests (≈32 `skip:` usages across the Flutter suite). Each skipped test is
annotated `allowed-skip: <id>` whose reason defers the behavior to the
*already-archived* `complete-flutter-workspace-ux` change — so the skips are
now orphaned: the change that was supposed to deliver them has shipped, yet the
entries remain absent.

These deferred entries include: home "Discover guides" entry and the
discovery → guide author chain, planning empty/denied states and party-size
totals from the home surface, notifications and notification-preferences home
entries, plugin-catalog home entry, tenant-dashboard home entry, assisted-import
home entry, license-policies home entry, self-hosted-status home entry,
public-creator-search home entry, release-history home entry, profile entry
from signed-in shell, guide-workspace drag-to-reorder, and anonymous-home
protected tiles.

The `release-quality-gates` "Skips SHALL be accountable" requirement requires
CI to fail when a skip lacks an approved reason or the skip count increases
unexpectedly; orphaned skips pointing at a shipped change are not an approved
reason and must be resolved.

## Scope

This change implements the deferred authorized-workflow entries so they are
discoverable and responsive, and converts the orphaned `skip: true` tests into
passing, non-skipped coverage. It includes the home-surface navigation entries
listed above, responsive/accessible layouts for the affected screens, and the
matching widget tests. It excludes new backend capabilities; backend endpoints
for these workflows already exist.

## Roles and consequences

Affected roles: creator, admin, tenant operator, traveler. Server authorization
remains authoritative — client entries MUST hide or clearly deny unauthorized
capabilities and MUST NOT enforce entitlements. No secret or credential is
touched.

## Acceptance

Every authorized workflow listed above is reachable from the appropriate home
or shell surface with a responsive, accessible layout, and the previously
skipped widget tests for those entries pass without `skip:`. The Flutter skip
count does not regress and no skip cites an already-archived change.
