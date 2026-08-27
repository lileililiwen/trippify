using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum EvidenceStatus { Pending, Approved, Rejected }
public enum EvidenceKind { TripJournal, Receipt, BookingConfirmation, PhotoNote, Other }

public sealed class TripEvidence
{
    public Guid Id { get; init; }
    public Guid GuideId { get; init; }
    public Guid UserId { get; init; }
    public EvidenceKind Kind { get; set; }
    public string Body { get; set; } = string.Empty;
    public string RedactedReference { get; set; } = string.Empty;
    public EvidenceStatus Status { get; set; } = EvidenceStatus.Pending;
    public DateTimeOffset SubmittedAt { get; init; }
    public DateTimeOffset RetentionDeadline { get; init; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class EvidenceReviewEntry
{
    public Guid Id { get; init; }
    public Guid EvidenceId { get; init; }
    public Guid ReviewerUserId { get; init; }
    public EvidenceStatus Decision { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ReviewedAt { get; init; }
}

public sealed class VerifiedGuideBadge
{
    public Guid Id { get; init; }
    public Guid GuideId { get; init; }
    public int ApprovedEvidenceCount { get; set; }
    public DateTimeOffset FirstGrantedAt { get; init; }
    public DateTimeOffset LastGrantedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class ActualTripMetric
{
    public Guid Id { get; init; }
    public Guid GuideId { get; init; }
    public Guid UserId { get; init; }
    public int PartySize { get; set; }
    public int TripDays { get; set; }
    public long TotalCostMinorUnits { get; set; }
    public required string CurrencyCode { get; set; }
    public DateTimeOffset SubmittedAt { get; init; }
}

public sealed class TripEvidenceConfiguration : IEntityTypeConfiguration<TripEvidence>
{
    public void Configure(EntityTypeBuilder<TripEvidence> e)
    {
        e.ToTable("trip_evidence"); e.HasKey(x => x.Id);
        e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Body).HasMaxLength(4000);
        e.Property(x => x.RedactedReference).HasMaxLength(200);
        e.HasIndex(x => new { x.GuideId, x.UserId }).IsUnique();
        e.HasIndex(x => x.RetentionDeadline);
        e.HasIndex(x => x.Status);
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EvidenceReviewEntryConfiguration : IEntityTypeConfiguration<EvidenceReviewEntry>
{
    public void Configure(EntityTypeBuilder<EvidenceReviewEntry> e)
    {
        e.ToTable("evidence_reviews"); e.HasKey(x => x.Id);
        e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Reason).HasMaxLength(500);
        e.HasIndex(x => x.EvidenceId);
        e.HasOne<TripEvidence>().WithMany().HasForeignKey(x => x.EvidenceId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class VerifiedGuideBadgeConfiguration : IEntityTypeConfiguration<VerifiedGuideBadge>
{
    public void Configure(EntityTypeBuilder<VerifiedGuideBadge> e)
    {
        e.ToTable("verified_guide_badges"); e.HasKey(x => x.Id);
        e.HasIndex(x => x.GuideId).IsUnique();
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ActualTripMetricConfiguration : IEntityTypeConfiguration<ActualTripMetric>
{
    public void Configure(EntityTypeBuilder<ActualTripMetric> e)
    {
        e.ToTable("actual_trip_metrics"); e.HasKey(x => x.Id);
        e.Property(x => x.CurrencyCode).HasMaxLength(3);
        e.HasIndex(x => new { x.GuideId, x.UserId }).IsUnique();
        e.HasIndex(x => new { x.GuideId, x.CurrencyCode });
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
