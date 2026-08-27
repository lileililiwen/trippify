using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class NotificationsEndpoints
{
    private static readonly Meter NotificationsMeter = new("Trippify.Notifications");
    private static readonly Counter<long> NotificationsCommands = NotificationsMeter.CreateCounter<long>("trippify.notifications.commands");

    public static void MapNotifications(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();
        group.MapPost("/creators/{slug}/follow", FollowCreator);
        group.MapDelete("/creators/{slug}/follow", UnfollowCreator);
        group.MapGet("/creators/{slug}/follow", GetCreatorFollowStatus);
        group.MapGet("/me/notifications", ListNotifications);
        group.MapPost("/me/notifications/{notificationId:guid}/read", MarkNotificationRead);
        group.MapGet("/me/notification-preferences", GetPreferences);
        group.MapPut("/me/notification-preferences", UpdatePreferences);

        var publicGroup = app.MapGroup("/api/v1").AllowAnonymous();
        publicGroup.MapGet("/creators/{slug}/followers/count", GetFollowerCount);
    }

    private static async Task<IResult> FollowCreator(string slug, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var creator = await db.CreatorProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug.ToLower() && x.Status == CreatorStatus.Active);
        if (creator is null) return Results.NotFound();
        if (creator.UserId == user) return Results.ValidationProblem(new Dictionary<string, string[]> { ["creator"] = ["You cannot follow yourself."] });
        var existing = await db.CreatorFollows.AsNoTracking().SingleOrDefaultAsync(x => x.CreatorUserId == creator.UserId && x.FollowerUserId == user);
        if (existing is null)
        {
            db.CreatorFollows.Add(new CreatorFollow { Id = Guid.NewGuid(), CreatorUserId = creator.UserId, FollowerUserId = user, CreatedAt = clock.UtcNow });
            await db.SaveChangesAsync();
            NotificationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "creator-followed"));
        }
        else
        {
            return Results.Ok(new FollowStatusResponse(slug, true, existing.CreatedAt));
        }
        return Results.Created($"/api/v1/creators/{slug}/follow", new FollowStatusResponse(slug, true, clock.UtcNow));
    }

    private static async Task<IResult> UnfollowCreator(string slug, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var creator = await db.CreatorProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug.ToLower());
        if (creator is null) return Results.NotFound();
        var row = await db.CreatorFollows.SingleOrDefaultAsync(x => x.CreatorUserId == creator.UserId && x.FollowerUserId == user);
        if (row is not null)
        {
            db.CreatorFollows.Remove(row);
            await db.SaveChangesAsync();
            NotificationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "creator-unfollowed"));
        }
        return Results.NoContent();
    }

    private static async Task<IResult> GetCreatorFollowStatus(string slug, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var creator = await db.CreatorProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug.ToLower());
        if (creator is null) return Results.NotFound();
        var existing = await db.CreatorFollows.AsNoTracking().SingleOrDefaultAsync(x => x.CreatorUserId == creator.UserId && x.FollowerUserId == user);
        return Results.Ok(new FollowStatusResponse(slug, existing is not null, existing?.CreatedAt));
    }

    private static async Task<IResult> GetFollowerCount(string slug, AppDbContext db)
    {
        var creator = await db.CreatorProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Slug == slug.ToLower() && x.Status == CreatorStatus.Active);
        if (creator is null) return Results.NotFound();
        var count = await db.CreatorFollows.AsNoTracking().CountAsync(x => x.CreatorUserId == creator.UserId);
        return Results.Ok(new FollowerCountResponse(slug, count));
    }

    private static async Task<IResult> ListNotifications(int? limit, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var pageSize = Math.Clamp(limit ?? 50, 1, 200);
        var notifications = await db.Notifications.AsNoTracking()
            .Where(x => x.UserId == user)
            .OrderByDescending(x => x.CreatedAt)
            .Take(pageSize)
            .Select(x => new NotificationResponse(x.Id, x.Kind.ToString(), x.Title, x.Body, x.TargetSlug, x.TargetGuideId, x.CreatedAt, x.ReadAt))
            .ToListAsync();
        var unread = await db.Notifications.AsNoTracking().CountAsync(x => x.UserId == user && x.ReadAt == null);
        NotificationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "notifications-listed"));
        return Results.Ok(new NotificationListResponse(unread, notifications));
    }

    private static async Task<IResult> MarkNotificationRead(Guid notificationId, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var row = await db.Notifications.SingleOrDefaultAsync(x => x.Id == notificationId && x.UserId == user);
        if (row is null) return Results.NotFound();
        if (row.ReadAt is null)
        {
            row.ReadAt = clock.UtcNow;
            await db.SaveChangesAsync();
            NotificationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "notification-read"));
        }
        return Results.NoContent();
    }

    private static async Task<IResult> GetPreferences(ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var prefs = await db.NotificationPreferences.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user);
        if (prefs is null) prefs = DefaultsFor(user);
        return Results.Ok(MapPreferences(prefs));
    }

    private static async Task<IResult> UpdatePreferences(UpdatePreferencesRequest request, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var prefs = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == user);
        if (prefs is null)
        {
            prefs = DefaultsFor(user);
            db.NotificationPreferences.Add(prefs);
        }
        prefs.EmailEnabled = request.EmailEnabled;
        prefs.InAppEnabled = request.InAppEnabled;
        prefs.NewGuidePublishedEmail = request.NewGuidePublishedEmail;
        prefs.NewGuidePublishedInApp = request.NewGuidePublishedInApp;
        prefs.NewReviewOnMyGuideEmail = request.NewReviewOnMyGuideEmail;
        prefs.NewReviewOnMyGuideInApp = request.NewReviewOnMyGuideInApp;
        prefs.NewReplyToReviewEmail = request.NewReplyToReviewEmail;
        prefs.NewReplyToReviewInApp = request.NewReplyToReviewInApp;
        prefs.FollowerGainedEmail = request.FollowerGainedEmail;
        prefs.FollowerGainedInApp = request.FollowerGainedInApp;
        prefs.EvidenceReviewedEmail = request.EvidenceReviewedEmail;
        prefs.EvidenceReviewedInApp = request.EvidenceReviewedInApp;
        await db.SaveChangesAsync();
        NotificationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "preferences-updated"));
        return Results.Ok(MapPreferences(prefs));
    }

    private static NotificationPreference DefaultsFor(Guid userId) => new()
    {
        UserId = userId,
        EmailEnabled = true,
        InAppEnabled = true,
    };

    private static NotificationPreferencesResponse MapPreferences(NotificationPreference prefs) => new(
        prefs.EmailEnabled,
        prefs.InAppEnabled,
        prefs.NewGuidePublishedEmail,
        prefs.NewGuidePublishedInApp,
        prefs.NewReviewOnMyGuideEmail,
        prefs.NewReviewOnMyGuideInApp,
        prefs.NewReplyToReviewEmail,
        prefs.NewReplyToReviewInApp,
        prefs.FollowerGainedEmail,
        prefs.FollowerGainedInApp,
        prefs.EvidenceReviewedEmail,
        prefs.EvidenceReviewedInApp);
}

public sealed record FollowStatusResponse(string Slug, bool Following, DateTimeOffset? FollowedAt);
public sealed record FollowerCountResponse(string Slug, int Followers);
public sealed record NotificationResponse(Guid Id, string Kind, string Title, string Body, string? TargetSlug, Guid? TargetGuideId, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt);
public sealed record NotificationListResponse(int UnreadCount, IReadOnlyList<NotificationResponse> Items);
public sealed record NotificationPreferencesResponse(
    bool EmailEnabled,
    bool InAppEnabled,
    bool NewGuidePublishedEmail,
    bool NewGuidePublishedInApp,
    bool NewReviewOnMyGuideEmail,
    bool NewReviewOnMyGuideInApp,
    bool NewReplyToReviewEmail,
    bool NewReplyToReviewInApp,
    bool FollowerGainedEmail,
    bool FollowerGainedInApp,
    bool EvidenceReviewedEmail,
    bool EvidenceReviewedInApp);
public sealed record UpdatePreferencesRequest(
    bool EmailEnabled,
    bool InAppEnabled,
    bool NewGuidePublishedEmail,
    bool NewGuidePublishedInApp,
    bool NewReviewOnMyGuideEmail,
    bool NewReviewOnMyGuideInApp,
    bool NewReplyToReviewEmail,
    bool NewReplyToReviewInApp,
    bool FollowerGainedEmail,
    bool FollowerGainedInApp,
    bool EvidenceReviewedEmail,
    bool EvidenceReviewedInApp);
