using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class OperationsEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;
    private static readonly Meter OperationsMeter = new("Trippify.Operations");
    private static readonly Counter<long> OperationsCommands = OperationsMeter.CreateCounter<long>("trippify.operations.commands");

    public static void MapOperations(this WebApplication app)
    {
        var creatorGroup = app.MapGroup("/api/v1/creator/dashboard").RequireAuthorization();
        creatorGroup.MapGet("/overview", GetCreatorDashboardOverview);
        creatorGroup.MapGet("/orders", ListCreatorOrders);
        creatorGroup.MapGet("/reviews", GetCreatorReviews);

        var adminGroup = app.MapGroup("/api/v1/admin/operations").RequireAuthorization(p => p.RequireRole("Administrator"));
        adminGroup.MapGet("/audit", ListAuditEntries);
        adminGroup.MapGet("/users", ListAdminUsers);
        adminGroup.MapGet("/creators", ListAdminCreators);
    }

    private static async Task<IResult> GetCreatorDashboardOverview(ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var creator = IdentityEndpoints.CurrentUserId(principal);
        var creatorProfile = await db.CreatorProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == creator);
        if (creatorProfile is null) return Results.Forbid();
        var guides = await db.TravelGuides.AsNoTracking().Where(x => x.OwnerUserId == creator).Select(x => new { x.Id, x.Lifecycle }).ToListAsync();
        var guideIds = guides.Select(x => x.Id).ToList();
        var orders = await db.GuideOrders.AsNoTracking().Where(x => guideIds.Contains(x.GuideId)).ToListAsync();
        var paidOrders = orders.Where(x => x.Status == OrderStatus.Paid).ToList();
        var refundedOrders = orders.Where(x => x.Status == OrderStatus.Refunded).ToList();
        var revenue = orders.Where(x => x.Status != OrderStatus.Pending && x.Status != OrderStatus.Failed)
            .GroupBy(x => x.CurrencyCode)
            .Select(g => new RevenueByCurrency(
                g.Key,
                g.Sum(x => x.AmountMinorUnits),
                0L,
                g.Sum(x => x.AmountMinorUnits),
                g.Count(x => x.Status == OrderStatus.Paid),
                g.Count(x => x.Status == OrderStatus.Refunded)))
            .ToList();
        var overview = new CreatorDashboardOverview(
            guides.Count,
            guides.Count(x => x.Lifecycle is GuideLifecycle.FreePublic or GuideLifecycle.Paid),
            paidOrders.Count,
            refundedOrders.Count,
            revenue,
            clock.UtcNow);
        OperationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "creator-overview"));
        return Results.Ok(overview);
    }

    private static async Task<IResult> ListCreatorOrders(int? limit, ClaimsPrincipal principal, AppDbContext db)
    {
        var creator = IdentityEndpoints.CurrentUserId(principal);
        var creatorProfile = await db.CreatorProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == creator);
        if (creatorProfile is null) return Results.Forbid();
        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var guideIds = await db.TravelGuides.AsNoTracking().Where(x => x.OwnerUserId == creator).Select(x => x.Id).ToListAsync();
        var rows = await db.GuideOrders.AsNoTracking()
            .Where(x => guideIds.Contains(x.GuideId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(pageSize)
            .Join(db.TravelGuides.AsNoTracking(), order => order.GuideId, guide => guide.Id, (order, guide) => new CreatorOrderRow(order.Id, order.GuideId, guide.Title, order.AmountMinorUnits, order.CurrencyCode, order.Status.ToString(), order.CreatedAt))
            .ToListAsync();
        OperationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "creator-orders-listed"));
        return Results.Ok(new CreatorOrdersResponse(rows.Count, rows));
    }

    private static async Task<IResult> GetCreatorReviews(ClaimsPrincipal principal, AppDbContext db)
    {
        var creator = IdentityEndpoints.CurrentUserId(principal);
        var creatorProfile = await db.CreatorProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == creator);
        if (creatorProfile is null) return Results.Forbid();
        var guideIds = await db.TravelGuides.AsNoTracking().Where(x => x.OwnerUserId == creator).Select(x => x.Id).ToListAsync();
        var reviews = await db.GuideReviews.AsNoTracking().Where(x => guideIds.Contains(x.GuideId)).ToListAsync();
        var reviewIds = reviews.Select(x => x.Id).ToList();
        var openReports = await db.ReviewReports.AsNoTracking().CountAsync(x => reviewIds.Contains(x.ReviewId));
        var summary = new CreatorReviewSummaryRow(
            reviews.Count(x => x.ModerationStatus == ReviewModerationStatus.Visible),
            reviews.Count(x => x.ModerationStatus == ReviewModerationStatus.Flagged),
            reviews.Count(x => x.ModerationStatus == ReviewModerationStatus.Hidden),
            openReports);
        OperationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "creator-review-summary"));
        return Results.Ok(summary);
    }

    private static async Task<IResult> ListAuditEntries(int? limit, AppDbContext db)
    {
        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var entries = await db.IdentityAuditEntries.AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .Take(pageSize)
            .Select(x => new AdminAuditEntryRow(x.Id, x.ActorUserId, x.TargetUserId, x.Action, x.Reason, x.OccurredAt))
            .ToListAsync();
        OperationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "admin-audit-listed"));
        return Results.Ok(new AdminAuditResponse(entries.Count, entries));
    }

    private static async Task<IResult> ListAdminUsers(int? limit, AppDbContext db)
    {
        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var rows = await db.Users.AsNoTracking()
            .OrderBy(x => x.Email)
            .Take(pageSize)
            .Select(x => new AdminUserRow(x.Id, x.Email ?? string.Empty, x.Status.ToString(), x.EmailConfirmed, DateTimeOffset.UtcNow))
            .ToListAsync();
        OperationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "admin-users-listed"));
        return Results.Ok(new AdminUsersResponse(rows.Count, rows));
    }

    private static async Task<IResult> ListAdminCreators(int? limit, AppDbContext db)
    {
        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var rows = await db.CreatorProfiles.AsNoTracking()
            .OrderBy(x => x.Slug)
            .Take(pageSize)
            .Select(x => new AdminCreatorRow(x.UserId, x.Slug, x.Status.ToString(), DateTimeOffset.UtcNow))
            .ToListAsync();
        OperationsCommands.Add(1, new KeyValuePair<string, object?>("operation", "admin-creators-listed"));
        return Results.Ok(new AdminCreatorsResponse(rows.Count, rows));
    }
}
