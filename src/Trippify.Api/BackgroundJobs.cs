using System.Diagnostics.Metrics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class BackgroundJobTypes
{
    public const string EvidenceRetention = "evidence-retention";
    public const string NotificationDelivery = "notification-delivery";
}

public sealed record EvidenceRetentionPayload(Guid EvidenceId);
public sealed record NotificationDeliveryPayload(Guid UserId, NotificationKind Kind, string Title, string Body, string? TargetSlug, Guid? TargetGuideId);

public sealed class BackgroundJobProcessor(AppDbContext db, IClock clock, Trippify.Application.IEmailSender email, ILogger<BackgroundJobProcessor> logger)
{
    private static readonly Meter Meter = new("Trippify.BackgroundJobs");
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>("trippify.backgroundjobs.outcomes");
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);
    private static readonly SemaphoreSlim NonRelationalClaimGate = new(1, 1);

    public async Task<int> ProcessBatchAsync(string workerId, int batchSize, CancellationToken cancellationToken)
    {
        var processed = 0;
        for (var index = 0; index < Math.Clamp(batchSize, 1, 50); index++)
        {
            var job = await ClaimAsync(workerId, cancellationToken);
            if (job is null) break;
            await ExecuteClaimedAsync(job, cancellationToken);
            processed++;
        }
        return processed;
    }

    public async Task ExecuteClaimedAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        try
        {
            switch (job.Type)
            {
                case BackgroundJobTypes.EvidenceRetention:
                    await HandleEvidenceRetention(JsonSerializer.Deserialize<EvidenceRetentionPayload>(job.Payload)!, cancellationToken);
                    break;
                case BackgroundJobTypes.NotificationDelivery:
                    await HandleNotification(JsonSerializer.Deserialize<NotificationDeliveryPayload>(job.Payload)!, job.Id, cancellationToken);
                    break;
                default:
                    throw new InvalidOperationException("unknown-job-type");
            }
            job.Status = BackgroundJobStatus.Completed;
            job.CompletedAt = clock.UtcNow;
            job.LeaseOwner = null;
            job.LeaseExpiresAt = null;
            job.FailureCode = string.Empty;
            await db.SaveChangesAsync(cancellationToken);
            Outcomes.Add(1, new KeyValuePair<string, object?>("type", job.Type), new KeyValuePair<string, object?>("outcome", "completed"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            job.Status = BackgroundJobStatus.Pending;
            job.LeaseOwner = null;
            job.LeaseExpiresAt = null;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception error)
        {
            job.LeaseOwner = null;
            job.LeaseExpiresAt = null;
            job.FailureCode = error is InvalidOperationException invalid ? invalid.Message : error.GetType().Name;
            if (job.Attempts >= job.MaxAttempts)
            {
                job.Status = BackgroundJobStatus.DeadLetter;
                job.CompletedAt = clock.UtcNow;
            }
            else
            {
                job.Status = BackgroundJobStatus.Pending;
                job.AvailableAt = clock.UtcNow.Add(Backoff(job.Attempts));
            }
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Background job {JobType} failed on attempt {Attempt} with {FailureCode}", job.Type, job.Attempts, job.FailureCode);
            Outcomes.Add(1, new KeyValuePair<string, object?>("type", job.Type), new KeyValuePair<string, object?>("outcome", job.Status == BackgroundJobStatus.DeadLetter ? "dead-letter" : "retry"));
        }
    }

    private async Task<BackgroundJob?> ClaimAsync(string workerId, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational()) await NonRelationalClaimGate.WaitAsync(cancellationToken);
        try
        {
        var now = clock.UtcNow;
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var query = db.BackgroundJobs.Where(x =>
            (x.Status == BackgroundJobStatus.Pending || (x.Status == BackgroundJobStatus.Running && x.LeaseExpiresAt <= now)) &&
            x.AvailableAt <= now);
        var job = db.Database.IsRelational()
            ? await db.BackgroundJobs.FromSqlInterpolated($"SELECT * FROM background_jobs WHERE (\"Status\" = 'Pending' OR (\"Status\" = 'Running' AND \"LeaseExpiresAt\" <= {now})) AND \"AvailableAt\" <= {now} ORDER BY \"AvailableAt\", \"CreatedAt\" FOR UPDATE SKIP LOCKED LIMIT 1").SingleOrDefaultAsync(cancellationToken)
            : await query.OrderBy(x => x.AvailableAt).ThenBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (job is null) return null;
        job.Status = BackgroundJobStatus.Running;
        job.Attempts++;
        job.LeaseOwner = workerId;
        job.LeaseExpiresAt = now.Add(LeaseDuration);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return job;
        }
        finally { if (!db.Database.IsRelational()) NonRelationalClaimGate.Release(); }
    }

    private async Task HandleEvidenceRetention(EvidenceRetentionPayload payload, CancellationToken cancellationToken)
    {
        var evidence = await db.TripEvidence.SingleOrDefaultAsync(x => x.Id == payload.EvidenceId, cancellationToken);
        if (evidence is null || evidence.DeletedAt is not null || evidence.RetentionDeadline > clock.UtcNow) return;
        evidence.DeletedAt = clock.UtcNow;
        var badge = await db.VerifiedGuideBadges.SingleOrDefaultAsync(x => x.GuideId == evidence.GuideId, cancellationToken);
        if (badge is not null)
        {
            badge.ApprovedEvidenceCount = await db.TripEvidence.CountAsync(x => x.GuideId == evidence.GuideId && x.Id != evidence.Id && x.Status == EvidenceStatus.Approved && x.DeletedAt == null, cancellationToken);
            if (badge.ApprovedEvidenceCount == 0) badge.RevokedAt ??= clock.UtcNow;
        }
        evidence.Body = string.Empty;
        evidence.RedactedReference = string.Empty;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleNotification(NotificationDeliveryPayload payload, Guid jobId, CancellationToken cancellationToken)
    {
        if (await db.Notifications.AnyAsync(x => x.SourceJobId == jobId, cancellationToken)) return;
        var prefs = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == payload.UserId, cancellationToken);
        var (inApp, emailAllowed) = Allowed(prefs, payload.Kind);
        if (inApp)
            db.Notifications.Add(new Notification { Id = Guid.NewGuid(), SourceJobId = jobId, UserId = payload.UserId, Kind = payload.Kind, Title = payload.Title, Body = payload.Body, TargetSlug = payload.TargetSlug, TargetGuideId = payload.TargetGuideId, CreatedAt = clock.UtcNow });
        if (emailAllowed)
        {
            var address = await db.Users.AsNoTracking().Where(x => x.Id == payload.UserId).Select(x => x.Email).SingleOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(address)) await email.SendAsync(address, payload.Title, payload.Body, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static (bool InApp, bool Email) Allowed(NotificationPreference? p, NotificationKind kind)
    {
        if (p is null) return (true, true);
        return kind switch
        {
            NotificationKind.NewGuidePublished => (p.InAppEnabled && p.NewGuidePublishedInApp, p.EmailEnabled && p.NewGuidePublishedEmail),
            NotificationKind.NewReviewOnMyGuide => (p.InAppEnabled && p.NewReviewOnMyGuideInApp, p.EmailEnabled && p.NewReviewOnMyGuideEmail),
            NotificationKind.NewReplyToReview => (p.InAppEnabled && p.NewReplyToReviewInApp, p.EmailEnabled && p.NewReplyToReviewEmail),
            NotificationKind.FollowerGained => (p.InAppEnabled && p.FollowerGainedInApp, p.EmailEnabled && p.FollowerGainedEmail),
            NotificationKind.EvidenceReviewed => (p.InAppEnabled && p.EvidenceReviewedInApp, p.EmailEnabled && p.EvidenceReviewedEmail),
            _ => (false, false),
        };
    }

    private static TimeSpan Backoff(int attempts) => TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Clamp(attempts, 1, 8))));
}

public sealed class BackgroundJobWorker(IServiceScopeFactory scopes, IConfiguration configuration) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("BackgroundJobs:WorkersEnabled", true)) return;
        var workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var count = await scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>().ProcessBatchAsync(workerId, 10, stoppingToken);
            if (count == 0) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}

public sealed class BackgroundJobsHealthCheck(AppDbContext db, IClock clock) : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var deadLetters = await db.BackgroundJobs.CountAsync(x => x.Status == BackgroundJobStatus.DeadLetter, cancellationToken);
        var expiredLeases = await db.BackgroundJobs.CountAsync(x => x.Status == BackgroundJobStatus.Running && x.LeaseExpiresAt < clock.UtcNow, cancellationToken);
        return deadLetters + expiredLeases == 0
            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy()
            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded("Background jobs require operator attention.", data: new Dictionary<string, object> { ["deadLetters"] = deadLetters, ["expiredLeases"] = expiredLeases });
    }
}

public static class BackgroundJobEndpoints
{
    public static void MapBackgroundJobs(this WebApplication app)
    {
        app.MapGet("/api/v1/admin/background-jobs", async (AppDbContext db) =>
        {
            var rows = await db.BackgroundJobs.AsNoTracking().GroupBy(x => new { x.Type, x.Status }).Select(x => new { x.Key.Type, status = x.Key.Status.ToString(), count = x.Count(), oldestAvailableAt = x.Min(j => j.AvailableAt) }).ToListAsync();
            return Results.Ok(new { total = rows.Sum(x => x.count), items = rows });
        }).RequireAuthorization(p => p.RequireRole("Administrator"));
    }
}
