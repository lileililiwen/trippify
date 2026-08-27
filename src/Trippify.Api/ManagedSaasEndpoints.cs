using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class ManagedSaasEndpoints
{
    private const string DefaultBrandJson = "{\"primary\":\"#1f7a3a\",\"logo\":null}";
    private static readonly Meter ManagedSaasMeter = new("Trippify.ManagedSaas");
    private static readonly Counter<long> ManagedSaasCommands = ManagedSaasMeter.CreateCounter<long>("trippify.managedsaas.commands");

    public static void MapManagedSaas(this WebApplication app)
    {
        var adminGroup = app.MapGroup("/api/v1/admin/tenants").RequireAuthorization(p => p.RequireRole("Administrator"));
        adminGroup.MapPost("", CreateTenant);
        adminGroup.MapGet("", ListTenants);
        adminGroup.MapGet("/{tenantId:guid}", GetTenant);
        adminGroup.MapPut("/{tenantId:guid}", UpdateTenant);
        adminGroup.MapPost("/{tenantId:guid}/suspend", SuspendTenant);
        adminGroup.MapPut("/{tenantId:guid}/quotas/{metric}", SetQuota);
        adminGroup.MapPost("/{tenantId:guid}/quotas/{metric}/adjustments", AdjustQuota);
        adminGroup.MapGet("/{tenantId:guid}/audit", ListTenantAudit);

        var tenantGroup = app.MapGroup("/api/v1/me/tenant").RequireAuthorization();
        tenantGroup.MapGet("", GetMyTenant);
        tenantGroup.MapPost("/subscription", UpdateSubscription);
        tenantGroup.MapGet("/quotas", ListMyQuotas);
        tenantGroup.MapPost("/export", RequestExport);
        tenantGroup.MapPost("/deletion", RequestDeletion);
    }

    public static async Task EnsureTenantForUserAsync(AppDbContext db, Guid userId, IClock clock)
    {
        var membership = await db.TenantMembers.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == userId);
        if (membership is not null)
        {
            await EnsurePlanQuotasAsync(db, membership.TenantId, clock);
            return;
        }
        var emailPrefix = userId.ToString("N");
        var slug = $"tenant-{emailPrefix}";
        if (await db.Tenants.AnyAsync(x => x.Slug == slug)) slug = $"{slug}-{Guid.NewGuid():N}".Substring(0, 60);
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = slug,
            DisplayName = "Default Tenant",
            PrimaryDomain = string.Empty,
            BrandingJson = DefaultBrandJson,
            Status = TenantStatus.Active,
            CreatedAt = clock.UtcNow,
        };
        db.Tenants.Add(tenant);
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Plan = SubscriptionPlan.Free,
            Status = SubscriptionStatus.Active,
            StartsAt = clock.UtcNow,
        });
        db.TenantMembers.Add(new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = userId,
            Role = "Owner",
            CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync();
        await EnsurePlanQuotasAsync(db, tenant.Id, clock);
    }

    private static async Task<IResult> CreateTenant(CreateTenantRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(request.Slug) || string.IsNullOrWhiteSpace(request.DisplayName))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tenant"] = ["Slug and displayName are required."] });
        if (await db.Tenants.AsNoTracking().AnyAsync(x => x.Slug == request.Slug))
            return Results.Conflict(new Dictionary<string, string[]> { ["slug"] = ["Tenant slug is in use."] });
        if (!string.IsNullOrWhiteSpace(request.PrimaryDomain) && await db.Tenants.AsNoTracking().AnyAsync(x => x.PrimaryDomain == request.PrimaryDomain))
            return Results.Conflict(new Dictionary<string, string[]> { ["primaryDomain"] = ["Domain is already mapped."] });
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Slug = request.Slug.Trim().ToLowerInvariant(),
            DisplayName = string.Empty,
            PrimaryDomain = request.PrimaryDomain?.Trim() ?? string.Empty,
            BrandingJson = DefaultBrandJson,
            Status = TenantStatus.Active,
            CreatedAt = clock.UtcNow,
        };
        tenant.DisplayName = request.DisplayName.Trim();
        db.Tenants.Add(tenant);
        db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Plan = SubscriptionPlan.Free,
            Status = SubscriptionStatus.Active,
            StartsAt = clock.UtcNow,
        });
        db.TenantAuditEntries.Add(Audit(tenant.Id, IdentityEndpoints.CurrentUserId(principal), "tenant-created", "admin-bootstrap"));
        await db.SaveChangesAsync();
        await EnsurePlanQuotasAsync(db, tenant.Id, clock);
        ManagedSaasCommands.Add(1, new KeyValuePair<string, object?>("operation", "tenant-created"));
        return Results.Created($"/api/v1/admin/tenants/{tenant.Id}", TenantResponse(tenant));
    }

    private static async Task<IResult> ListTenants(int? limit, AppDbContext db)
    {
        var pageSize = Math.Clamp(limit ?? 50, 1, 200);
        var tenants = await db.Tenants.AsNoTracking()
            .OrderBy(x => x.Slug)
            .Take(pageSize)
            .ToListAsync();
        return Results.Ok(new TenantListResponse(tenants.Count, tenants.Select(TenantResponse).ToList()));
    }

    private static async Task<IResult> GetTenant(Guid tenantId, AppDbContext db)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return Results.NotFound();
        return Results.Ok(TenantResponse(tenant));
    }

    private static async Task<IResult> UpdateTenant(Guid tenantId, UpdateTenantRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return Results.NotFound();
        if (!string.IsNullOrWhiteSpace(request.DisplayName)) tenant.DisplayName = request.DisplayName.Trim();
        if (request.PrimaryDomain is not null) tenant.PrimaryDomain = request.PrimaryDomain.Trim();
        if (!string.IsNullOrWhiteSpace(request.BrandingJson)) tenant.BrandingJson = request.BrandingJson.Trim();
        tenant.Status = TenantStatus.Active;
        db.TenantAuditEntries.Add(Audit(tenant.Id, IdentityEndpoints.CurrentUserId(principal), "tenant-updated", "admin"));
        await db.SaveChangesAsync();
        ManagedSaasCommands.Add(1, new KeyValuePair<string, object?>("operation", "tenant-updated"));
        return Results.Ok(TenantResponse(tenant));
    }

    private static async Task<IResult> SuspendTenant(Guid tenantId, TenantActionRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(request.Reason)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["Reason is required."] });
        tenant.Status = TenantStatus.Suspended;
        db.TenantAuditEntries.Add(Audit(tenant.Id, IdentityEndpoints.CurrentUserId(principal), "tenant-suspended", request.Reason.Trim()));
        await db.SaveChangesAsync();
        ManagedSaasCommands.Add(1, new KeyValuePair<string, object?>("operation", "tenant-suspended"));
        return Results.NoContent();
    }

    private static async Task<IResult> SetQuota(Guid tenantId, string metric, SetQuotaRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return Results.NotFound();
        try { metric = QuotaMetrics.Canonical(metric); }
        catch (ArgumentOutOfRangeException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["metric"] = ["Unknown quota metric."] }); }
        if (request.Limit < 0) return Results.ValidationProblem(new Dictionary<string, string[]> { ["limit"] = ["Limit must be zero or positive."] });
        var periodStart = await EnsureQuotaAsync(db, tenantId, metric, clock);
        var quota = await db.QuotaUsages.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Metric == metric && x.PeriodStart == periodStart);
        if (quota is null)
        {
            quota = new QuotaUsage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Metric = metric,
                Used = 0,
                Limit = request.Limit,
                IsCustomLimit = true,
                PeriodStart = periodStart,
                PeriodEnd = periodStart.AddMonths(1),
            };
            db.QuotaUsages.Add(quota);
        }
        else
        {
            quota.Limit = request.Limit;
            quota.IsCustomLimit = true;
        }
        db.TenantAuditEntries.Add(Audit(tenant.Id, IdentityEndpoints.CurrentUserId(principal), $"quota-set:{metric}", $"limit={request.Limit}"));
        await db.SaveChangesAsync();
        ManagedSaasCommands.Add(1, new KeyValuePair<string, object?>("operation", "quota-set"));
        return Results.Ok(new QuotaResponse(quota.Metric, quota.Used, quota.Limit, quota.PeriodStart, quota.PeriodEnd));
    }

    private static async Task<IResult> AdjustQuota(Guid tenantId, string metric, AdjustQuotaRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        try { metric = QuotaMetrics.Canonical(metric); }
        catch (ArgumentOutOfRangeException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["metric"] = ["Unknown quota metric."] }); }
        if (request.Amount == 0 || string.IsNullOrWhiteSpace(request.Reason))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["adjustment"] = ["A non-zero amount and reason are required."] });
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tenantId);
        if (tenant is null) return Results.NotFound();
        var period = await EnsureQuotaAsync(db, tenantId, metric, clock);
        var quota = await db.QuotaUsages.SingleAsync(x => x.TenantId == tenantId && x.Metric == metric && x.PeriodStart == period);
        if (quota.Used + request.Amount < 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["amount"] = ["Adjustment cannot make usage negative."] });
        quota.Used += request.Amount;
        var actor = IdentityEndpoints.CurrentUserId(principal);
        db.QuotaHistoryEntries.Add(new QuotaHistoryEntry { Id = Guid.NewGuid(), TenantId = tenantId, ActorUserId = actor, Metric = metric, Kind = QuotaHistoryKind.Adjusted, Amount = request.Amount, Reason = request.Reason.Trim(), OccurredAt = clock.UtcNow });
        db.TenantAuditEntries.Add(Audit(tenantId, actor, $"quota-adjusted:{metric}", $"amount={request.Amount}; reason={request.Reason.Trim()}"));
        await db.SaveChangesAsync();
        return Results.Ok(new QuotaResponse(quota.Metric, quota.Used, quota.Limit, quota.PeriodStart, quota.PeriodEnd));
    }

    private static async Task<IResult> ListTenantAudit(Guid tenantId, int? limit, AppDbContext db)
    {
        var pageSize = Math.Clamp(limit ?? 50, 1, 200);
        var entries = await db.TenantAuditEntries.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.OccurredAt)
            .Take(pageSize)
            .ToListAsync();
        return Results.Ok(new TenantAuditListResponse(entries.Count, entries.Select(ToAuditResponse).ToList()));
    }

    private static async Task<IResult> GetMyTenant(ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        await EnsureTenantForUserAsync(db, user, clock);
        var member = await db.TenantMembers.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == user);
        if (member is null) return Results.NotFound();
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == member.TenantId);
        var subscription = await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenant.Id);
        return Results.Ok(new TenantDashboardResponse(TenantResponse(tenant), SubscriptionResponse(subscription)));
    }

    private static async Task<IResult> UpdateSubscription(UpdateSubscriptionRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        if (!Enum.TryParse<SubscriptionPlan>(request.Plan, true, out var plan))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["plan"] = ["Plan must be Free, Pro, or Enterprise."] });
        var user = IdentityEndpoints.CurrentUserId(principal);
        await EnsureTenantForUserAsync(db, user, clock);
        var member = await db.TenantMembers.AsNoTracking().SingleAsync(m => m.UserId == user);
        var subscription = await db.Subscriptions.SingleOrDefaultAsync(x => x.TenantId == member.TenantId);
        if (subscription is null)
        {
            subscription = new Subscription { Id = Guid.NewGuid(), TenantId = member.TenantId, Plan = plan, Status = SubscriptionStatus.Active, StartsAt = clock.UtcNow };
            db.Subscriptions.Add(subscription);
        }
        else if (subscription.Plan == plan)
        {
            return Results.Ok(SubscriptionResponse(subscription));
        }
        else
        {
            subscription.Plan = plan;
        }
        var currentPeriod = new DateTimeOffset(clock.UtcNow.Year, clock.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var planQuotas = await db.QuotaUsages.Where(x => x.TenantId == member.TenantId && x.PeriodStart == currentPeriod && !x.IsCustomLimit).ToListAsync();
        foreach (var quota in planQuotas) quota.Limit = QuotaMetrics.Limit(plan, quota.Metric);
        db.TenantAuditEntries.Add(Audit(member.TenantId, user, "subscription-updated", plan.ToString()));
        await db.SaveChangesAsync();
        ManagedSaasCommands.Add(1, new KeyValuePair<string, object?>("operation", "subscription-updated"));
        return Results.Ok(SubscriptionResponse(subscription));
    }

    private static async Task<IResult> ListMyQuotas(ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        await EnsureTenantForUserAsync(db, user, clock);
        var member = await db.TenantMembers.AsNoTracking().SingleAsync(m => m.UserId == user);
        var quotas = await db.QuotaUsages.AsNoTracking()
            .Where(x => x.TenantId == member.TenantId)
            .ToListAsync();
        return Results.Ok(new QuotaListResponse(quotas.Count, quotas.Select(q => new QuotaResponse(q.Metric, q.Used, q.Limit, q.PeriodStart, q.PeriodEnd)).ToList()));
    }

    private static async Task<IResult> RequestExport(ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        await EnsureTenantForUserAsync(db, user, clock);
        var member = await db.TenantMembers.AsNoTracking().SingleAsync(m => m.UserId == user);
        var tenantId = member.TenantId;
        var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user);
        var purchases = await db.PurchaseEntitlements.AsNoTracking().Where(x => x.UserId == user).ToListAsync();
        var export = new ExportPayloadResponse(
            user,
            profile?.DisplayName ?? string.Empty,
            profile?.Locale,
            purchases.Select(p => new ExportPurchaseRow(p.GuideId, p.OrderId, p.GrantedAt, p.RevokedAt)).ToList());
        db.TenantAuditEntries.Add(Audit(tenantId, user, "export-requested", $"purchases={purchases.Count}"));
        await db.SaveChangesAsync();
        ManagedSaasCommands.Add(1, new KeyValuePair<string, object?>("operation", "tenant-export"));
        return Results.Ok(export);
    }

    private static async Task<IResult> RequestDeletion(TenantActionRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        await EnsureTenantForUserAsync(db, user, clock);
        var member = await db.TenantMembers.AsNoTracking().SingleAsync(m => m.UserId == user);
        if (string.IsNullOrWhiteSpace(request.Reason)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["reason"] = ["Reason is required."] });
        db.TenantAuditEntries.Add(Audit(member.TenantId, user, "deletion-requested", request.Reason.Trim()));
        await db.SaveChangesAsync();
        ManagedSaasCommands.Add(1, new KeyValuePair<string, object?>("operation", "tenant-deletion-request"));
        return Results.Ok(new { status = "Deletion recorded" });
    }

    private static async Task<DateTimeOffset> EnsureQuotaAsync(AppDbContext db, Guid tenantId, string metric, IClock clock)
    {
        var now = clock.UtcNow;
        var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var existing = await db.QuotaUsages.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Metric == metric && x.PeriodStart == periodStart);
        if (existing is null)
        {
            var plan = await db.Subscriptions.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => (SubscriptionPlan?)x.Plan).SingleOrDefaultAsync() ?? SubscriptionPlan.Free;
            db.QuotaUsages.Add(new QuotaUsage
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Metric = metric,
                Used = 0,
                Limit = QuotaMetrics.Limit(plan, metric),
                PeriodStart = periodStart,
                PeriodEnd = periodStart.AddMonths(1),
            });
            await db.SaveChangesAsync();
        }
        return periodStart;
    }

    private static async Task EnsurePlanQuotasAsync(AppDbContext db, Guid tenantId, IClock clock)
    {
        var plan = await db.Subscriptions.AsNoTracking().Where(x => x.TenantId == tenantId).Select(x => (SubscriptionPlan?)x.Plan).SingleOrDefaultAsync() ?? SubscriptionPlan.Free;
        foreach (var metric in QuotaMetrics.All) await EnsureQuotaAsync(db, tenantId, metric, clock);
        var period = new DateTimeOffset(clock.UtcNow.Year, clock.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var rows = await db.QuotaUsages.Where(x => x.TenantId == tenantId && x.PeriodStart == period && !x.IsCustomLimit).ToListAsync();
        foreach (var row in rows) row.Limit = QuotaMetrics.Limit(plan, row.Metric);
        await db.SaveChangesAsync();
    }

    private static TenantResponse TenantResponse(Tenant t) => new(t.Id, t.Slug, t.DisplayName, t.PrimaryDomain, t.Status.ToString(), t.BrandingJson, t.CreatedAt);
    private static SubscriptionResponse SubscriptionResponse(Subscription? s) => new(s?.Id ?? Guid.Empty, s?.Plan.ToString() ?? SubscriptionPlan.Free.ToString(), s?.Status.ToString() ?? SubscriptionStatus.Active.ToString(), s?.StartsAt ?? DateTimeOffset.UtcNow, s?.EndsAt);
    private static TenantAuditEntryResponse ToAuditResponse(TenantAuditEntry e) => new(e.Id, e.TenantId, e.ActorUserId, e.Action, e.Reason, e.OccurredAt);
    private static TenantAuditEntry Audit(Guid tenantId, Guid actor, string action, string reason) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ActorUserId = actor,
        Action = action,
        Reason = reason,
        OccurredAt = DateTimeOffset.UtcNow,
    };
}

public sealed record CreateTenantRequest(string Slug, string DisplayName, string? PrimaryDomain);
public sealed record UpdateTenantRequest(string? DisplayName, string? PrimaryDomain, string? BrandingJson);
public sealed record TenantActionRequest(string Reason);
public sealed record SetQuotaRequest(int Limit);
public sealed record AdjustQuotaRequest(int Amount, string Reason);
public sealed record UpdateSubscriptionRequest(string Plan);
public sealed record TenantResponse(Guid Id, string Slug, string DisplayName, string PrimaryDomain, string Status, string BrandingJson, DateTimeOffset CreatedAt);
public sealed record TenantListResponse(int Total, IReadOnlyList<TenantResponse> Items);
public sealed record SubscriptionResponse(Guid Id, string Plan, string Status, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);
public sealed record TenantDashboardResponse(TenantResponse Tenant, SubscriptionResponse Subscription);
public sealed record QuotaResponse(string Metric, int Used, int Limit, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd);
public sealed record QuotaListResponse(int Total, IReadOnlyList<QuotaResponse> Items);
public sealed record TenantAuditEntryResponse(Guid Id, Guid TenantId, Guid ActorUserId, string Action, string Reason, DateTimeOffset OccurredAt);
public sealed record TenantAuditListResponse(int Total, IReadOnlyList<TenantAuditEntryResponse> Items);
public sealed record ExportPurchaseRow(Guid GuideId, Guid OrderId, DateTimeOffset GrantedAt, DateTimeOffset? RevokedAt);
public sealed record ExportPayloadResponse(Guid UserId, string DisplayName, string? Locale, IReadOnlyList<ExportPurchaseRow> Purchases);
