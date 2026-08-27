using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public sealed class GeocodeCache
{
    public Guid Id { get; init; }
    public string QueryHash { get; set; } = string.Empty;
    public string OriginalQuery { get; set; } = string.Empty;
    public GeocodeResolutionStatus Status { get; set; } = GeocodeResolutionStatus.Manual;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderAttribution { get; set; } = string.Empty;
    public string? ProviderPlaceId { get; set; }
    public DateTimeOffset ResolvedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class GeocodeCacheConfiguration : IEntityTypeConfiguration<GeocodeCache>
{
    public void Configure(EntityTypeBuilder<GeocodeCache> e)
    {
        e.ToTable("geocode_cache"); e.HasKey(x => x.Id);
        e.Property(x => x.QueryHash).HasMaxLength(64);
        e.Property(x => x.OriginalQuery).HasMaxLength(500);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.ProviderName).HasMaxLength(80);
        e.Property(x => x.ProviderAttribution).HasMaxLength(200);
        e.Property(x => x.ProviderPlaceId).HasMaxLength(200);
        e.HasIndex(x => x.QueryHash).IsUnique();
        e.HasIndex(x => x.ExpiresAt);
    }
}
