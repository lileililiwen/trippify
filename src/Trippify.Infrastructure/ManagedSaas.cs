using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum TenantStatus { Active, Suspended, Pending }
public enum SubscriptionPlan { Free, Pro, Enterprise }
public enum SubscriptionStatus { Active, Cancelled, Expired }

public sealed class Tenant
{
    public Guid Id { get; init; }
    public required string Slug { get; init; }
    public string DisplayName { get; set; } = string.Empty;
    public string PrimaryDomain { get; set; } = string.Empty;
    public string BrandingJson { get; set; } = string.Empty;
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class TenantMember
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid UserId { get; init; }
    public string Role { get; set; } = "Member";
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class Subscription
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset? EndsAt { get; set; }
}

public sealed class QuotaUsage
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Metric { get; init; }
    public int Used { get; set; }
    public int Reserved { get; set; }
    public int Limit { get; set; }
    public bool IsCustomLimit { get; set; }
    public DateTimeOffset PeriodStart { get; init; }
    public DateTimeOffset PeriodEnd { get; init; }
}

public enum QuotaReservationStatus { Reserved, Finalized, Released }
public enum QuotaHistoryKind { Reserved, Finalized, Released, Adjusted }

public sealed class QuotaReservation
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public required string Metric { get; init; }
    public int Amount { get; init; }
    public QuotaReservationStatus Status { get; set; } = QuotaReservationStatus.Reserved;
    public DateTimeOffset PeriodStart { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class QuotaHistoryEntry
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid? ReservationId { get; init; }
    public Guid? ActorUserId { get; init; }
    public required string Metric { get; init; }
    public QuotaHistoryKind Kind { get; init; }
    public int Amount { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public sealed class TenantAuditEntry
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid ActorUserId { get; init; }
    public required string Action { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> e)
    {
        e.ToTable("tenants"); e.HasKey(x => x.Id);
        e.Property(x => x.Slug).HasMaxLength(80);
        e.Property(x => x.DisplayName).HasMaxLength(200);
        e.Property(x => x.PrimaryDomain).HasMaxLength(200);
        e.Property(x => x.BrandingJson).HasMaxLength(4000);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => x.Slug).IsUnique();
        e.HasIndex(x => x.PrimaryDomain).IsUnique();
    }
}

public sealed class TenantMemberConfiguration : IEntityTypeConfiguration<TenantMember>
{
    public void Configure(EntityTypeBuilder<TenantMember> e)
    {
        e.ToTable("tenant_members"); e.HasKey(x => x.Id);
        e.Property(x => x.Role).HasMaxLength(40);
        e.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique();
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> e)
    {
        e.ToTable("subscriptions"); e.HasKey(x => x.Id);
        e.Property(x => x.Plan).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => x.TenantId).IsUnique();
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuotaUsageConfiguration : IEntityTypeConfiguration<QuotaUsage>
{
    public void Configure(EntityTypeBuilder<QuotaUsage> e)
    {
        e.ToTable("quota_usages"); e.HasKey(x => x.Id);
        e.Property(x => x.Metric).HasMaxLength(40);
        e.HasIndex(x => new { x.TenantId, x.Metric, x.PeriodStart }).IsUnique();
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuotaReservationConfiguration : IEntityTypeConfiguration<QuotaReservation>
{
    public void Configure(EntityTypeBuilder<QuotaReservation> e)
    {
        e.ToTable("quota_reservations"); e.HasKey(x => x.Id);
        e.Property(x => x.Metric).HasMaxLength(40);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.TenantId, x.Metric, x.PeriodStart });
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class QuotaHistoryEntryConfiguration : IEntityTypeConfiguration<QuotaHistoryEntry>
{
    public void Configure(EntityTypeBuilder<QuotaHistoryEntry> e)
    {
        e.ToTable("quota_history_entries"); e.HasKey(x => x.Id);
        e.Property(x => x.Metric).HasMaxLength(40);
        e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Reason).HasMaxLength(500);
        e.HasIndex(x => new { x.TenantId, x.Metric, x.OccurredAt });
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TenantAuditEntryConfiguration : IEntityTypeConfiguration<TenantAuditEntry>
{
    public void Configure(EntityTypeBuilder<TenantAuditEntry> e)
    {
        e.ToTable("tenant_audit_entries"); e.HasKey(x => x.Id);
        e.Property(x => x.Action).HasMaxLength(80);
        e.Property(x => x.Reason).HasMaxLength(500);
        e.HasIndex(x => x.TenantId);
        e.HasIndex(x => x.OccurredAt);
        e.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
