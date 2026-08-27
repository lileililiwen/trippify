# Assisted import and translation

Authors need help translating and reformatting source material without giving the AI the keys to the kingdom. This slice adds queued text/photo/video imports, generated drafts with provenance, owner-driven review, linked translations, and per-user quotas.

## Imports

- `POST /api/v1/me/imports/text { sourceText }` queues a text import (30–20 000 characters). `IAiAssistant.AssistAsync` drafts an `ImportDraft` immediately; the job leaves `Queued → Processing → Completed` synchronously so reviewers can act without waiting for a background scheduler.
- `POST /api/v1/me/imports/object { objectKey, kind }` queues an object-backed import; `kind` is parsed to `Photo`/`Video` (defaults to `Photo`).
- `GET /api/v1/me/imports?limit=N` lists the caller's jobs newest-first (`limit` clamped to `1`-`100`).
- `GET /api/v1/me/imports/{jobId}` returns the job and, when completed, the associated draft.
- `POST /api/v1/me/imports/{jobId}/process` is a no-op for completed jobs and re-runs processing for queued ones (idempotent on completed).

## Draft review

- `POST /api/v1/me/drafts/{draftId}/approve { guideId? }` flips the draft to `Approved`. **No automatic publish** — the draft stays private until a future slice wires the guide editor.
- `POST /api/v1/me/drafts/{draftId}/reject` flips the draft to `Rejected`.
- Drafts store provenance via `provenanceJson` (`{ job, kind, generatedAt }`) so reviewers can trace every node back to the import.

## Translations

- `POST /api/v1/me/translations { sourceDraftId, locale, body }` stores a translation linked to the source draft. Re-submitting the same `(sourceDraftId, locale)` updates the body and marks the translation `Outdated`. The unique index `(SourceDraftId, Locale)` enforces the link in the database even under concurrent requests.
- `GET /api/v1/me/translations` returns the caller's translations newest-first.
- Translation bodies are run through `IAiAssistant.AssistAsync` so this slice demonstrates re-planning via the same adapter used by imports.

## Quotas

- `GET /api/v1/me/ai-quotas` returns per-metric usage. `POST /api/v1/me/imports/text|object` and `POST /api/v1/me/translations` consult a per-user monthly quota; over-quota requests fail with `403 Import quota exceeded.` or `403 Translation quota exceeded.` and never persist a job.
- Quota increments happen **inside** the call that consumes the slot, so retries after a `403` do not double-count.

## Operations

The `Trippify.AssistedImport` meter emits `trippify.assistedimport.commands` with low-cardinality `operation` tags (`import-text-queued`, `import-object-queued`, `import-process`, `draft-approved`, `draft-rejected`, `translation-linked`). Source text, media references, translation bodies, and provenance tokens are never part of the telemetry. Alert on elevated `403 quota-exceeded` (abuse) or repeated `Translation overwritten` (stale translations).

## Privacy guarantees

- Imports and drafts are scoped to the submitting user; `GET /api/v1/me/imports/{jobId}` and the draft endpoints return `404` for any owner mismatch.
- AI assistance runs through `IAiAssistant` so secrets never leave the API process; the LocalProviders stub keeps the contract intact without invoking an external model.
- Quotas prevent bulk uploads from monopolising shared infrastructure, and rejected `503`-style failures are encoded as `403` to keep the privacy posture consistent with the rest of the API.
