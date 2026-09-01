# Trippify handoff

## Next spec

`add-rendered-browser-and-release-quality-gates`

Path: `openspec/changes/add-rendered-browser-and-release-quality-gates/`

Status: planned and strictly validated; implementation has not started.

## Recently shipped

These audit changes have been merged into `openspec/specs/` and committed:

- `complete-flutter-localization-and-accessibility` (in this branch, awaiting commit)
- `complete-flutter-workspace-ux` (in this branch, awaiting commit)
- `align-flutter-api-methods-and-error-states` (commit `a5813da`)
- `fail-closed-evidence-malware-scanning` (commit `40ff68e`)
- `fix-provider-health-probes` (commit `e61bda0`)
- `make-commerce-checkout-idempotent` (commit `0c14eae`)
- `harden-cross-origin-and-runtime-secrets` (commit `4b90390`)
- `wire-production-provider-credentials` (commit `77555e6`)

The next unstarted audit change after
`add-rendered-browser-and-release-quality-gates` is
`complete-flutter-localization-and-accessibility` is now shipped.

## Objective

Add browser workflow evidence, skip accountability, PostgreSQL upgrade,
provider outage, and backup/restore release gates to the rendered Flutter
web workflows and release quality gates.

## Implementation workflow

Use this sequence for every OpenSpec change picked up from this handoff:

1. Read `AGENTS.md`, `README.md`, `openspec/config.yaml`, the active change
   folder (`openspec/changes/<name>/`), the related `specs/<capability>/`
   delta, and every affected source-of-truth spec.
2. Inspect `git status` and the live OpenSpec change list; do not silently
   start a later roadmap change.
3. Survey the existing code for the affected modules (endpoints,
   services, adapters, tests) and record reuse decisions in `design.md`
   before writing production code.
4. Write red tests first (in the matching test project) that fail without
   the planned behaviour change.
5. Implement the change top-down within the owning modules. Keep local
   modes, anonymous routes, and existing error envelopes intact.
6. Add the negative-path tests required by the spec scenarios (missing
   credentials, 401/403, timeout, transport failure, secret-safe logging,
   cross-user isolation, etc.).
7. Update `docs/<domain>.md` and the deployment notes in `README.md` when
   configuration, public contracts, or operator steps change.
8. Re-run `openspec validate <change> --strict` until the change is green.
9. Mark every task in `tasks.md` with `[x]` only after its evidence is
   real (build green, tests green, behaviour observed in HTTP).
10. Run the repository quality gates from a clean tree:
    ```sh
    dotnet build Trippify.sln
    dotnet test Trippify.sln --no-build
    ```
    (Add `flutter test` when a Flutter change is in scope.)
11. Archive the change so the spec delta merges into
    `openspec/specs/<capability>/spec.md`:
    ```sh
    openspec archive <change-name> --yes
    ```
12. Inspect the resulting `git status` and `git diff`; commit only paths
    that belong to the archived change:
    ```sh
    git add <scoped-paths>
    git commit -m "<conventional-commit-message>"
    ```
    Do not amend, do not push, and never combine another change into the
    same commit.

## Boundaries

Do not archive or commit more than one change at a time. Do not start a
later roadmap change before the previous one is archived, committed, and
reported green. Do not treat this handoff, a checked task, an OpenSpec
validation, or a passing test name as shipped evidence — only a green
build, a green test run, an archived spec delta, and a related-only
commit qualify.

A change is "shipped" only after `openspec archive` has merged the delta
into `openspec/specs/` and the related implementation, verification, and
commit are recorded in Git.
