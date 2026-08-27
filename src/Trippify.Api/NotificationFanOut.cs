using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class NotificationFanOut
{
    public static async Task QueueReviewSubmittedAsync(AppDbContext db, GuideReview review, TravelGuide guide, IClock clock, BackgroundJobProcessor processor)
    {
        if (guide.OwnerUserId == review.UserId) { await db.SaveChangesAsync(); return; }
        var prefs = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == guide.OwnerUserId);
        var inAppAllowed = prefs is null || (prefs.InAppEnabled && prefs.NewReviewOnMyGuideInApp);
        var emailAllowed = prefs is null || (prefs.EmailEnabled && prefs.NewReviewOnMyGuideEmail);
        if (!inAppAllowed && !emailAllowed) { await db.SaveChangesAsync(); return; }
        var job = Job($"notification:review:{review.Id}", new NotificationDeliveryPayload(guide.OwnerUserId, NotificationKind.NewReviewOnMyGuide, "New review on your guide", $"Rating {review.Rating}/5 — {TrimForNotification(review.Body)}", guide.Slug, guide.Id), clock);
        db.BackgroundJobs.Add(job);
        await db.SaveChangesAsync();
        await ExecuteInline(job, processor);
    }

    public static async Task QueueGuideUpdatedAsync(AppDbContext db, TravelGuide guide, GuideRelease release, IClock clock, BackgroundJobProcessor processor)
    {
        var buyers = await db.PurchaseEntitlements.AsNoTracking()
            .Where(x => x.GuideId == guide.Id && x.RevokedAt == null)
            .Select(x => x.UserId)
            .Distinct()
            .ToListAsync();
        foreach (var buyer in buyers)
        {
            var prefs = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == buyer);
            var inAppAllowed = prefs is null || (prefs.InAppEnabled && prefs.NewGuidePublishedInApp);
            var emailAllowed = prefs is null || (prefs.EmailEnabled && prefs.NewGuidePublishedEmail);
            if (!inAppAllowed && !emailAllowed) continue;
            db.BackgroundJobs.Add(Job($"notification:release:{release.Id}:{buyer}", new NotificationDeliveryPayload(buyer, NotificationKind.NewGuidePublished, $"Update v{release.VersionNumber} for {guide.Title}", TrimForNotification(release.Changelog), guide.Slug, guide.Id), clock));
        }
        var jobs = db.ChangeTracker.Entries<BackgroundJob>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToList();
        if (jobs.Count > 0)
        {
            await db.SaveChangesAsync();
            foreach (var job in jobs) await ExecuteInline(job, processor);
        }
        else await db.SaveChangesAsync();
    }

    private static BackgroundJob Job(string key, NotificationDeliveryPayload payload, IClock clock) => new()
    {
        Id = Guid.NewGuid(), Type = BackgroundJobTypes.NotificationDelivery,
        Payload = System.Text.Json.JsonSerializer.Serialize(payload), IdempotencyKey = key,
        AvailableAt = clock.UtcNow, CreatedAt = clock.UtcNow,
    };

    private static Task ExecuteInline(BackgroundJob job, BackgroundJobProcessor processor)
    {
        job.Status = BackgroundJobStatus.Running; job.Attempts = 1; job.LeaseOwner = "inline"; job.LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2);
        return processor.ExecuteClaimedAsync(job, default);
    }

    private static string TrimForNotification(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= 80 ? value : value[..80] + "…";
    }
}
