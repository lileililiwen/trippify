using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class VerifiedTripEndpoints
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(90);
    private const int KAnonymityMinimum = 5;
    private static readonly Meter VerifiedMeter = new("Trippify.Verified");
    private static readonly Counter<long> VerifiedCommands = VerifiedMeter.CreateCounter<long>("trippify.verified.commands");

    public static void MapVerifiedTrips(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();
        group.MapPost("/guides/{guideId:guid}/evidence", SubmitEvidence);
        group.MapPost("/guides/{guideId:guid}/evidence/{evidenceId:guid}/attachments", AttachToEvidence);
        group.MapGet("/evidence/{evidenceId:guid}/attachments", ListOwnAttachments);
        group.MapDelete("/evidence/attachments/{attachmentId:guid}", RemoveOwnAttachment);
        group.MapGet("/evidence/attachments/{attachmentId:guid}/download", DownloadOwnAttachment);
        group.MapPost("/guides/{guideId:guid}/insights", SubmitInsight);

        var publicGroup = app.MapGroup("/api/v1").AllowAnonymous();
        publicGroup.MapGet("/guides/{guideId:guid}/evidence/badge", GetBadge);
        publicGroup.MapGet("/guides/{guideId:guid}/insights", GetInsightSummary);

        var adminGroup = app.MapGroup("/api/v1/admin").RequireAuthorization(p => p.RequireRole("Administrator"));
        adminGroup.MapPost("/evidence/{evidenceId:guid}/review", ReviewEvidence);
        adminGroup.MapDelete("/evidence/{evidenceId:guid}", DeleteEvidence);
        adminGroup.MapDelete("/guides/{guideId:guid}/badge", RevokeBadge);
        adminGroup.MapGet("/evidence/{evidenceId:guid}/attachments", ListEvidenceAttachmentsForReviewer);
        adminGroup.MapGet("/evidence/attachments/{attachmentId:guid}/download", DownloadAttachmentForReviewer);
    }

    private static async Task<IResult> SubmitEvidence(Guid guideId, SubmitEvidenceRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        var creatorId = guide.OwnerUserId;
        var isEntitled = creatorId == user || await db.PurchaseEntitlements.AsNoTracking().AnyAsync(x => x.GuideId == guideId && x.UserId == user && x.RevokedAt == null) || guide.Lifecycle == GuideLifecycle.FreePublic;
        if (!isEntitled) return Results.Forbid();
        if (await db.TripEvidence.AsNoTracking().AnyAsync(x => x.GuideId == guideId && x.UserId == user && x.DeletedAt == null))
            return Results.Conflict(new Dictionary<string, string[]> { ["evidence"] = ["Only one active evidence submission per guide is allowed."] });
        var errors = ValidateEvidence(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var attachmentIds = (request.AttachmentIds ?? new List<Guid>()).Distinct().ToArray();
        if (attachmentIds.Length > EvidenceAttachmentRules.MaxAttachmentsPerEvidence)
        {
            errors["attachmentIds"] = [$"No more than {EvidenceAttachmentRules.MaxAttachmentsPerEvidence} attachments may be linked to a single evidence submission."];
            return Results.ValidationProblem(errors);
        }
        if (attachmentIds.Length > 0)
        {
            var owned = await db.EvidenceAttachments.Where(x => attachmentIds.Contains(x.Id)).ToListAsync();
            if (owned.Count != attachmentIds.Length)
            {
                errors["attachmentIds"] = ["One or more attachments could not be found."];
                return Results.ValidationProblem(errors);
            }
            foreach (var attachment in owned)
            {
                if (attachment.OwnerUserId != user)
                {
                    errors["attachmentIds"] = ["You can only attach files you uploaded."];
                    return Results.ValidationProblem(errors);
                }
                if (attachment.State != EvidenceAttachmentState.Ready)
                {
                    errors["attachmentIds"] = ["One or more attachments are not ready. Wait for the scan to finish or upload a new file."];
                    return Results.ValidationProblem(errors);
                }
                if (attachment.DeletedAt is not null || attachment.EvidenceId is not null)
                {
                    errors["attachmentIds"] = ["One or more attachments are no longer available."];
                    return Results.ValidationProblem(errors);
                }
            }
        }
        var now = clock.UtcNow;
        var evidence = new TripEvidence
        {
            Id = Guid.NewGuid(),
            GuideId = guideId,
            UserId = user,
            Kind = request.Kind,
            Body = request.Body.Trim(),
            RedactedReference = request.RedactedReference?.Trim() ?? string.Empty,
            Status = EvidenceStatus.Pending,
            SubmittedAt = now,
            RetentionDeadline = now.Add(RetentionPeriod),
        };
        db.TripEvidence.Add(evidence);
        if (attachmentIds.Length > 0)
        {
            var staged = await db.EvidenceAttachments.Where(x => attachmentIds.Contains(x.Id)).ToListAsync();
            foreach (var attachment in staged)
            {
                attachment.EvidenceId = evidence.Id;
                attachment.LinkedAt = now;
            }
        }
        db.BackgroundJobs.Add(new BackgroundJob
        {
            Id = Guid.NewGuid(), Type = BackgroundJobTypes.EvidenceRetention,
            Payload = System.Text.Json.JsonSerializer.Serialize(new EvidenceRetentionPayload(evidence.Id)),
            IdempotencyKey = $"evidence-retention:{evidence.Id}", AvailableAt = evidence.RetentionDeadline, CreatedAt = now,
        });
        await db.SaveChangesAsync();
        VerifiedCommands.Add(1, new KeyValuePair<string, object?>("operation", "evidence-submitted"));
        return EvidenceCreatedResult(evidence);
    }

    private static async Task<IResult> AttachToEvidence(Guid guideId, Guid evidenceId, AttachEvidenceAttachmentsRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var evidence = await db.TripEvidence.SingleOrDefaultAsync(x => x.Id == evidenceId && x.DeletedAt == null);
        if (evidence is null) return Results.NotFound();
        if (evidence.GuideId != guideId || evidence.UserId != user) return Results.Forbid();
        if (evidence.Status != EvidenceStatus.Pending) return Results.Conflict(new Dictionary<string, string[]> { ["evidence"] = ["Only pending evidence accepts attachments."] });
        if (request.AttachmentIds is null || request.AttachmentIds.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachmentIds"] = ["At least one attachment is required."] });
        var distinctIds = request.AttachmentIds.Distinct().ToArray();
        if (distinctIds.Length > EvidenceAttachmentRules.MaxAttachmentsPerEvidence)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachmentIds"] = [$"No more than {EvidenceAttachmentRules.MaxAttachmentsPerEvidence} attachments may be linked to a single evidence submission."] });
        var now = clock.UtcNow;
        var owned = await db.EvidenceAttachments.Where(x => distinctIds.Contains(x.Id)).ToListAsync();
        if (owned.Count != distinctIds.Length)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachmentIds"] = ["One or more attachments could not be found."] });
        foreach (var attachment in owned)
        {
            if (attachment.OwnerUserId != user)
                return Results.Forbid();
            if (attachment.EvidenceId is not null && attachment.EvidenceId != evidenceId)
                return Results.Conflict(new Dictionary<string, string[]> { ["attachmentIds"] = ["One or more attachments are already linked to a different evidence submission."] });
            if (attachment.State != EvidenceAttachmentState.Ready)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachmentIds"] = ["One or more attachments are not ready to be linked. Wait for the scan to finish or upload a new file."] });
            if (attachment.DeletedAt is not null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachmentIds"] = ["One or more attachments have been deleted."] });
        }
        foreach (var attachment in owned)
        {
            attachment.EvidenceId = evidenceId;
            attachment.LinkedAt = now;
        }
        await db.SaveChangesAsync();
        VerifiedCommands.Add(1, new KeyValuePair<string, object?>("operation", "evidence-attachments-linked"));
        return Results.Ok(new { evidenceId, attachmentIds = owned.Select(x => x.Id).ToArray() });
    }

    private static async Task<IResult> ListOwnAttachments(Guid evidenceId, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var evidence = await db.TripEvidence.AsNoTracking().SingleOrDefaultAsync(x => x.Id == evidenceId && x.DeletedAt == null);
        if (evidence is null || evidence.UserId != user) return Results.NotFound();
        var rows = await db.EvidenceAttachments.AsNoTracking()
            .Where(x => x.EvidenceId == evidenceId && x.DeletedAt == null)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new EvidenceAttachmentSummary(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.State.ToString(), x.CreatedAt, x.LinkedAt, x.ScanFailureCode))
            .ToListAsync();
        return Results.Ok(rows);
    }

    private static async Task<IResult> RemoveOwnAttachment(Guid attachmentId, ClaimsPrincipal principal, AppDbContext db, IObjectStorage storage, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var attachment = await db.EvidenceAttachments.SingleOrDefaultAsync(x => x.Id == attachmentId);
        if (attachment is null) return Results.NotFound();
        if (attachment.OwnerUserId != user) return Results.Forbid();
        if (attachment.State == EvidenceAttachmentState.Deleted || attachment.DeletedAt is not null) return Results.NoContent();
        var now = clock.UtcNow;
        attachment.State = EvidenceAttachmentState.Deleted;
        attachment.DeletedAt = now;
        if (attachment.EvidenceId is not null)
        {
            var evidence = await db.TripEvidence.SingleOrDefaultAsync(x => x.Id == attachment.EvidenceId);
            if (evidence is not null && evidence.Status == EvidenceStatus.Pending)
            {
                var remaining = await db.EvidenceAttachments.CountAsync(x => x.EvidenceId == evidence.Id && x.DeletedAt == null);
                if (remaining == 0)
                {
                    evidence.Body = string.Empty;
                    evidence.RedactedReference = string.Empty;
                }
            }
        }
        await db.SaveChangesAsync();
        try { await storage.DeleteAsync(attachment.StorageKey, default); } catch (Exception error) when (error is IOException or NotSupportedException) { /* best-effort */ }
        VerifiedCommands.Add(1, new KeyValuePair<string, object?>("operation", "evidence-attachment-removed"));
        return Results.NoContent();
    }

    private static async Task<IResult> DownloadOwnAttachment(Guid attachmentId, ClaimsPrincipal principal, AppDbContext db, IObjectStorage storage, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var attachment = await db.EvidenceAttachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachmentId);
        if (attachment is null || attachment.OwnerUserId != user) return Results.NotFound();
        if (attachment.State != EvidenceAttachmentState.Ready || attachment.DeletedAt is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachment"] = ["Attachment is not available for download."] });
        var evidence = await db.TripEvidence.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachment.EvidenceId && x.DeletedAt == null);
        if (evidence is null) return Results.NotFound();
        var uri = await storage.CreateSignedReadAsync(attachment.StorageKey, EvidenceAttachmentRules.DownloadLifetime, default);
        VerifiedCommands.Add(1, new KeyValuePair<string, object?>("operation", "evidence-attachment-downloaded"));
        return Results.Ok(new EvidenceAttachmentDownloadResponse(attachment.Id, uri, attachment.ContentType, attachment.FileName, clock.UtcNow.Add(EvidenceAttachmentRules.DownloadLifetime)));
    }

    private static async Task<IResult> ListEvidenceAttachmentsForReviewer(Guid evidenceId, AppDbContext db)
    {
        var evidence = await db.TripEvidence.AsNoTracking().SingleOrDefaultAsync(x => x.Id == evidenceId && x.DeletedAt == null);
        if (evidence is null) return Results.NotFound();
        var rows = await db.EvidenceAttachments.AsNoTracking()
            .Where(x => x.EvidenceId == evidenceId && x.DeletedAt == null)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new EvidenceAttachmentReviewerView(x.Id, x.FileName, x.ContentType, x.SizeBytes, x.State.ToString(), x.ScanFailureCode))
            .ToListAsync();
        return Results.Ok(rows);
    }

    private static async Task<IResult> DownloadAttachmentForReviewer(Guid attachmentId, AppDbContext db, IObjectStorage storage, IClock clock)
    {
        var attachment = await db.EvidenceAttachments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachmentId && x.DeletedAt == null);
        if (attachment is null) return Results.NotFound();
        if (attachment.State != EvidenceAttachmentState.Ready)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["attachment"] = ["Attachment is not available for download."] });
        var uri = await storage.CreateSignedReadAsync(attachment.StorageKey, EvidenceAttachmentRules.DownloadLifetime, default);
        VerifiedCommands.Add(1, new KeyValuePair<string, object?>("operation", "evidence-attachment-downloaded-reviewer"));
        return Results.Ok(new EvidenceAttachmentDownloadResponse(attachment.Id, uri, attachment.ContentType, attachment.FileName, clock.UtcNow.Add(EvidenceAttachmentRules.DownloadLifetime)));
    }

    private static async Task<IResult> GetBadge(Guid guideId, AppDbContext db)
    {
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        var badge = await db.VerifiedGuideBadges.AsNoTracking().SingleOrDefaultAsync(x => x.GuideId == guideId && x.RevokedAt == null);
        var approvedCount = badge?.ApprovedEvidenceCount ?? await db.TripEvidence.AsNoTracking().CountAsync(x => x.GuideId == guideId && x.Status == EvidenceStatus.Approved && x.DeletedAt == null);
        return Results.Ok(new BadgeResponse(guideId, badge is not null, approvedCount, badge?.FirstGrantedAt, badge?.LastGrantedAt));
    }

    private static async Task<IResult> ReviewEvidence(Guid evidenceId, ReviewEvidenceRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var reviewer = IdentityEndpoints.CurrentUserId(principal);
        if (!Enum.TryParse<EvidenceStatus>(request.Decision, true, out var decision) || (decision != EvidenceStatus.Approved && decision != EvidenceStatus.Rejected))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["decision"] = ["Decision must be Approved or Rejected."] });
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["Reason must contain 1 to 500 characters."] });
        var evidence = await db.TripEvidence.SingleOrDefaultAsync(x => x.Id == evidenceId && x.DeletedAt == null);
        if (evidence is null) return Results.NotFound();
        if (evidence.Status != EvidenceStatus.Pending) return Results.Conflict(new Dictionary<string, string[]> { ["evidence"] = ["Only pending evidence can be reviewed."] });
        var guide = await db.TravelGuides.SingleOrDefaultAsync(x => x.Id == evidence.GuideId);
        if (guide is not null && guide.OwnerUserId == reviewer) return Results.Forbid();
        evidence.Status = decision; evidence.ReviewedAt = clock.UtcNow;
        db.EvidenceReviews.Add(new EvidenceReviewEntry { Id = Guid.NewGuid(), EvidenceId = evidenceId, ReviewerUserId = reviewer, Decision = decision, Reason = request.Reason.Trim(), ReviewedAt = evidence.ReviewedAt.Value });
        if (decision == EvidenceStatus.Approved && guide is not null)
        {
            var badge = await db.VerifiedGuideBadges.SingleOrDefaultAsync(x => x.GuideId == evidence.GuideId);
            var now = evidence.ReviewedAt.Value;
            if (badge is null)
                db.VerifiedGuideBadges.Add(new VerifiedGuideBadge { Id = Guid.NewGuid(), GuideId = evidence.GuideId, ApprovedEvidenceCount = 1, FirstGrantedAt = now, LastGrantedAt = now });
            else { badge.ApprovedEvidenceCount += 1; badge.LastGrantedAt = now; badge.RevokedAt = null; }
        }
        await db.SaveChangesAsync();
        VerifiedCommands.Add(1, new KeyValuePair<string, object?>("operation", "evidence-reviewed"));
        return EvidenceCreatedResult(evidence, request.Reason.Trim(), reviewer);
    }

    private static async Task<IResult> DeleteEvidence(Guid evidenceId, ClaimsPrincipal principal, AppDbContext db, IClock clock, IObjectStorage storage)
    {
        var evidence = await db.TripEvidence.SingleOrDefaultAsync(x => x.Id == evidenceId);
        if (evidence is null) return Results.NotFound();
        if (evidence.DeletedAt is not null) return Results.NoContent();
        var now = clock.UtcNow;
        var attachments = await db.EvidenceAttachments.Where(x => x.EvidenceId == evidence.Id && x.DeletedAt == null).ToListAsync();
        foreach (var attachment in attachments)
        {
            attachment.State = EvidenceAttachmentState.Deleted;
            attachment.DeletedAt = now;
            try { await storage.DeleteAsync(attachment.StorageKey, default); } catch (Exception error) when (error is IOException or NotSupportedException) { /* best-effort */ }
        }
        evidence.DeletedAt = now;
        var approvedBadge = await db.VerifiedGuideBadges.SingleOrDefaultAsync(x => x.GuideId == evidence.GuideId);
        if (approvedBadge is not null && evidence.Status == EvidenceStatus.Approved && approvedBadge.ApprovedEvidenceCount > 0)
        {
            approvedBadge.ApprovedEvidenceCount -= 1;
            if (approvedBadge.ApprovedEvidenceCount == 0) approvedBadge.RevokedAt = evidence.DeletedAt;
        }
        await db.SaveChangesAsync();
        VerifiedCommands.Add(1, new KeyValuePair<string, object?>("operation", "evidence-deleted"));
        return Results.NoContent();
    }

    private static async Task<IResult> RevokeBadge(Guid guideId, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var badge = await db.VerifiedGuideBadges.SingleOrDefaultAsync(x => x.GuideId == guideId);
        if (badge is null) return Results.NotFound();
        if (badge.RevokedAt is not null) return Results.NoContent();
        badge.RevokedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        VerifiedCommands.Add(1, new KeyValuePair<string, object?>("operation", "badge-revoked"));
        return Results.NoContent();
    }

    private static async Task<IResult> SubmitInsight(Guid guideId, SubmitInsightRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        if (guide.OwnerUserId == user) return Results.ValidationProblem(new Dictionary<string, string[]> { ["insight"] = ["Creators cannot share insights about their own guide."] });
        if (request.PartySize is < 1 or > 50) return Results.ValidationProblem(new Dictionary<string, string[]> { ["partySize"] = ["Party size must be between 1 and 50."] });
        if (request.TripDays is < 1 or > 60) return Results.ValidationProblem(new Dictionary<string, string[]> { ["tripDays"] = ["Trip days must be between 1 and 60."] });
        if (request.TotalCostMinorUnits is < 0 or > 100_000_00) return Results.ValidationProblem(new Dictionary<string, string[]> { ["totalCostMinorUnits"] = ["Total cost must be between 0 and 100,000 in minor units."] });
        if (request.CurrencyCode is null || request.CurrencyCode.Length != 3) return Results.ValidationProblem(new Dictionary<string, string[]> { ["currencyCode"] = ["Currency code must be 3 letters."] });
        if (await db.ActualTripMetrics.AsNoTracking().AnyAsync(x => x.GuideId == guideId && x.UserId == user))
            return Results.Conflict(new Dictionary<string, string[]> { ["insight"] = ["Only one insight submission per user per guide is allowed."] });
        var metric = new ActualTripMetric
        {
            Id = Guid.NewGuid(),
            GuideId = guideId,
            UserId = user,
            PartySize = request.PartySize,
            TripDays = request.TripDays,
            TotalCostMinorUnits = request.TotalCostMinorUnits,
            CurrencyCode = request.CurrencyCode.ToUpperInvariant(),
            SubmittedAt = clock.UtcNow,
        };
        db.ActualTripMetrics.Add(metric);
        await db.SaveChangesAsync();
        VerifiedCommands.Add(1, new KeyValuePair<string, object?>("operation", "insight-submitted"));
        return Results.Created($"/api/v1/guides/{guideId}/insights", new { metric.Id });
    }

    private static async Task<IResult> GetInsightSummary(Guid guideId, AppDbContext db)
    {
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        var metrics = await db.ActualTripMetrics.AsNoTracking().Where(x => x.GuideId == guideId).ToListAsync();
        if (metrics.Count < KAnonymityMinimum) return Results.Ok(new InsightSummaryResponse(guideId, metrics.Count, false, null, null, null));
        var byCurrency = metrics.GroupBy(x => x.CurrencyCode).OrderByDescending(g => g.Count()).First();
        var partyMedian = Median(metrics.Select(x => x.PartySize).ToList());
        var daysMedian = Median(metrics.Select(x => x.TripDays).ToList());
        var costMedian = Median(metrics.Select(x => x.TotalCostMinorUnits).ToList());
        var partyAverage = metrics.Average(x => x.PartySize);
        var daysAverage = metrics.Average(x => x.TripDays);
        var costAverage = metrics.Average(x => x.TotalCostMinorUnits);
        var median = new InsightAggregate(byCurrency.Key, byCurrency.Count(), partyMedian, daysMedian, costMedian);
        var average = new InsightAggregate(byCurrency.Key, byCurrency.Count(), partyAverage, daysAverage, costAverage);
        return Results.Ok(new InsightSummaryResponse(guideId, metrics.Count, true, median, average, null));
    }

    private static double Median(IReadOnlyCollection<int> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(x => x).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }

    private static double Median(IReadOnlyCollection<long> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(x => x).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
    }

    private static double Average(IReadOnlyCollection<int> values) => values.Count == 0 ? 0 : values.Average();
    private static double Average(IReadOnlyCollection<long> values) => values.Count == 0 ? 0 : values.Average();

    private static Dictionary<string, string[]> ValidateEvidence(SubmitEvidenceRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.IsDefined(typeof(EvidenceKind), request.Kind)) errors["kind"] = ["Evidence kind is required."];
        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Trim().Length is < 50 or > 4000)
            errors["body"] = ["Evidence body must contain 50 to 4000 characters."];
        if (!string.IsNullOrEmpty(request.RedactedReference) && request.RedactedReference.Trim().Length > 200)
            errors["redactedReference"] = ["Redacted reference cannot exceed 200 characters."];
        return errors;
    }

    private static IResult EvidenceCreatedResult(TripEvidence evidence, string? reviewerReason = null, Guid? reviewerId = null)
    {
        return Results.Created($"/api/v1/guides/{evidence.GuideId}/evidence", new
        {
            evidence.Id,
            evidence.GuideId,
            evidence.UserId,
            Kind = evidence.Kind.ToString(),
            evidence.Body,
            evidence.RedactedReference,
            Status = evidence.Status.ToString(),
            evidence.SubmittedAt,
            evidence.RetentionDeadline,
            evidence.ReviewedAt,
            evidence.DeletedAt,
            ReviewerReason = reviewerReason,
            ReviewerId = reviewerId,
        });
    }
}

public sealed record SubmitEvidenceRequest(EvidenceKind Kind, string Body, string? RedactedReference, List<Guid>? AttachmentIds);
public sealed record ReviewEvidenceRequest(string Decision, string Reason);
public sealed record SubmitInsightRequest(int PartySize, int TripDays, long TotalCostMinorUnits, string CurrencyCode);
public sealed record BadgeResponse(Guid GuideId, bool Verified, int ApprovedEvidenceCount, DateTimeOffset? FirstGrantedAt, DateTimeOffset? LastGrantedAt);
public sealed record InsightAggregate(string CurrencyCode, int Count, double AveragePartySize, double AverageTripDays, double AverageTotalCostMinorUnits);
public sealed record InsightSummaryResponse(Guid GuideId, int SubmissionCount, bool MeetsKAnonymity, InsightAggregate? Median, InsightAggregate? Average, InsightAggregate? MinMax);
public sealed record AttachEvidenceAttachmentsRequest(List<Guid> AttachmentIds);
public sealed record EvidenceAttachmentSummary(Guid Id, string FileName, string ContentType, long SizeBytes, string State, DateTimeOffset CreatedAt, DateTimeOffset? LinkedAt, string? ScanFailureCode);
public sealed record EvidenceAttachmentReviewerView(Guid Id, string FileName, string ContentType, long SizeBytes, string State, string? ScanFailureCode);
public sealed record EvidenceAttachmentDownloadResponse(Guid Id, Uri Url, string ContentType, string FileName, DateTimeOffset ExpiresAt);
public sealed record StageEvidenceAttachmentRequest(string FileName, string ContentType, long SizeBytes, string Sha256);
public sealed record StageEvidenceAttachmentResponse(Guid AttachmentId, string StorageKey, DateTimeOffset ExpiresAt);