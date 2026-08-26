using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum TransportMode { Walk, Bicycle, Car, Taxi, Bus, Train, Ferry, Flight, Other }
public enum BudgetCategory { Transport, Lodging, Food, Activities, Shopping, Other }

public sealed class GuideTransportSegment
{
    public Guid Id { get; init; }
    public Guid DayId { get; init; }
    public int Position { get; set; }
    public TransportMode Mode { get; set; }
    public string Label { get; set; } = string.Empty;
    public string OriginName { get; set; } = string.Empty;
    public string DestinationName { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public long CostPerPersonMinorUnits { get; set; }
    public required string CurrencyCode { get; set; }
}

public sealed class GuideBudgetEntry
{
    public Guid Id { get; init; }
    public Guid GuideId { get; init; }
    public BudgetCategory Category { get; set; }
    public long AmountPerPersonMinorUnits { get; set; }
    public required string CurrencyCode { get; set; }
}

public sealed class GuideTransportSegmentConfiguration : IEntityTypeConfiguration<GuideTransportSegment>
{
    public void Configure(EntityTypeBuilder<GuideTransportSegment> e)
    {
        e.ToTable("guide_transport_segments"); e.HasKey(x => x.Id);
        e.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Label).HasMaxLength(160);
        e.Property(x => x.OriginName).HasMaxLength(200);
        e.Property(x => x.DestinationName).HasMaxLength(200);
        e.Property(x => x.CurrencyCode).HasMaxLength(3);
        e.HasIndex(x => new { x.DayId, x.Position }).IsUnique();
        e.HasOne<GuideDay>().WithMany().HasForeignKey(x => x.DayId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GuideBudgetEntryConfiguration : IEntityTypeConfiguration<GuideBudgetEntry>
{
    public void Configure(EntityTypeBuilder<GuideBudgetEntry> e)
    {
        e.ToTable("guide_budget_entries"); e.HasKey(x => x.Id);
        e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.CurrencyCode).HasMaxLength(3);
        e.HasIndex(x => new { x.GuideId, x.Category }).IsUnique();
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
    }
}
