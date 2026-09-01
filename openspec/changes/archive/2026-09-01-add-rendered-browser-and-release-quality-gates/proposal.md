# Proposal: Add rendered browser and release quality gates

## Problem

The repository has broad API tests but no rendered-browser gate, 32 skipped Flutter widget tests, no visible PostgreSQL migration upgrade drill in CI, and no end-to-end provider/backup release evidence.

## Scope

Add machine-checkable gates for Flutter web rendering, role workflows, responsive/accessibility smoke checks, skipped-test accounting, PostgreSQL migration upgrade, provider failures, and backup-delete-restore. This excludes production infrastructure provisioning and a complete visual regression platform.

## Acceptance

CI fails on broken rendered flows, newly skipped tests, migration failures, unsafe provider success, or backup/restore regressions. Release evidence identifies environment limitations instead of claiming unsupported coverage.
