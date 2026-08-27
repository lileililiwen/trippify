using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trippify.Api;
using Trippify.Application;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class DurableBackgroundJobsTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    [Fact]
    public async Task Enqueue_is_persistent_and_duplicate_idempotency_key_is_ignored()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>();
            await queue.EnqueueAsync(BackgroundJobTypes.EvidenceRetention, JsonSerializer.Serialize(new EvidenceRetentionPayload(Guid.NewGuid())), default, "restart-job");
            await queue.EnqueueAsync(BackgroundJobTypes.EvidenceRetention, JsonSerializer.Serialize(new EvidenceRetentionPayload(Guid.NewGuid())), default, "restart-job");
        }
        await WithDb(async db => Assert.Equal(1, await db.BackgroundJobs.CountAsync(x => x.IdempotencyKey == "restart-job")));
    }

    [Fact]
    public async Task Expired_evidence_cleanup_is_idempotent_and_reconciles_badge()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid(); var guideId = Guid.NewGuid(); var evidenceId = Guid.NewGuid(); var jobId = Guid.NewGuid();
        await WithDb(async db =>
        {
            db.Users.Add(new AppUser { Id = userId, UserName = "job-evidence@example.com", Email = "job-evidence@example.com" });
            db.TravelGuides.Add(new TravelGuide { Id = guideId, OwnerUserId = userId, Title = "Job guide", CountryCode = "JP", Slug = $"job-{guideId:N}", CreatedAt = now, UpdatedAt = now });
            db.TripEvidence.Add(new TripEvidence { Id = evidenceId, GuideId = guideId, UserId = userId, Kind = EvidenceKind.Receipt, Body = new string('x', 60), Status = EvidenceStatus.Approved, SubmittedAt = now.AddDays(-100), RetentionDeadline = now.AddDays(-1) });
            db.VerifiedGuideBadges.Add(new VerifiedGuideBadge { Id = Guid.NewGuid(), GuideId = guideId, ApprovedEvidenceCount = 1, FirstGrantedAt = now.AddDays(-90), LastGrantedAt = now.AddDays(-90) });
            db.BackgroundJobs.Add(new BackgroundJob { Id = jobId, Type = BackgroundJobTypes.EvidenceRetention, Payload = JsonSerializer.Serialize(new EvidenceRetentionPayload(evidenceId)), IdempotencyKey = $"cleanup:{evidenceId}", AvailableAt = now.AddDays(-1), CreatedAt = now.AddDays(-100) });
            await db.SaveChangesAsync();
        });
        await Process();
        await WithDb(async db =>
        {
            var evidence = await db.TripEvidence.SingleAsync(x => x.Id == evidenceId);
            var badge = await db.VerifiedGuideBadges.SingleAsync(x => x.GuideId == guideId);
            Assert.NotNull(evidence.DeletedAt); Assert.Empty(evidence.Body); Assert.Equal(0, badge.ApprovedEvidenceCount); Assert.NotNull(badge.RevokedAt);
            var job = await db.BackgroundJobs.SingleAsync(x => x.Id == jobId);
            job.Status = BackgroundJobStatus.Running; job.Attempts++; job.LeaseOwner = "replay"; job.LeaseExpiresAt = now.AddMinutes(1);
            await db.SaveChangesAsync();
        });
        await using var replayScope = factory.Services.CreateAsyncScope();
        var replayDb = replayScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await replayScope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>().ExecuteClaimedAsync(await replayDb.BackgroundJobs.SingleAsync(x => x.Id == jobId), default);
        await WithDb(async db => Assert.Equal(0, (await db.VerifiedGuideBadges.SingleAsync(x => x.GuideId == guideId)).ApprovedEvidenceCount));
    }

    [Fact]
    public async Task Current_notification_opt_out_suppresses_queued_delivery()
    {
        var userId = Guid.NewGuid(); var jobId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await WithDb(async db =>
        {
            db.Users.Add(new AppUser { Id = userId, UserName = "job-optout@example.com", Email = "job-optout@example.com" });
            db.NotificationPreferences.Add(new NotificationPreference { UserId = userId, EmailEnabled = false, InAppEnabled = false });
            db.BackgroundJobs.Add(new BackgroundJob { Id = jobId, Type = BackgroundJobTypes.NotificationDelivery, Payload = JsonSerializer.Serialize(new NotificationDeliveryPayload(userId, NotificationKind.NewGuidePublished, "Private title", "Private body", null, null)), IdempotencyKey = $"notify:{jobId}", AvailableAt = now, CreatedAt = now });
            await db.SaveChangesAsync();
        });
        await Process();
        await WithDb(async db =>
        {
            Assert.False(await db.Notifications.AnyAsync(x => x.UserId == userId));
            Assert.Equal(BackgroundJobStatus.Completed, (await db.BackgroundJobs.SingleAsync(x => x.Id == jobId)).Status);
        });
    }

    [Fact]
    public async Task Poison_job_dead_letters_and_admin_diagnostics_hide_payload()
    {
        var jobId = Guid.NewGuid();
        await WithDb(async db =>
        {
            db.BackgroundJobs.Add(new BackgroundJob { Id = jobId, Type = "unknown", Payload = "secret evidence body", IdempotencyKey = $"poison:{jobId}", MaxAttempts = 1, AvailableAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });
        await Process();
        await WithDb(async db => Assert.Equal(BackgroundJobStatus.DeadLetter, (await db.BackgroundJobs.SingleAsync(x => x.Id == jobId)).Status));
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/background-jobs")).StatusCode);
        var admin = await CreateAdmin();
        using var adminClient = factory.CreateClient();
        var login = await adminClient.PostAsJsonAsync("/api/v1/auth/login", new { email = admin.Email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString());
        var diagnostics = await adminClient.GetAsync("/api/v1/admin/background-jobs");
        diagnostics.EnsureSuccessStatusCode();
        Assert.DoesNotContain("secret evidence body", await diagnostics.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Expired_lease_is_reclaimed_once_by_competing_workers()
    {
        var jobId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await WithDb(async db =>
        {
            db.BackgroundJobs.Add(new BackgroundJob
            {
                Id = jobId, Type = BackgroundJobTypes.EvidenceRetention,
                Payload = JsonSerializer.Serialize(new EvidenceRetentionPayload(Guid.NewGuid())),
                IdempotencyKey = $"expired:{jobId}", Status = BackgroundJobStatus.Running,
                Attempts = 1, LeaseOwner = "crashed-worker", LeaseExpiresAt = now.AddMinutes(-1),
                AvailableAt = now.AddMinutes(-2), CreatedAt = now.AddMinutes(-3),
            });
            await db.SaveChangesAsync();
        });
        async Task<int> Run(string worker)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>().ProcessBatchAsync(worker, 1, default);
        }
        await Task.WhenAll(Run("worker-a"), Run("worker-b"));
        await WithDb(async db =>
        {
            var job = await db.BackgroundJobs.SingleAsync(x => x.Id == jobId);
            Assert.Equal(BackgroundJobStatus.Completed, job.Status);
            Assert.Equal(2, job.Attempts);
        });
    }

    [Fact]
    public async Task Failure_retries_with_backoff_then_dead_letters_and_cancellation_releases_claim()
    {
        var retryId = Guid.NewGuid(); var cancelId = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await WithDb(async db =>
        {
            db.BackgroundJobs.AddRange(
                new BackgroundJob { Id = retryId, Type = "unknown-retry", Payload = "{}", IdempotencyKey = $"retry:{retryId}", Status = BackgroundJobStatus.Running, Attempts = 1, MaxAttempts = 2, AvailableAt = now, CreatedAt = now, LeaseOwner = "test", LeaseExpiresAt = now.AddMinutes(1) },
                new BackgroundJob { Id = cancelId, Type = BackgroundJobTypes.NotificationDelivery, Payload = JsonSerializer.Serialize(new NotificationDeliveryPayload(Guid.NewGuid(), NotificationKind.FollowerGained, "title", "body", null, null)), IdempotencyKey = $"cancel:{cancelId}", Status = BackgroundJobStatus.Running, Attempts = 1, AvailableAt = now, CreatedAt = now, LeaseOwner = "test", LeaseExpiresAt = now.AddMinutes(1) });
            await db.SaveChangesAsync();
        });
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>();
        var retry = await db.BackgroundJobs.SingleAsync(x => x.Id == retryId);
        await processor.ExecuteClaimedAsync(retry, default);
        Assert.Equal(BackgroundJobStatus.Pending, retry.Status);
        Assert.True(retry.AvailableAt > now);
        retry.Status = BackgroundJobStatus.Running; retry.Attempts = 2;
        await processor.ExecuteClaimedAsync(retry, default);
        Assert.Equal(BackgroundJobStatus.DeadLetter, retry.Status);

        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var cancelled = await db.BackgroundJobs.SingleAsync(x => x.Id == cancelId);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ExecuteClaimedAsync(cancelled, cancellation.Token));
        Assert.Equal(BackgroundJobStatus.Pending, cancelled.Status);
        Assert.Null(cancelled.LeaseOwner);
    }

    [Fact]
    public async Task Evidence_attachment_cleanup_deletes_rejected_expired_and_unattached_files_idempotently()
    {
        var owner = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var rejectedId = Guid.NewGuid();
        var expiredId = Guid.NewGuid();
        var unattachedExpiredId = Guid.NewGuid();
        await WithDb(async db =>
        {
            db.Users.Add(new AppUser { Id = owner, UserName = "jobs-att@example.com", Email = "jobs-att@example.com" });
            db.EvidenceAttachments.AddRange(
                new EvidenceAttachment { Id = rejectedId, OwnerUserId = owner, StorageKey = "evidence/rejected", FileName = "x.jpg", ContentType = "image/jpeg", SizeBytes = 1024, Sha256 = new string('1', 64), State = EvidenceAttachmentState.Rejected, CreatedAt = now.AddHours(-2), ExpiresAt = now.AddHours(-1), DeletedAt = now.AddHours(-2) },
                new EvidenceAttachment { Id = expiredId, OwnerUserId = owner, StorageKey = "evidence/expired", FileName = "y.jpg", ContentType = "image/jpeg", SizeBytes = 1024, Sha256 = new string('2', 64), State = EvidenceAttachmentState.Staged, CreatedAt = now.AddHours(-2), ExpiresAt = now.AddHours(-1) },
                new EvidenceAttachment { Id = unattachedExpiredId, OwnerUserId = owner, StorageKey = "evidence/unattached", FileName = "z.jpg", ContentType = "image/jpeg", SizeBytes = 1024, Sha256 = new string('3', 64), State = EvidenceAttachmentState.Ready, CreatedAt = now.AddHours(-30), ExpiresAt = now.AddHours(-1) });
            await db.SaveChangesAsync();
        });
        await using var enqueueScope = factory.Services.CreateAsyncScope();
        var queue = enqueueScope.ServiceProvider.GetRequiredService<IBackgroundJobQueue>();
        await queue.EnqueueAsync(BackgroundJobTypes.EvidenceAttachmentCleanup, JsonSerializer.Serialize(new EvidenceAttachmentCleanupPayload()), default, idempotencyKey: $"cleanup-test:{Guid.NewGuid()}");
        await Process();
        await Process();
        await WithDb(async db =>
        {
            foreach (var id in new[] { rejectedId, expiredId, unattachedExpiredId })
            {
                var row = await db.EvidenceAttachments.SingleAsync(x => x.Id == id);
                Assert.Equal(EvidenceAttachmentState.Deleted, row.State);
                Assert.NotNull(row.DeletedAt);
            }
        });
    }

    private async Task Process()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<BackgroundJobProcessor>().ProcessBatchAsync("test-worker", 20, default);
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<AppUser> CreateAdmin()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        if (!await roles.RoleExistsAsync("Administrator")) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>("Administrator"))).Succeeded);
        var user = new AppUser { Id = Guid.NewGuid(), UserName = "jobs-admin@example.com", Email = "jobs-admin@example.com", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user, "Strong!Pass123")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Administrator")).Succeeded);
        return user;
    }
}
