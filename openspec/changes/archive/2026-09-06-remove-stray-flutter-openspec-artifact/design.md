# Design: Remove stray Flutter OpenSpec artifact

## Ownership

Repository hygiene. No module owns this; it is a one-off cleanup.

## Decisions

Delete `apps/trippify_flutter/openspec/` entirely (the single file under it is a
copy of content already present in the repository-root `openspec/`). Verify no
other nested `openspec/` directory exists under `apps/` or `src/`. No
`.gitignore` or build change is required.

## Failure and privacy

None.

## Rollback

Recreate the deleted folder from Git history if needed (it was untracked, so
restore from the prior working copy).

## Test strategy

Run `git status` and `find . -name openspec -type d` (excluding
`node_modules`) to confirm a single OpenSpec root. Run `openspec list` to
confirm CLI resolution is unaffected.
