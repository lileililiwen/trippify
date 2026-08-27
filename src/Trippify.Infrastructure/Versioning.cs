using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public sealed class GuideRelease
{
    public Guid Id { get; init; }
    public Guid GuideId { get; init; }
    public int VersionNumber { get; init; }
    public required string Changelog { get; init; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; init; }
    public Guid PublisherUserId { get; init; }
    public string NodeSummary { get; set; } = string.Empty;
}

public sealed class GuideReleaseConfiguration : IEntityTypeConfiguration<GuideRelease>
{
    public void Configure(EntityTypeBuilder<GuideRelease> e)
    {
        e.ToTable("guide_releases"); e.HasKey(x => x.Id);
        e.Property(x => x.Changelog).HasMaxLength(4000);
        e.Property(x => x.Title).HasMaxLength(200);
        e.Property(x => x.NodeSummary).HasMaxLength(2000);
        e.HasIndex(x => new { x.GuideId, x.VersionNumber }).IsUnique();
        e.HasIndex(x => x.GuideId);
        e.HasIndex(x => x.PublishedAt);
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.PublisherUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
