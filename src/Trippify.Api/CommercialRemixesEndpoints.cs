using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class CommercialRemixesEndpoints
{
    private static readonly Meter CommercialRemixesMeter = new("Trippify.CommercialRemixes");
    private static readonly Counter<long> CommercialRemixesCommands = CommercialRemixesMeter.CreateCounter<long>("trippify.commercialremixes.commands");

    public static void MapCommercialRemixes(this WebApplication app)
    {
        var meGroup = app.MapGroup("/api/v1/me").RequireAuthorization();
        meGroup.MapPost("/license-policies", UpsertMyLicensePolicy);
        meGroup.MapGet("/license-policies", GetMyLicensePolicies);

        var creatorGroup = app.MapGroup("/api/v1/creators/{slug}").AllowAnonymous();
        creatorGroup.MapGet("/license", GetCreatorLicense);

        var guideGroup = app.MapGroup("/api/v1/guides/{guideId:guid}").RequireAuthorization();
        guideGroup.MapPost("/remix/ancestry", DeclareAncestry);

        var publicGuideGroup = app.MapGroup("/api/v1/guides/{guideId:guid}").AllowAnonymous();
        publicGuideGroup.MapGet("/ancestry", GetGuideAncestry);

        var adminGroup = app.MapGroup("/api/v1/admin").RequireAuthorization(p => p.RequireRole("Administrator"));
        adminGroup.MapPost("/remix-approvals/{ancestryId:guid}/decide", DecideRemixApproval);
        adminGroup.MapGet("/remix-approvals/queue", ListApprovalQueue);
        adminGroup.MapPost("/revenue-shares", RecordRevenueShares);
        adminGroup.MapGet("/revenue-shares", ListRevenueShares);
    }

    private static async Task<IResult> UpsertMyLicensePolicy(UpsertLicensePolicyRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        if (string.IsNullOrWhiteSpace(request.Slug) || request.RoyaltyPercent is < 0 or > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["policy"] = ["Slug is required and royalty must be 0-100."] });
        var slug = request.Slug.Trim().ToLowerInvariant();
        var existing = await db.LicensePolicies.SingleOrDefaultAsync(x => x.OwnerUserId == user && x.Slug == slug);
        if (existing is null)
        {
            existing = new LicensePolicy { Id = Guid.NewGuid(), OwnerUserId = user, Slug = slug, CreatedAt = clock.UtcNow };
            db.LicensePolicies.Add(existing);
        }
        existing.DisplayName = request.DisplayName?.Trim() ?? existing.Slug;
        existing.AllowCommercial = request.AllowCommercial;
        existing.RequireApproval = request.RequireApproval;
        existing.RoyaltyPercent = request.RoyaltyPercent;
        existing.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync();
        CommercialRemixesCommands.Add(1, new KeyValuePair<string, object?>("operation", "license-policy-upserted"));
        return Results.Ok(LicensePolicyResponse(existing));
    }

    private static async Task<IResult> GetMyLicensePolicies(ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var policies = await db.LicensePolicies.AsNoTracking().Where(x => x.OwnerUserId == user).ToListAsync();
        return Results.Ok(policies.Select(LicensePolicyResponse).ToList());
    }

    private static async Task<IResult> GetCreatorLicense(string slug, AppDbContext db)
    {
        var policies = await db.LicensePolicies.AsNoTracking()
            .Where(x => x.Slug == slug.ToLowerInvariant() && x.AllowCommercial)
            .ToListAsync();
        return Results.Ok(policies.Select(LicensePolicyResponse).ToList());
    }

    private static async Task<IResult> DeclareAncestry(Guid guideId, DeclareAncestryRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guideId);
        if (guide is null) return Results.NotFound();
        if (guide.OwnerUserId != user) return Results.Forbid();
        var parent = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ParentGuideId);
        if (parent is null) return Results.NotFound();
        if (parent.Id == guide.Id) return Results.ValidationProblem(new Dictionary<string, string[]> { ["parentGuideId"] = ["Parent must differ from the child."] });
        var policy = await db.LicensePolicies.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.LicensePolicyId);
        if (policy is null) return Results.NotFound();
        var existing = await db.RemixAncestries.AsNoTracking().SingleOrDefaultAsync(x => x.ChildGuideId == guideId);
        if (existing is not null) return Results.Conflict(new Dictionary<string, string[]> { ["ancestry"] = ["Ancestry already declared."] });
        var decision = policy.RequireApproval ? RemixDecision.Pending : RemixDecision.Approved;
        var ancestry = new RemixAncestry
        {
            Id = Guid.NewGuid(),
            ChildGuideId = guideId,
            ParentGuideId = request.ParentGuideId,
            LicensePolicyId = policy.Id,
            AttributionJson = request.AttributionJson ?? "{}",
            Decision = decision,
            CreatedAt = clock.UtcNow,
            DecidedAt = decision == RemixDecision.Approved ? clock.UtcNow : null,
        };
        db.RemixAncestries.Add(ancestry);
        await db.SaveChangesAsync();
        CommercialRemixesCommands.Add(1, new KeyValuePair<string, object?>("operation", "ancestry-declared"));
        return Results.Created($"/api/v1/guides/{guideId}/ancestry", AncestryResponse(ancestry));
    }

    private static async Task<IResult> GetGuideAncestry(Guid guideId, AppDbContext db)
    {
        var ancestry = await db.RemixAncestries.AsNoTracking().FirstOrDefaultAsync(x => x.ChildGuideId == guideId);
        if (ancestry is null) return Results.NotFound();
        return Results.Ok(AncestryResponse(ancestry));
    }

    private static async Task<IResult> DecideRemixApproval(Guid ancestryId, DecideRemixApprovalRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        if (!Enum.TryParse<RemixDecision>(request.Decision, true, out var decision) || decision == RemixDecision.Pending)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["decision"] = ["Decision must be Approved or Rejected."] });
        var approver = IdentityEndpoints.CurrentUserId(principal);
        var ancestry = await db.RemixAncestries.SingleOrDefaultAsync(x => x.Id == ancestryId);
        if (ancestry is null) return Results.NotFound();
        if (ancestry.Decision != RemixDecision.Pending)
            return Results.Ok(AncestryResponse(ancestry));
        ancestry.Decision = decision;
        ancestry.DecidedAt = clock.UtcNow;
        db.RemixApprovals.Add(new RemixApproval
        {
            Id = Guid.NewGuid(),
            RemixAncestryId = ancestry.Id,
            ApproverUserId = approver,
            Decision = decision,
            Reason = request.Reason?.Trim() ?? string.Empty,
            DecidedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync();
        CommercialRemixesCommands.Add(1, new KeyValuePair<string, object?>("operation", "ancestry-decided"));
        return Results.Ok(AncestryResponse(ancestry));
    }

    private static async Task<IResult> ListApprovalQueue(AppDbContext db)
    {
        var pending = await db.RemixAncestries.AsNoTracking()
            .Where(x => x.Decision == RemixDecision.Pending)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
        return Results.Ok(pending.Select(AncestryResponse).ToList());
    }

    private static async Task<IResult> RecordRevenueShares(RecordRevenueSharesRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        if (request.Shares is null || request.Shares.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["shares"] = ["At least one share is required."] });
        var order = await db.GuideOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.OrderId);
        if (order is null) return Results.NotFound();
        if (request.Shares.Any(s => s.CurrencyCode != order.CurrencyCode))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["currencyCode"] = ["All shares must use the order currency."] });
        if (request.Shares.Sum(s => s.Percent) != 100)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["percent"] = ["Revenue shares must total 100 percent."] });
        if (request.Shares.Sum(s => s.AmountMinorUnits) != order.AmountMinorUnits)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["amount"] = ["Revenue amounts must total the order amount."] });
        var existing = await db.RevenueShares.AsNoTracking().Where(x => x.OrderId == order.Id).CountAsync();
        if (existing > 0)
            return Results.Ok(await db.RevenueShares.AsNoTracking().Where(x => x.OrderId == order.Id).ToListAsync().ContinueWith(t => t.Result.Select(RevenueShareResponse).ToList()));
        var created = request.Shares.Select(s => new RevenueShare
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            UserId = s.UserId,
            Percent = s.Percent,
            AmountMinorUnits = s.AmountMinorUnits,
            CurrencyCode = s.CurrencyCode,
            CreatedAt = clock.UtcNow,
        }).ToList();
        db.RevenueShares.AddRange(created);
        await db.SaveChangesAsync();
        CommercialRemixesCommands.Add(1, new KeyValuePair<string, object?>("operation", "revenue-shares-recorded"));
        return Results.Created($"/api/v1/admin/revenue-shares?orderId={order.Id}", created.Select(RevenueShareResponse).ToList());
    }

    private static async Task<IResult> ListRevenueShares(Guid? orderId, AppDbContext db)
    {
        var shares = await db.RevenueShares.AsNoTracking()
            .Where(x => orderId == null || x.OrderId == orderId)
            .ToListAsync();
        return Results.Ok(shares.Select(RevenueShareResponse).ToList());
    }

    private static LicensePolicyResponse LicensePolicyResponse(LicensePolicy p) => new(p.Id, p.OwnerUserId, p.Slug, p.DisplayName, p.AllowCommercial, p.RequireApproval, p.RoyaltyPercent, p.CreatedAt, p.UpdatedAt);
    private static RemixAncestryResponse AncestryResponse(RemixAncestry a) => new(a.Id, a.ChildGuideId, a.ParentGuideId, a.LicensePolicyId, a.AttributionJson, a.Decision.ToString(), a.CreatedAt, a.DecidedAt);
    private static RevenueShareResponse RevenueShareResponse(RevenueShare r) => new(r.Id, r.OrderId, r.UserId, r.Percent, r.AmountMinorUnits, r.CurrencyCode, r.CreatedAt);
}

public sealed record UpsertLicensePolicyRequest(string Slug, string? DisplayName, bool AllowCommercial, bool RequireApproval, int RoyaltyPercent);
public sealed record DeclareAncestryRequest(Guid ParentGuideId, Guid LicensePolicyId, string? AttributionJson);
public sealed record DecideRemixApprovalRequest(string Decision, string? Reason);
public sealed record RevenueShareRequest(Guid UserId, int Percent, long AmountMinorUnits, string CurrencyCode);
public sealed record RecordRevenueSharesRequest(Guid OrderId, IReadOnlyList<RevenueShareRequest> Shares);
public sealed record LicensePolicyResponse(Guid Id, Guid OwnerUserId, string Slug, string DisplayName, bool AllowCommercial, bool RequireApproval, int RoyaltyPercent, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record RemixAncestryResponse(Guid Id, Guid ChildGuideId, Guid ParentGuideId, Guid LicensePolicyId, string AttributionJson, string Decision, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt);
public sealed record RevenueShareResponse(Guid Id, Guid OrderId, Guid UserId, int Percent, long AmountMinorUnits, string CurrencyCode, DateTimeOffset CreatedAt);
