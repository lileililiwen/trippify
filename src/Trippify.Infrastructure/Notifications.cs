using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum NotificationKind { NewGuidePublished, NewReviewOnMyGuide, NewReplyToReview, FollowerGained, EvidenceReviewed }

public sealed class CreatorFollow
{
    public Guid Id { get; init; }
    public Guid FollowerUserId { get; init; }
    public Guid CreatorUserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class NotificationPreference
{
    public Guid UserId { get; init; }
    public bool EmailEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;
    public bool NewGuidePublishedEmail { get; set; } = true;
    public bool NewGuidePublishedInApp { get; set; } = true;
    public bool NewReviewOnMyGuideEmail { get; set; } = true;
    public bool NewReviewOnMyGuideInApp { get; set; } = true;
    public bool NewReplyToReviewEmail { get; set; } = true;
    public bool NewReplyToReviewInApp { get; set; } = true;
    public bool FollowerGainedEmail { get; set; } = true;
    public bool FollowerGainedInApp { get; set; } = true;
    public bool EvidenceReviewedEmail { get; set; } = true;
    public bool EvidenceReviewedInApp { get; set; } = true;
}

public sealed class Notification
{
    public Guid Id { get; init; }
    public Guid? SourceJobId { get; init; }
    public Guid UserId { get; init; }
    public NotificationKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? TargetSlug { get; init; }
    public Guid? TargetGuideId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class CreatorFollowConfiguration : IEntityTypeConfiguration<CreatorFollow>
{
    public void Configure(EntityTypeBuilder<CreatorFollow> e)
    {
        e.ToTable("creator_follows"); e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.CreatorUserId, x.FollowerUserId }).IsUnique();
        e.HasIndex(x => x.FollowerUserId);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.FollowerUserId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> e)
    {
        e.ToTable("notification_preferences"); e.HasKey(x => x.UserId);
        e.HasOne<AppUser>().WithOne().HasForeignKey<NotificationPreference>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> e)
    {
        e.ToTable("notifications"); e.HasKey(x => x.Id);
        e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        e.Property(x => x.Title).HasMaxLength(200);
        e.Property(x => x.Body).HasMaxLength(800);
        e.Property(x => x.TargetSlug).HasMaxLength(200);
        e.HasIndex(x => new { x.UserId, x.CreatedAt });
        e.HasIndex(x => new { x.UserId, x.ReadAt });
        e.HasIndex(x => x.SourceJobId).IsUnique();
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
