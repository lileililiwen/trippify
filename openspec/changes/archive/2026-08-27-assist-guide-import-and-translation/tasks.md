# Tasks

## 1. Domain model and persistence

- [x] 1.1 Add `ImportJob`, `ImportDraft`, `Translation`, `AiQuotaUsage` entities with EF configurations in `src/Trippify.Infrastructure/AssistedImport.cs`.
- [x] 1.2 Register the new `DbSet<>` properties on `AppDbContext`.
- [x] 1.3 Add forward-only EF Core migration `AddAssistedImportTranslation` with unique indexes and FK cascades.

## 2. ASP.NET Core API surface

- [x] 2.1 Create `src/Trippify.Api/AssistedImportEndpoints.cs` mapping:
  - `POST /api/v1/me/imports/text { sourceText }`
  - `POST /api/v1/me/imports/object { objectKey, kind }`
  - `GET /api/v1/me/imports` (paginated)
  - `GET /api/v1/me/imports/{jobId}`
  - `POST /api/v1/me/imports/{jobId}/process`
  - `POST /api/v1/me/drafts/{draftId}/approve { guideId }`
  - `POST /api/v1/me/drafts/{draftId}/reject`
  - `POST /api/v1/me/translations { sourceDraftId, locale, body }`
  - `GET /api/v1/me/translations`
  - `GET /api/v1/me/ai-quotas`
- [x] 2.2 Wire `MapAssistedImport()` in `Program.cs` after `MapManagedSaas()`.
- [x] 2.3 Honor quotas inside the import endpoints, returning `403 quota-exceeded` rather than consuming the slot.
- [x] 2.4 Hook `IAiAssistant.AssistAsync` to draft the import body and a translated body; emit `Trippify.AssistedImport` meter counters with low-cardinality tags.

## 3. Flutter client experience

- [x] 3.1 Add typed models (`ImportJob`, `ImportDraft`, `Translation`, `AiQuotaUsage`) to `apps/trippify_flutter/lib/api_client.dart`.
- [x] 3.2 Add `submitTextImport`, `submitObjectImport`, `listMyImports`, `getImportJob`, `processImportJob`, `approveImportDraft`, `rejectImportDraft`, `createTranslation`, `listMyTranslations`, `listMyAiQuotas` methods.
- [x] 3.3 Build `_ImportSubmissionScreen` (paste text or upload object) and `_ImportReviewScreen` (draft + approve/reject).
- [x] 3.4 Cover loading/empty/error states and accessibility (`Semantics`) in `apps/trippify_flutter/test/widget_test.dart`.

## 4. Concurrency, idempotency, privacy, retention, upgrades

- [x] 4.1 Tests in `tests/Trippify.ApiTests/AssistedImportApiTests.cs` covering:
  - text import produces a `PendingReview` draft with provenance and does not auto-publish
  - linked translation stores the source draft id and re-runs after approval
  - quota enforcement rejects after the configured limit
  - approve ignores drafts owned by someone else
- [x] 4.2 Verify replaying a process action on an already-completed job is idempotent.

## 5. OpenAPI, telemetry, documentation, quality gates

- [x] 5.1 Confirm `swashbuckle` documents the new endpoint contracts.
- [x] 5.2 Document `Trippify.AssistedImport` meter alerts (quota exceeded, no PII or media bytes in telemetry).
- [x] 5.3 Author `docs/assisted-import.md` and link from `README.md`.
- [x] 5.4 Run `dotnet build`, `dotnet test`, `flutter analyze`, and `flutter test`.
