using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class AssistedImportEndpoints
{
    private const int DefaultImportLimit = 5;
    private const int DefaultTranslationLimit = 10;
    private static readonly Meter AssistedImportMeter = new("Trippify.AssistedImport");
    private static readonly Counter<long> AssistedImportCommands = AssistedImportMeter.CreateCounter<long>("trippify.assistedimport.commands");

    public static void MapAssistedImport(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/me").RequireAuthorization();
        group.MapPost("/imports/text", SubmitTextImport);
        group.MapPost("/imports/object", SubmitObjectImport);
        group.MapGet("/imports", ListMyImports);
        group.MapGet("/imports/{jobId:guid}", GetImportJob);
        group.MapPost("/imports/{jobId:guid}/process", ProcessImportJob);
        group.MapPost("/drafts/{draftId:guid}/approve", ApproveDraft);
        group.MapPost("/drafts/{draftId:guid}/reject", RejectDraft);
        group.MapPost("/translations", CreateTranslation);
        group.MapGet("/translations", ListMyTranslations);
        group.MapGet("/ai-quotas", ListMyAiQuotas);
    }

    private static async Task<IResult> SubmitTextImport(SubmitTextImportRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock, IAiAssistant ai, IQuotaService quotas)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        if (string.IsNullOrWhiteSpace(request.SourceText) || request.SourceText.Length is < 30 or > 20000)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["sourceText"] = ["Source text must be between 30 and 20000 characters."] });
        var reservation = await quotas.ReserveAsync(user, QuotaMetrics.AiImports, 1);
        if (!reservation.Succeeded) return QuotaProblem.From(reservation);
        var now = clock.UtcNow;
        var job = new ImportJob { Id = Guid.NewGuid(), UserId = user, Kind = ImportKind.Text, SourceText = SafeSource(request.SourceText.Trim()), Status = ImportStatus.Queued, SubmittedAt = now };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync();
        await ProcessInternalAsync(db, ai, clock, job);
        if (job.Status == ImportStatus.Completed) await quotas.FinalizeAsync(reservation.ReservationId!.Value);
        else await quotas.ReleaseAsync(reservation.ReservationId!.Value, "ai-import-failed");
        AssistedImportCommands.Add(1, new KeyValuePair<string, object?>("operation", "import-text-queued"));
        return Results.Created($"/api/v1/me/imports/{job.Id}", JobResponse(job));
    }

    private static async Task<IResult> SubmitObjectImport(SubmitObjectImportRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock, IAiAssistant ai, IQuotaService quotas)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        if (string.IsNullOrWhiteSpace(request.ObjectKey)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["objectKey"] = ["Object key is required."] });
        if (!Enum.TryParse<ImportKind>(request.Kind, true, out var kind)) kind = ImportKind.Photo;
        var reservation = await quotas.ReserveAsync(user, QuotaMetrics.AiImports, 1);
        if (!reservation.Succeeded) return QuotaProblem.From(reservation);
        var now = clock.UtcNow;
        var job = new ImportJob { Id = Guid.NewGuid(), UserId = user, Kind = kind, ObjectKey = request.ObjectKey.Trim(), Status = ImportStatus.Queued, SubmittedAt = now };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync();
        await ProcessInternalAsync(db, ai, clock, job);
        if (job.Status == ImportStatus.Completed) await quotas.FinalizeAsync(reservation.ReservationId!.Value);
        else await quotas.ReleaseAsync(reservation.ReservationId!.Value, "ai-import-failed");
        AssistedImportCommands.Add(1, new KeyValuePair<string, object?>("operation", "import-object-queued"));
        return Results.Created($"/api/v1/me/imports/{job.Id}", JobResponse(job));
    }

    private static async Task<IResult> ListMyImports(int? limit, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var pageSize = Math.Clamp(limit ?? 20, 1, 100);
        var jobs = await db.ImportJobs.AsNoTracking()
            .Where(x => x.UserId == user)
            .OrderByDescending(x => x.SubmittedAt)
            .Take(pageSize)
            .ToListAsync();
        return Results.Ok(new ImportJobListResponse(jobs.Count, jobs.Select(JobResponse).ToList()));
    }

    private static async Task<IResult> GetImportJob(Guid jobId, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var job = await db.ImportJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == jobId && x.UserId == user);
        if (job is null) return Results.NotFound();
        var draft = await db.ImportDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.ImportJobId == jobId);
        return Results.Ok(new ImportJobDetailResponse(JobResponse(job), draft is null ? null : DraftResponse(draft)));
    }

    private static async Task<IResult> ProcessImportJob(Guid jobId, ClaimsPrincipal principal, AppDbContext db, IClock clock, IAiAssistant ai)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var job = await db.ImportJobs.SingleOrDefaultAsync(x => x.Id == jobId && x.UserId == user);
        if (job is null) return Results.NotFound();
        if (job.Status == ImportStatus.Completed) return Results.Ok(JobResponse(job));
        await ProcessInternalAsync(db, ai, clock, job);
        AssistedImportCommands.Add(1, new KeyValuePair<string, object?>("operation", "import-process"));
        return Results.Ok(JobResponse(job));
    }

    private static async Task<IResult> ApproveDraft(Guid draftId, ApproveDraftRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var draft = await db.ImportDrafts.SingleOrDefaultAsync(x => x.Id == draftId && x.UserId == user);
        if (draft is null) return Results.NotFound();
        if (draft.ProviderName == string.Empty) return Results.Problem("AI provenance is missing; refusing to publish.", statusCode: StatusCodes.Status409Conflict);
        draft.Status = ImportDraftStatus.Approved;
        await db.SaveChangesAsync();
        AssistedImportCommands.Add(1, new KeyValuePair<string, object?>("operation", "draft-approved"));
        return Results.Ok(DraftResponse(draft));
    }

    private static async Task<IResult> RejectDraft(Guid draftId, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var draft = await db.ImportDrafts.SingleOrDefaultAsync(x => x.Id == draftId && x.UserId == user);
        if (draft is null) return Results.NotFound();
        draft.Status = ImportDraftStatus.Rejected;
        await db.SaveChangesAsync();
        AssistedImportCommands.Add(1, new KeyValuePair<string, object?>("operation", "draft-rejected"));
        return Results.Ok(DraftResponse(draft));
    }

    private static async Task<IResult> CreateTranslation(CreateTranslationRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock, IAiAssistant ai, IQuotaService quotas)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        if (string.IsNullOrWhiteSpace(request.Locale) || request.Locale.Length is < 2 or > 8)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["locale"] = ["Locale must be 2-8 characters."] });
        if (string.IsNullOrWhiteSpace(request.Body)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Body is required."] });
        var source = await db.ImportDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.SourceDraftId && x.UserId == user);
        if (source is null) return Results.NotFound();
        var reservation = await quotas.ReserveAsync(user, QuotaMetrics.AiImports, 1);
        if (!reservation.Succeeded) return QuotaProblem.From(reservation);
        var operationId = $"translation:{Guid.NewGuid():N}";
        var assistRequest = new AiAssistRequest(AiAssistKind.Translate, SafeSource(request.Body), null, request.Locale, operationId, 16000, 16000);
        AiAssistResult assist;
        try { assist = await ai.AssistAsync(assistRequest, default); }
        catch { await quotas.ReleaseAsync(reservation.ReservationId!.Value, "ai-translation-failed"); throw; }
        if (assist.Status != AiAssistStatus.Completed || string.IsNullOrEmpty(assist.Body))
        {
            await quotas.ReleaseAsync(reservation.ReservationId!.Value, "ai-translation-failed");
            return Results.Problem("AI translation failed.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        var existing = await db.Translations.SingleOrDefaultAsync(x => x.SourceDraftId == source.Id && x.Locale == request.Locale);
        if (existing is null)
        {
            existing = new Translation { Id = Guid.NewGuid(), SourceDraftId = source.Id, UserId = user, Locale = request.Locale, Body = assist.Body!, ProviderName = assist.ProviderName, ModelName = assist.ModelName, SchemaVersion = assist.SchemaVersion, CreatedAt = clock.UtcNow };
            db.Translations.Add(existing);
        }
        else
        {
            existing.Body = assist.Body!; existing.Status = TranslationStatus.Outdated; existing.UpdatedAt = clock.UtcNow;
            existing.ProviderName = assist.ProviderName; existing.ModelName = assist.ModelName; existing.SchemaVersion = assist.SchemaVersion;
        }
        await db.SaveChangesAsync();
        await quotas.FinalizeAsync(reservation.ReservationId!.Value);
        AssistedImportCommands.Add(1, new KeyValuePair<string, object?>("operation", "translation-linked"));
        return Results.Ok(TranslationResponse(existing));
    }

    private static async Task<IResult> ListMyTranslations(ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var translations = await db.Translations.AsNoTracking()
            .Where(x => x.UserId == user)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Results.Ok(new TranslationListResponse(translations.Count, translations.Select(TranslationResponse).ToList()));
    }

    private static async Task<IResult> ListMyAiQuotas(ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        await ManagedSaasEndpoints.EnsureTenantForUserAsync(db, user, clock);
        var tenantId = await db.TenantMembers.AsNoTracking().Where(x => x.UserId == user).Select(x => x.TenantId).SingleAsync();
        var quotas = await db.QuotaUsages.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Metric == QuotaMetrics.AiImports)
            .ToListAsync();
        return Results.Ok(quotas.Select(q => new QuotaUsageResponse(q.Metric, q.Used, q.Limit, q.PeriodStart, q.PeriodEnd)).ToList());
    }

    private static string SafeSource(string input) => SafeInputScrubber.Scrub(input);

    private static async Task ProcessInternalAsync(AppDbContext db, IAiAssistant ai, IClock clock, ImportJob job)
    {
        try
        {
            job.Status = ImportStatus.Processing;
            await db.SaveChangesAsync();
            var source = !string.IsNullOrWhiteSpace(job.SourceText) ? job.SourceText : $"object://{job.ObjectKey}";
            var operationId = $"import:{job.Id:N}";
            var assistRequest = new AiAssistRequest(AiAssistKind.Draft, source, null, null, operationId, 20000, 8000);
            var result = await ai.AssistAsync(assistRequest, default);
            job.ProviderName = result.ProviderName;
            job.ModelName = result.ModelName;
            job.SchemaVersion = result.SchemaVersion;
            job.AttemptCount = result.AttemptCount;
            if (result.Status == AiAssistStatus.Completed && result.Nodes is { Count: > 0 })
            {
                var title = !string.IsNullOrWhiteSpace(result.Title) ? result.Title! : (result.Nodes[0].Length > 200 ? result.Nodes[0][..200] : result.Nodes[0]);
                var draft = new ImportDraft
                {
                    Id = Guid.NewGuid(),
                    ImportJobId = job.Id,
                    UserId = job.UserId,
                    SuggestedTitle = title,
                    SuggestedNodesJson = JsonSerializer.Serialize(result.Nodes),
                    ProvenanceJson = JsonSerializer.Serialize(new { job = job.Id, kind = job.Kind.ToString(), generatedAt = clock.UtcNow, provider = result.ProviderName, model = result.ModelName, schemaVersion = result.SchemaVersion, attempts = result.AttemptCount }),
                    ProviderName = result.ProviderName,
                    ModelName = result.ModelName,
                    SchemaVersion = result.SchemaVersion,
                    OutputSchemaVersion = result.SchemaVersion,
                    CreatedAt = clock.UtcNow,
                };
                db.ImportDrafts.Add(draft);
                job.Status = ImportStatus.Completed;
                job.CompletedAt = clock.UtcNow;
            }
            else
            {
                job.Status = ImportStatus.Failed;
                job.FailureCode = result.FailureCode;
                job.FailureReason = DescribeStatus(result.Status);
                job.CompletedAt = clock.UtcNow;
            }
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            job.Status = ImportStatus.Failed;
            job.FailureCode = "unexpected-exception";
            job.FailureReason = ex.Message;
            job.CompletedAt = clock.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private static string DescribeStatus(AiAssistStatus status) => status switch
    {
        AiAssistStatus.Disabled => "AI provider is disabled by configuration; no draft was created.",
        AiAssistStatus.ProviderUnavailable => "AI provider is unavailable. Please retry.",
        AiAssistStatus.InvalidOutput => "AI provider returned output that failed validation.",
        AiAssistStatus.Timeout => "AI provider timed out before producing a result.",
        _ => "AI processing failed.",
    };

    private static ImportJobResponse JobResponse(ImportJob j) => new(j.Id, j.UserId, j.Kind.ToString(), j.Status.ToString(), j.SubmittedAt, j.CompletedAt, j.FailureReason, j.ProviderName, j.ModelName, j.SchemaVersion, j.FailureCode);
    private static ImportDraftResponse DraftResponse(ImportDraft d) => new(d.Id, d.ImportJobId, d.SuggestedTitle, d.ProvenanceJson, d.Status.ToString(), d.CreatedAt, d.SuggestedNodesJson, d.ProviderName, d.ModelName, d.SchemaVersion);
    private static TranslationResponse TranslationResponse(Translation t) => new(t.Id, t.SourceDraftId, t.Locale, t.Body, t.Status.ToString(), t.CreatedAt, t.UpdatedAt, t.ProviderName, t.ModelName, t.SchemaVersion);
}

public sealed record SubmitTextImportRequest(string SourceText);
public sealed record SubmitObjectImportRequest(string ObjectKey, string Kind);
public sealed record ApproveDraftRequest(string? GuideId);
public sealed record CreateTranslationRequest(Guid SourceDraftId, string Locale, string Body);
public sealed record ImportJobResponse(Guid Id, Guid UserId, string Kind, string Status, DateTimeOffset SubmittedAt, DateTimeOffset? CompletedAt, string FailureReason, string ProviderName, string ModelName, string SchemaVersion, string FailureCode);
public sealed record ImportJobListResponse(int Total, IReadOnlyList<ImportJobResponse> Items);
public sealed record ImportDraftResponse(Guid Id, Guid ImportJobId, string SuggestedTitle, string ProvenanceJson, string Status, DateTimeOffset CreatedAt, string SuggestedNodesJson, string ProviderName, string ModelName, string SchemaVersion);
public sealed record ImportJobDetailResponse(ImportJobResponse Job, ImportDraftResponse? Draft);
public sealed record TranslationResponse(Guid Id, Guid SourceDraftId, string Locale, string Body, string Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, string ProviderName, string ModelName, string SchemaVersion);
public sealed record TranslationListResponse(int Total, IReadOnlyList<TranslationResponse> Items);
public sealed record QuotaUsageResponse(string Metric, int Used, int Limit, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);
