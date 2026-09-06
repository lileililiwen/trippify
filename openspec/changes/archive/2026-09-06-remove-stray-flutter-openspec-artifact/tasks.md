# Tasks: Remove stray Flutter OpenSpec artifact

- [x] Confirm `apps/trippify_flutter/openspec/` is untracked and duplicates repository-root content.
- [x] Delete `apps/trippify_flutter/openspec/` and any other nested `openspec/` directory outside the repository root.
- [x] Run `git status` and confirm the stray path is gone.
- [x] Run `openspec list` to confirm CLI root resolution still resolves to the repository root.
