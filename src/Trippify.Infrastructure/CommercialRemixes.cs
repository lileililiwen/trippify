using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum RemixDecision { Pending, Approved, Rejected }

public sealed class LicensePolicy
{
    public Guid Id { get; init; }
    public Guid OwnerUserId { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool AllowCommercial { get; set; }
    public bool RequireApproval { get; set; } = true;
    public int RoyaltyPercent { get; set; } = 25;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RemixAncestry
{
    public Guid Id { get; init; }
    public Guid ChildGuideId { get; init; }
    public Guid ParentGuideId { get; init; }
    public Guid LicensePolicyId { get; init; }
    public string AttributionJson { get; set; } = "{}";
    public RemixDecision Decision { get; set; } = RemixDecision.Pending;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? DecidedAt { get; set; }
}

public sealed class RemixApproval
{
    public Guid Id { get; init; }
    public Guid RemixAncestryId { get; init; }
    public Guid ApproverUserId { get; init; }
    public RemixDecision Decision { get; set; } = RemixDecision.Approved;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset DecidedAt { get; set; }
}

public sealed class RevenueShare
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public int Percent { get; init; }
    public long AmountMinorUnits { get; init; }
    public required string CurrencyCode { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class LicensePolicyConfiguration : IEntityTypeConfiguration<LicensePolicy>
{
    public void Configure(EntityTypeBuilder<LicensePolicy> e)
    {
        e.ToTable("license_policies"); e.HasKey(x => x.Id);
        e.Property(x => x.Slug).HasMaxLength(80);
        e.Property(x => x.DisplayName).HasMaxLength(200);
        e.HasIndex(x => new { x.OwnerUserId, x.Slug }).IsUnique();
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RemixAncestryConfiguration : IEntityTypeConfiguration<RemixAncestry>
{
    public void Configure(EntityTypeBuilder<RemixAncestry> e)
    {
        e.ToTable("remix_ancestries"); e.HasKey(x => x.Id);
        e.HasIndex(x => x.ChildGuideId).IsUnique();
        e.Property(x => x.AttributionJson).HasMaxLength(2000);
        e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.ChildGuideId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.ParentGuideId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<LicensePolicy>().WithMany().HasForeignKey(x => x.LicensePolicyId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RemixApprovalConfiguration : IEntityTypeConfiguration<RemixApproval>
{
    public void Configure(EntityTypeBuilder<RemixApproval> e)
    {
        e.ToTable("remix_approvals"); e.HasKey(x => x.Id);
        e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Reason).HasMaxLength(500);
        e.HasIndex(x => x.RemixAncestryId);
        e.HasOne<RemixAncestry>().WithMany().HasForeignKey(x => x.RemixAncestryId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ApproverUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RevenueShareConfiguration : IEntityTypeConfiguration<RevenueShare>
{
    public void Configure(EntityTypeBuilder<RevenueShare> e)
    {
        e.ToTable("revenue_shares"); e.HasKey(x => x.Id);
        e.Property(x => x.CurrencyCode).HasMaxLength(3);
        e.HasIndex(x => x.OrderId);
        e.HasOne<GuideOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
