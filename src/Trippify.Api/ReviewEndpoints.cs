using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class ReviewEndpoints
{
    private static readonly Meter ReviewMeter = new("Trippify.Reviews");
    private static readonly Counter<long> ReviewCommands = ReviewMeter.CreateCounter<long>("trippify.review.commands");
    public static void MapReview(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1").RequireAuthorization();
        group.MapPost("/guides/{guideId:guid}/reviews", SubmitReview);
        group.MapPut("/reviews/{reviewId:guid}", EditReview);
        group.MapDelete("/reviews/{reviewId:guid}", DeleteReview);
        group.MapPost("/reviews/{reviewId:guid}/reply", ReplyToReview);
        group.MapPost("/reviews/{reviewId:guid}/reports", ReportReview);
        group.MapPost("/guides/{guideId:guid}/feedback", SubmitFeedback);
        var publicGroup = app.MapGroup("/api/v1").AllowAnonymous();
        publicGroup.MapGet("/guides/{guideId:guid}/reviews", ListReviews);
        var adminGroup = app.MapGroup("/api/v1/admin").RequireAuthorization(p => p.RequireRole("Administrator"));
        adminGroup.MapPut("/reviews/{reviewId:guid}/moderate", ModerateReview);
    }

    private static async Task<IResult> SubmitReview(Guid guideId, SubmitReviewRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        if (guide.OwnerUserId == user) return Results.ValidationProblem(new Dictionary<string, string[]> { ["guide"] = ["Creators cannot review their own guide."] });
        if (!await db.PurchaseEntitlements.AsNoTracking().AnyAsync(x => x.GuideId == guideId && x.UserId == user && x.RevokedAt == null) && guide.Lifecycle != GuideLifecycle.FreePublic)
            return Results.Forbid();
        if (await db.GuideReviews.AnyAsync(x => x.GuideId == guideId && x.UserId == user))
            return Results.Conflict(new Dictionary<string, string[]> { ["review"] = ["You already reviewed this guide."] });
        var errors = ValidateReview(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var now = clock.UtcNow;
        var review = new GuideReview { Id = Guid.NewGuid(), GuideId = guideId, UserId = user, Rating = request.Rating, Body = request.Body.Trim(), CreatedAt = now, UpdatedAt = now };
        db.GuideReviews.Add(review); await db.SaveChangesAsync();
        ReviewCommands.Add(1, new KeyValuePair<string, object?>("operation", "review-submitted"));
        return Results.Created($"/api/v1/guides/{guideId}/reviews", new ReviewResponse(review.Id, review.GuideId, review.UserId, review.Rating, review.Body, review.ModerationStatus.ToString(), review.CreatedAt, review.UpdatedAt, null));
    }

    private static async Task<IResult> EditReview(Guid reviewId, EditReviewRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var review = await db.GuideReviews.SingleOrDefaultAsync(x => x.Id == reviewId && x.UserId == user);
        if (review is null) return Results.NotFound();
        var errors = ValidateReview(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
        review.Rating = request.Rating; review.Body = request.Body.Trim(); review.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        ReviewCommands.Add(1, new KeyValuePair<string, object?>("operation", "review-edited"));
        return Results.Ok(new ReviewResponse(review.Id, review.GuideId, review.UserId, review.Rating, review.Body, review.ModerationStatus.ToString(), review.CreatedAt, review.UpdatedAt, null));
    }

    private static async Task<IResult> DeleteReview(Guid reviewId, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var review = await db.GuideReviews.SingleOrDefaultAsync(x => x.Id == reviewId && x.UserId == user);
        if (review is null) return Results.NotFound();
        db.GuideReviews.Remove(review); await db.SaveChangesAsync();
        ReviewCommands.Add(1, new KeyValuePair<string, object?>("operation", "review-deleted"));
        return Results.NoContent();
    }

    private static async Task<IResult> ListReviews(Guid guideId, ClaimsPrincipal principal, AppDbContext db)
    {
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        var query = db.GuideReviews.AsNoTracking().Where(x => x.GuideId == guideId && x.ModerationStatus == ReviewModerationStatus.Visible);
        var userId = principal.Identity?.IsAuthenticated == true ? IdentityEndpoints.CurrentUserId(principal) : (Guid?)null;
        var reviews = await query.OrderByDescending(x => x.CreatedAt).Select(r => new ReviewResponse(r.Id, r.GuideId, r.UserId, r.Rating, r.Body, r.ModerationStatus.ToString(), r.CreatedAt, r.UpdatedAt, db.ReviewReplies.Where(rp => rp.ReviewId == r.Id).Select(rp => new ReplyResponse(rp.Id, rp.ReviewId, rp.AuthorUserId, rp.Body, rp.CreatedAt, rp.UpdatedAt)).FirstOrDefault())).ToListAsync();
        ReviewCommands.Add(1, new KeyValuePair<string, object?>("operation", "reviews-listed"));
        return Results.Ok(reviews);
    }

    private static async Task<IResult> ReplyToReview(Guid reviewId, ReplyRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var review = await db.GuideReviews.SingleOrDefaultAsync(x => x.Id == reviewId);
        if (review is null) return Results.NotFound();
        var guide = await db.TravelGuides.SingleOrDefaultAsync(x => x.Id == review.GuideId);
        if (guide is null || guide.OwnerUserId != user) return Results.Forbid();
        if (await db.ReviewReplies.AnyAsync(x => x.ReviewId == reviewId))
            return Results.Conflict(new Dictionary<string, string[]> { ["reply"] = ["A reply already exists for this review."] });
        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Trim().Length > 4000)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Reply body must contain 1 to 4000 characters."] });
        var now = clock.UtcNow;
        var reply = new ReviewReply { Id = Guid.NewGuid(), ReviewId = reviewId, AuthorUserId = user, Body = request.Body.Trim(), CreatedAt = now, UpdatedAt = now };
        db.ReviewReplies.Add(reply); await db.SaveChangesAsync();
        ReviewCommands.Add(1, new KeyValuePair<string, object?>("operation", "reply-created"));
        return Results.Created($"/api/v1/reviews/{reviewId}/reply", new ReplyResponse(reply.Id, reply.ReviewId, reply.AuthorUserId, reply.Body, reply.CreatedAt, reply.UpdatedAt));
    }

    private static async Task<IResult> ReportReview(Guid reviewId, ReportRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var review = await db.GuideReviews.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reviewId);
        if (review is null) return Results.NotFound();
        if (review.UserId == user) return Results.ValidationProblem(new Dictionary<string, string[]> { ["review"] = ["You cannot report your own review."] });
        if (await db.ReviewReports.AnyAsync(x => x.ReviewId == reviewId && x.ReporterUserId == user))
            return Results.Conflict(new Dictionary<string, string[]> { ["report"] = ["You already reported this review."] });
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["Reason must contain 1 to 500 characters."] });
        var report = new ReviewReport { Id = Guid.NewGuid(), ReviewId = reviewId, ReporterUserId = user, Reason = request.Reason.Trim(), CreatedAt = clock.UtcNow };
        db.ReviewReports.Add(report); await db.SaveChangesAsync();
        ReviewCommands.Add(1, new KeyValuePair<string, object?>("operation", "report-created"));
        return Results.Created($"/api/v1/reviews/{reviewId}/reports", new { report.Id, report.ReviewId, report.Reason, report.CreatedAt });
    }

    private static async Task<IResult> SubmitFeedback(Guid guideId, FeedbackRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        if (await db.ReviewFeedbacks.AnyAsync(x => x.GuideId == guideId && x.UserId == user))
            return Results.Conflict(new Dictionary<string, string[]> { ["feedback"] = ["You already submitted feedback for this guide."] });
        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Trim().Length > 4000)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["Feedback body must contain 1 to 4000 characters."] });
        var feedback = new ReviewFeedback { Id = Guid.NewGuid(), GuideId = guideId, UserId = user, Body = request.Body.Trim(), CreatedAt = clock.UtcNow };
        db.ReviewFeedbacks.Add(feedback); await db.SaveChangesAsync();
        ReviewCommands.Add(1, new KeyValuePair<string, object?>("operation", "feedback-submitted"));
        return Results.Created($"/api/v1/guides/{guideId}/feedback", new { feedback.Id, feedback.GuideId, feedback.UserId, feedback.Body, feedback.CreatedAt });
    }

    private static async Task<IResult> ModerateReview(Guid reviewId, ModerateRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var review = await db.GuideReviews.SingleOrDefaultAsync(x => x.Id == reviewId);
        if (review is null) return Results.NotFound();
        if (!Enum.TryParse<ReviewModerationStatus>(request.Status, true, out var status))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Unknown moderation status."] });
        review.ModerationStatus = status; review.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        ReviewCommands.Add(1, new KeyValuePair<string, object?>("operation", "review-moderated"));
        return Results.Ok(new { review.Id, status = review.ModerationStatus.ToString() });
    }

    private static Dictionary<string, string[]> ValidateReview(SubmitReviewRequest request) { var errors = new Dictionary<string, string[]>(); if (request.Rating is < 1 or > 5) errors["rating"] = ["Rating must be between 1 and 5."]; if (request.Body.Trim().Length > 4000) errors["body"] = ["Body cannot exceed 4000 characters."]; return errors; }
    private static Dictionary<string, string[]> ValidateReview(EditReviewRequest request) { var errors = new Dictionary<string, string[]>(); if (request.Rating is < 1 or > 5) errors["rating"] = ["Rating must be between 1 and 5."]; if (request.Body.Trim().Length > 4000) errors["body"] = ["Body cannot exceed 4000 characters."]; return errors; }
}

public sealed record SubmitReviewRequest(int Rating, string Body);
public sealed record EditReviewRequest(int Rating, string Body);
public sealed record ReplyRequest(string Body);
public sealed record ReportRequest(string Reason);
public sealed record FeedbackRequest(string Body);
public sealed record ModerateRequest(string Status);
public sealed record ReviewResponse(Guid Id, Guid GuideId, Guid UserId, int Rating, string Body, string ModerationStatus, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, ReplyResponse? Reply);
public sealed record ReplyResponse(Guid Id, Guid ReviewId, Guid AuthorUserId, string Body, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
