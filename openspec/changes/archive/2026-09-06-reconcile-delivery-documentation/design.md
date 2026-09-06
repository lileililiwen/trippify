# Design: Reconcile delivery documentation

## Ownership

Documentation only. `openspec/ROADMAP.md` and `HANDOFF.md` are edited directly.

## Decisions

`ROADMAP.md` SHALL: remove the `Current next item: wire-production-provider-credentials`
line (or repoint it at the genuine next work), add `ci-coverage-gates` to the
completed list with its commit, and align the audit-backlog status text with
`HANDOFF.md`. `HANDOFF.md` SHALL correct "eleven audit changes" to the actual
count (nine) and may reference the new gap-tracking changes
(`wire-production-checkout-gateway`, `ship-flutter-workspace-ux-entries`,
`remove-stray-flutter-openspec-artifact`).

## Failure and privacy

None. Documentation edits only.

## Rollback

Documentation only; revert the file edits.

## Test strategy

Run `openspec validate --changes` and `openspec validate --specs` to confirm
no artifact breaks. No code test required.
