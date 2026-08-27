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
        var job = new ImportJob { Id = Guid.NewGuid(), UserId = user, Kind = ImportKind.Text, SourceText = request.SourceText.Trim(), Status = ImportStatus.Queued, SubmittedAt = now };
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
        string translated;
        try { translated = await ai.AssistAsync(request.Body, default); }
        catch { await quotas.ReleaseAsync(reservation.ReservationId!.Value, "ai-translation-failed"); throw; }
        var existing = await db.Translations.SingleOrDefaultAsync(x => x.SourceDraftId == source.Id && x.Locale == request.Locale);
        if (existing is null)
        {
            existing = new Translation { Id = Guid.NewGuid(), SourceDraftId = source.Id, UserId = user, Locale = request.Locale, Body = translated, CreatedAt = clock.UtcNow };
            db.Translations.Add(existing);
        }
        else
        {
            existing.Body = translated; existing.Status = TranslationStatus.Outdated; existing.UpdatedAt = clock.UtcNow;
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

    private static async Task<bool> TryConsumeQuotaAsync(AppDbContext db, Guid userId, string metric, int limit, IClock clock)
    {
        var now = clock.UtcNow;
        var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1);
        var quota = await db.AiQuotaUsages.SingleOrDefaultAsync(x => x.UserId == userId && x.Metric == metric && x.PeriodStart == periodStart);
        if (quota is null)
        {
            quota = new AiQuotaUsage { Id = Guid.NewGuid(), UserId = userId, Metric = metric, Used = 0, Limit = limit, PeriodStart = periodStart, PeriodEnd = periodEnd };
            db.AiQuotaUsages.Add(quota);
        }
        if (quota.Used >= quota.Limit) return false;
        quota.Used += 1;
        await db.SaveChangesAsync();
        return true;
    }

    private static async Task ProcessInternalAsync(AppDbContext db, IAiAssistant ai, IClock clock, ImportJob job)
    {
        try
        {
            job.Status = ImportStatus.Processing;
            await db.SaveChangesAsync();
            var source = !string.IsNullOrWhiteSpace(job.SourceText) ? job.SourceText : $"object://{job.ObjectKey}";
            var suggestion = await ai.AssistAsync(source, default);
            var nodes = suggestion.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(8).ToList();
            var draft = new ImportDraft
            {
                Id = Guid.NewGuid(),
                ImportJobId = job.Id,
                UserId = job.UserId,
                SuggestedTitle = $"Imported — {nodes.FirstOrDefault() ?? source[..Math.Min(40, source.Length)]}",
                SuggestedNodesJson = JsonSerializer.Serialize(nodes),
                ProvenanceJson = JsonSerializer.Serialize(new { job = job.Id, kind = job.Kind.ToString(), generatedAt = clock.UtcNow }),
                CreatedAt = clock.UtcNow,
            };
            db.ImportDrafts.Add(draft);
            job.Status = ImportStatus.Completed;
            job.CompletedAt = clock.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            job.Status = ImportStatus.Failed;
            job.FailureReason = ex.Message;
            job.CompletedAt = clock.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    private static async Task<bool> EnsureQuotaAsync(AppDbContext db, Guid userId, string metric, int limit, IClock clock)
    {
        var now = clock.UtcNow;
        var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var periodEnd = periodStart.AddMonths(1);
        var quota = await db.AiQuotaUsages.SingleOrDefaultAsync(x => x.UserId == userId && x.Metric == metric && x.PeriodStart == periodStart);
        if (quota is null)
        {
            quota = new AiQuotaUsage { Id = Guid.NewGuid(), UserId = userId, Metric = metric, Used = 0, Limit = limit, PeriodStart = periodStart, PeriodEnd = periodEnd };
            db.AiQuotaUsages.Add(quota);
            await db.SaveChangesAsync();
        }
        return quota.Used < quota.Limit;
    }

    private static ImportJobResponse JobResponse(ImportJob j) => new(j.Id, j.UserId, j.Kind.ToString(), j.Status.ToString(), j.SubmittedAt, j.CompletedAt, j.FailureReason);
    private static ImportDraftResponse DraftResponse(ImportDraft d) => new(d.Id, d.ImportJobId, d.SuggestedTitle, d.ProvenanceJson, d.Status.ToString(), d.CreatedAt, d.SuggestedNodesJson);
    private static TranslationResponse TranslationResponse(Translation t) => new(t.Id, t.SourceDraftId, t.Locale, t.Body, t.Status.ToString(), t.CreatedAt, t.UpdatedAt);
}

public sealed record SubmitTextImportRequest(string SourceText);
public sealed record SubmitObjectImportRequest(string ObjectKey, string Kind);
public sealed record ApproveDraftRequest(string? GuideId);
public sealed record CreateTranslationRequest(Guid SourceDraftId, string Locale, string Body);
public sealed record ImportJobResponse(Guid Id, Guid UserId, string Kind, string Status, DateTimeOffset SubmittedAt, DateTimeOffset? CompletedAt, string FailureReason);
public sealed record ImportJobListResponse(int Total, IReadOnlyList<ImportJobResponse> Items);
public sealed record ImportDraftResponse(Guid Id, Guid ImportJobId, string SuggestedTitle, string ProvenanceJson, string Status, DateTimeOffset CreatedAt, string SuggestedNodesJson);
public sealed record ImportJobDetailResponse(ImportJobResponse Job, ImportDraftResponse? Draft);
public sealed record TranslationResponse(Guid Id, Guid SourceDraftId, string Locale, string Body, string Status, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
public sealed record TranslationListResponse(int Total, IReadOnlyList<TranslationResponse> Items);
public sealed record QuotaUsageResponse(string Metric, int Used, int Limit, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);
