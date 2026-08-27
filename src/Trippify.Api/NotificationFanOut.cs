using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class NotificationFanOut
{
    public static async Task QueueReviewSubmittedAsync(AppDbContext db, GuideReview review, TravelGuide guide, IClock clock)
    {
        if (guide.OwnerUserId == review.UserId) return;
        var prefs = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == guide.OwnerUserId);
        var inAppAllowed = prefs is null || (prefs.InAppEnabled && prefs.NewReviewOnMyGuideInApp);
        var emailAllowed = prefs is null || (prefs.EmailEnabled && prefs.NewReviewOnMyGuideEmail);
        if (!inAppAllowed && !emailAllowed) return;
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = guide.OwnerUserId,
            Kind = NotificationKind.NewReviewOnMyGuide,
            Title = "New review on your guide",
            Body = $"Rating {review.Rating}/5 — {TrimForNotification(review.Body)}",
            TargetSlug = guide.Slug,
            TargetGuideId = guide.Id,
            CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static string TrimForNotification(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= 80 ? value : value[..80] + "…";
    }
}
