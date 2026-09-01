# Assisted import and translation

Authors need help translating and reformatting source material without giving the AI the keys to the kingdom. This slice adds queued text/photo/video imports, validated, attributed private drafts, provider-backed translation, owner-driven review, and per-user quotas.

## Imports

- `POST /api/v1/me/imports/text { sourceText }` queues a text import (30–20 000 characters). The job leaves `Queued → Processing → Completed` synchronously so reviewers can act without waiting for a background scheduler.
- `POST /api/v1/me/imports/object { objectKey, kind }` queues an object-backed import; `kind` is parsed to `Photo`/`Video` (defaults to `Photo`).
- `GET /api/v1/me/imports?limit=N` lists the caller's jobs newest-first (`limit` clamped to `1`-`100`).
- `GET /api/v1/me/imports/{jobId}` returns the job and, when completed, the associated draft.
- `POST /api/v1/me/imports/{jobId}/process` is a no-op for completed jobs and re-runs processing for queued ones (idempotent on completed).

## AI adapter contract

- `IAiAssistant` accepts a typed `AiAssistRequest` (kind, source text, locales, schema version, output limits) and returns an `AiAssistResult` with status, title, nodes or body, provider/model/schema version, attempt count, and a structured failure code.
- `AiAssistStatus` values: `Completed`, `InvalidOutput`, `ProviderUnavailable`, `Timeout`, `Disabled`. Drafts and translations are only persisted on `Completed`; every other state surfaces a retryable or terminal failure to the caller. A 401/403 from the configured provider maps to `InvalidOutput` so no draft or translation is committed.
- The production adapter (`HttpAiAssistant`) issues an HTTP `POST /v1/ai/assist` against a configured server-side endpoint, authenticates with `Authorization: Bearer ${Ai:ApiKey}` (the configured credential, never a placeholder), validates the response against the `v1` schema (`title`, `nodes[]` for drafts; `body` for translations), and retries transient transport failures with exponential backoff inside the configured timeout. 5xx, 408, and 429 responses are retried; 401/403 are not — they are surfaced as a controlled failure.
- Local deployments bind the `Ai:Provider` setting to `local` (default) or set `Ai:Enabled=false`. In both cases the adapter returns a `Disabled` result without echoing the source text.
- Server-side credentials live under `Ai:ApiKey`. Client-held keys are out of scope. The API key is never logged, embedded in error responses, or returned by any endpoint.

## Draft review

- `POST /api/v1/me/drafts/{draftId}/approve { guideId? }` flips the draft to `Approved`. **No automatic publish** — the draft stays private until a future slice wires the guide editor.
- `POST /api/v1/me/drafts/{draftId}/reject` flips the draft to `Rejected`.
- Drafts store provenance via `provenanceJson` plus first-class columns (`ProviderName`, `ModelName`, `SchemaVersion`, `OutputSchemaVersion`) so reviewers can trace every node back to the import and refuse to approve drafts lacking provenance.

## Translations

- `POST /api/v1/me/translations { sourceDraftId, locale, body }` runs the body through `IAiAssistant`, validates the response, and stores a translation linked to the source draft. Provider/model/schema provenance is captured alongside the body.
- Re-submitting the same `(sourceDraftId, locale)` updates the body and marks the translation `Outdated`. The unique index `(SourceDraftId, Locale)` enforces the link in the database even under concurrent requests.
- `GET /api/v1/me/translations` returns the caller's translations newest-first.
- Provider outages during translation return `503 AI translation failed.` and the quota reservation is released without persisting a body.

## Quotas

- `GET /api/v1/me/ai-quotas` returns per-metric usage. `POST /api/v1/me/imports/text|object` and `POST /api/v1/me/translations` consult a per-user monthly quota; over-quota requests fail with `403 Import quota exceeded.` or `403 Translation quota exceeded.` and never persist a job.
- Quota increments happen **inside** the call that consumes the slot, so retries after a `403` do not double-count.

## Operations

The `Trippify.AssistedImport` meter emits `trippify.assistedimport.commands` with low-cardinality `operation` tags (`import-text-queued`, `import-object-queued`, `import-process`, `draft-approved`, `draft-rejected`, `translation-linked`). Source text, media references, translation bodies, and provenance tokens are never part of the telemetry. Alert on elevated `403 quota-exceeded` (abuse) or repeated `Translation overwritten` (stale translations).

## Privacy guarantees

- Imports and drafts are scoped to the submitting user; `GET /api/v1/me/imports/{jobId}` and the draft endpoints return `404` for any owner mismatch.
- AI assistance runs through `IAiAssistant` so secrets never leave the API process; the local adapter keeps the contract intact without invoking an external model and is clearly labelled `Disabled` rather than fabricating a successful draft.
- Source text is scrubbed for email addresses and phone numbers before it is forwarded to a configured provider so unrelated personal data does not leave the server.
- Quotas prevent bulk uploads from monopolising shared infrastructure, and rejected `503`-style failures are encoded as `503` (translation) or as a `Failed` job (imports) so reviewers can act without losing the original source.
