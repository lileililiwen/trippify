# Proposal: Reconcile delivery documentation

## Problem

The shipped-work record and the delivery roadmap have drifted apart, which
hides real gaps (see the related `wire-production-checkout-gateway` and
`ship-flutter-workspace-ux-entries` changes) and misleads agents about what is
next:

- `openspec/ROADMAP.md:83` still declares *"Current next item:
  `wire-production-provider-credentials`"* even though that change shipped in
  commit `77555e6`, and the whole audit backlog (items 27-35) is labeled the
  "next delivery sequence" while `HANDOFF.md` states it is fully shipped.
- The committed, archived change `ci-coverage-gates` (HEAD `62f105e`, with
  `openspec/specs/ci-coverage-gates/`) is **not listed anywhere in
  `ROADMAP.md`**, so a shipped change has no roadmap trace.
- `HANDOFF.md` states *"All eleven audit changes from the September 2026
  backlog are now in `openspec/specs/`"*, but only **nine** audit changes exist
  (roadmap items 27-35). The count is wrong.

## Scope

This is a documentation-only change. It corrects `ROADMAP.md` (remove the
stale next-item pointer, record `ci-coverage-gates`, and reconcile the audit
backlog status against `HANDOFF.md`) and corrects the "eleven" count in
`HANDOFF.md`. It introduces no application code, migration, or test. It does
not implement any deferred behavior; those are tracked by their own changes.

## Roles and consequences

Affects only contributors and agents reading the delivery plan. No runtime,
security, or financial consequence.

## Acceptance

`ROADMAP.md` no longer points at a shipped change as "next", lists
`ci-coverage-gates`, and matches `HANDOFF.md`; the audit-change count is
correct. `openspec validate` remains green.
