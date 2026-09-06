# Proposal: Remove stray Flutter OpenSpec artifact

## Problem

A stray OpenSpec artifact lives inside the Flutter client tree:
`apps/trippify_flutter/openspec/changes/complete-flutter-localization-and-accessibility/tasks.md`.
It is untracked (`git status` shows `?? apps/trippify_flutter/openspec/`) and
duplicates planning content that already lives in the repository-root
`openspec/`. Having a nested `openspec/` folder inside the app can confuse the
`openspec` CLI root resolution, leak spec tooling into the client build tree,
and create drift between the two copies.

## Scope

This is a repository-hygiene change. It deletes the `apps/trippify_flutter/openspec/`
directory and confirms no other nested `openspec/` folders exist outside the
repository root. It introduces no application code, migration, or test.

## Roles and consequences

No runtime, security, or financial consequence. Only contributors and the
`openspec` CLI are affected.

## Acceptance

`apps/trippify_flutter/openspec/` no longer exists; `git status` is clean of
the stray path; `openspec list` resolves to the repository-root OpenSpec root.
