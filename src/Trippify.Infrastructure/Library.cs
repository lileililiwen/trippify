using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum TripStatus { Planning, Active, Completed, Archived }

public sealed class GuideFavorite
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid GuideId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class UserTrip
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string Title { get; set; }
    public Guid? SourceGuideId { get; set; }
    public Guid? ForkedGuideId { get; set; }
    public TripStatus Status { get; set; } = TripStatus.Planning;
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class GuideFavoriteConfiguration : IEntityTypeConfiguration<GuideFavorite>
{
    public void Configure(EntityTypeBuilder<GuideFavorite> e)
    {
        e.ToTable("guide_favorites"); e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.UserId, x.GuideId }).IsUnique();
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserTripConfiguration : IEntityTypeConfiguration<UserTrip>
{
    public void Configure(EntityTypeBuilder<UserTrip> e)
    {
        e.ToTable("user_trips"); e.HasKey(x => x.Id);
        e.Property(x => x.Title).HasMaxLength(160);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Notes).HasMaxLength(4000);
        e.HasIndex(x => new { x.UserId, x.UpdatedAt });
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.SourceGuideId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.ForkedGuideId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
