using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum ReviewModerationStatus { Visible, Flagged, Hidden }

public sealed class GuideReview
{
    public Guid Id { get; init; }
    public Guid GuideId { get; init; }
    public Guid UserId { get; init; }
    public int Rating { get; set; }
    public string Body { get; set; } = string.Empty;
    public ReviewModerationStatus ModerationStatus { get; set; } = ReviewModerationStatus.Visible;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReviewReply
{
    public Guid Id { get; init; }
    public Guid ReviewId { get; init; }
    public Guid AuthorUserId { get; init; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ReviewReport
{
    public Guid Id { get; init; }
    public Guid ReviewId { get; init; }
    public Guid ReporterUserId { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class ReviewFeedback
{
    public Guid Id { get; init; }
    public Guid GuideId { get; init; }
    public Guid UserId { get; init; }
    public required string Body { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class GuideReviewConfiguration : IEntityTypeConfiguration<GuideReview>
{
    public void Configure(EntityTypeBuilder<GuideReview> e)
    {
        e.ToTable("guide_reviews"); e.HasKey(x => x.Id);
        e.Property(x => x.Rating).IsRequired();
        e.Property(x => x.Body).HasMaxLength(4000);
        e.Property(x => x.ModerationStatus).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.GuideId, x.UserId }).IsUnique();
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReviewReplyConfiguration : IEntityTypeConfiguration<ReviewReply>
{
    public void Configure(EntityTypeBuilder<ReviewReply> e)
    {
        e.ToTable("review_replies"); e.HasKey(x => x.Id);
        e.Property(x => x.Body).HasMaxLength(4000);
        e.HasIndex(x => x.ReviewId).IsUnique();
        e.HasOne<GuideReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReviewReportConfiguration : IEntityTypeConfiguration<ReviewReport>
{
    public void Configure(EntityTypeBuilder<ReviewReport> e)
    {
        e.ToTable("review_reports"); e.HasKey(x => x.Id);
        e.Property(x => x.Reason).HasMaxLength(500);
        e.HasIndex(x => new { x.ReviewId, x.ReporterUserId }).IsUnique();
        e.HasOne<GuideReview>().WithMany().HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReviewFeedbackConfiguration : IEntityTypeConfiguration<ReviewFeedback>
{
    public void Configure(EntityTypeBuilder<ReviewFeedback> e)
    {
        e.ToTable("review_feedback"); e.HasKey(x => x.Id);
        e.Property(x => x.Body).HasMaxLength(4000);
        e.HasIndex(x => new { x.GuideId, x.UserId }).IsUnique();
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
