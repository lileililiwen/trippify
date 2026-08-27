using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class QuotaMetrics
{
    public const string Guides = "Guides";
    public const string AiImports = "AiImports";
    public const string MediaMegabytes = "MediaMegabytes";
    public const string BackgroundJobs = "BackgroundJobs";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Guides, AiImports, MediaMegabytes, BackgroundJobs,
    };

    public static int Limit(SubscriptionPlan plan, string metric) => (plan, Canonical(metric)) switch
    {
        (SubscriptionPlan.Free, Guides) => 3,
        (SubscriptionPlan.Free, AiImports) => 5,
        (SubscriptionPlan.Free, MediaMegabytes) => 100,
        (SubscriptionPlan.Free, BackgroundJobs) => 25,
        (SubscriptionPlan.Pro, Guides) => 100,
        (SubscriptionPlan.Pro, AiImports) => 250,
        (SubscriptionPlan.Pro, MediaMegabytes) => 10_000,
        (SubscriptionPlan.Pro, BackgroundJobs) => 2_500,
        (SubscriptionPlan.Enterprise, _) => 1_000_000,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), "Unknown quota metric."),
    };

    public static string Canonical(string metric) => All.FirstOrDefault(x => x.Equals(metric, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentOutOfRangeException(nameof(metric), "Unknown quota metric.");
}

public sealed record QuotaReservationResult(bool Succeeded, Guid? ReservationId, string Metric, int Used, int Reserved, int Limit, DateTimeOffset ResetAt, string? Failure);

public interface IQuotaService
{
    Task<QuotaReservationResult> ReserveAsync(Guid userId, string metric, int amount, CancellationToken cancellationToken = default);
    Task FinalizeAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task ReleaseAsync(Guid reservationId, string reason, CancellationToken cancellationToken = default);
}

public sealed class QuotaService(AppDbContext db, IClock clock) : IQuotaService
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TenantLocks = new();

    public async Task<QuotaReservationResult> ReserveAsync(Guid userId, string metric, int amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var canonical = QuotaMetrics.Canonical(metric);
        await ManagedSaasEndpoints.EnsureTenantForUserAsync(db, userId, clock);
        var member = await db.TenantMembers.AsNoTracking().SingleAsync(x => x.UserId == userId, cancellationToken);
        var gate = TenantLocks.GetOrAdd(member.TenantId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await BeginTransactionAsync(cancellationToken);
            var tenant = await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == member.TenantId, cancellationToken);
            var now = clock.UtcNow;
            var periodStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var periodEnd = periodStart.AddMonths(1);
            var subscription = await db.Subscriptions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == member.TenantId, cancellationToken);
            var defaultLimit = QuotaMetrics.Limit(subscription?.Plan ?? SubscriptionPlan.Free, canonical);
            var usage = db.Database.IsRelational()
                ? await db.QuotaUsages.FromSqlInterpolated($"SELECT * FROM quota_usages WHERE \"TenantId\" = {member.TenantId} AND \"Metric\" = {canonical} AND \"PeriodStart\" = {periodStart} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
                : await db.QuotaUsages.SingleOrDefaultAsync(x => x.TenantId == member.TenantId && x.Metric == canonical && x.PeriodStart == periodStart, cancellationToken);
            if (usage is null)
            {
                usage = new QuotaUsage { Id = Guid.NewGuid(), TenantId = member.TenantId, Metric = canonical, Limit = defaultLimit, PeriodStart = periodStart, PeriodEnd = periodEnd };
                db.QuotaUsages.Add(usage);
            }
            if (tenant.Status != TenantStatus.Active)
                return new(false, null, canonical, usage.Used, usage.Reserved, usage.Limit, periodEnd, "tenant-suspended");
            if (usage.Used + usage.Reserved + amount > usage.Limit)
                return new(false, null, canonical, usage.Used, usage.Reserved, usage.Limit, periodEnd, "quota-exceeded");
            usage.Reserved += amount;
            var reservation = new QuotaReservation { Id = Guid.NewGuid(), TenantId = member.TenantId, Metric = canonical, Amount = amount, PeriodStart = periodStart, CreatedAt = now };
            db.QuotaReservations.Add(reservation);
            db.QuotaHistoryEntries.Add(History(reservation, QuotaHistoryKind.Reserved, amount, "operation-reserved", now));
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new(true, reservation.Id, canonical, usage.Used, usage.Reserved, usage.Limit, periodEnd, null);
        }
        finally { gate.Release(); }
    }

    public Task FinalizeAsync(Guid reservationId, CancellationToken cancellationToken = default) => CompleteAsync(reservationId, true, "operation-succeeded", cancellationToken);
    public Task ReleaseAsync(Guid reservationId, string reason, CancellationToken cancellationToken = default) => CompleteAsync(reservationId, false, reason, cancellationToken);

    private async Task CompleteAsync(Guid reservationId, bool finalize, string reason, CancellationToken cancellationToken)
    {
        var summary = await db.QuotaReservations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (summary is null) return;
        var gate = TenantLocks.GetOrAdd(summary.TenantId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction = await BeginTransactionAsync(cancellationToken);
            var reservation = db.Database.IsRelational()
                ? await db.QuotaReservations.FromSqlInterpolated($"SELECT * FROM quota_reservations WHERE \"Id\" = {reservationId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
                : await db.QuotaReservations.SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
            if (reservation is null || reservation.Status != QuotaReservationStatus.Reserved) return;
            var usage = db.Database.IsRelational()
                ? await db.QuotaUsages.FromSqlInterpolated($"SELECT * FROM quota_usages WHERE \"TenantId\" = {reservation.TenantId} AND \"Metric\" = {reservation.Metric} AND \"PeriodStart\" = {reservation.PeriodStart} FOR UPDATE").SingleAsync(cancellationToken)
                : await db.QuotaUsages.SingleAsync(x => x.TenantId == reservation.TenantId && x.Metric == reservation.Metric && x.PeriodStart == reservation.PeriodStart, cancellationToken);
            usage.Reserved -= reservation.Amount;
            if (finalize) usage.Used += reservation.Amount;
            reservation.Status = finalize ? QuotaReservationStatus.Finalized : QuotaReservationStatus.Released;
            reservation.CompletedAt = clock.UtcNow;
            db.QuotaHistoryEntries.Add(History(reservation, finalize ? QuotaHistoryKind.Finalized : QuotaHistoryKind.Released, reservation.Amount, reason, clock.UtcNow));
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        finally { gate.Release(); }
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken) => db.Database.IsRelational()
        ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
        : null;

    private static QuotaHistoryEntry History(QuotaReservation reservation, QuotaHistoryKind kind, int amount, string reason, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), TenantId = reservation.TenantId, ReservationId = reservation.Id, Metric = reservation.Metric,
        Kind = kind, Amount = amount, Reason = reason, OccurredAt = now,
    };
}

public static class QuotaProblem
{
    public static IResult From(QuotaReservationResult result) => Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: result.Failure == "tenant-suspended" ? "Tenant suspended" : "Quota exceeded",
        type: result.Failure,
        extensions: new Dictionary<string, object?>
        {
            ["code"] = result.Failure,
            ["metric"] = result.Metric,
            ["used"] = result.Used,
            ["reserved"] = result.Reserved,
            ["limit"] = result.Limit,
            ["resetAt"] = result.ResetAt,
        });
}
