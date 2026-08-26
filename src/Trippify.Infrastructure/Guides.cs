using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum GuideLifecycle { Draft, Private, FreePublic, Paid, Unlisted, Archived }
public enum GuideNodeType { Attraction, Restaurant, Hotel, Cafe, Shop, Airport, TrainStation, BusStop, CustomPlace, Activity, Rest }
public enum GuideSectionType { Preparation, Visa, Connectivity, TransportCard, Currency, Packing, Safety, Culture, Pitfalls, RecommendedApps, Summary, Custom }

public sealed class TravelGuide
{
    public Guid Id { get; init; }
    public Guid OwnerUserId { get; init; }
    public required string Title { get; set; }
    public string Subtitle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public required string CountryCode { get; set; }
    public string[] Cities { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public int TripDays { get; set; }
    public GuideLifecycle Lifecycle { get; set; } = GuideLifecycle.Draft;
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public List<GuideDay> Days { get; } = [];
    public List<GuideSection> Sections { get; } = [];
    public List<GuideMedia> Media { get; } = [];
}
public sealed class GuideDay { public Guid Id { get; init; } public Guid GuideId { get; init; } public int Position { get; set; } public required string Title { get; set; } public string Notes { get; set; } = string.Empty; public List<GuideNode> Nodes { get; } = []; }
public sealed class GuideNode { public Guid Id { get; init; } public Guid DayId { get; init; } public int Position { get; set; } public GuideNodeType Type { get; set; } public required string Name { get; set; } public string? Address { get; set; } public double? Latitude { get; set; } public double? Longitude { get; set; } public TimeOnly? ArrivalTime { get; set; } public TimeOnly? DepartureTime { get; set; } public int? StayMinutes { get; set; } public string? TicketInformation { get; set; } public string? ReservationInformation { get; set; } public string? OpeningHours { get; set; } public string Notes { get; set; } = string.Empty; }
public sealed class GuideSection { public Guid Id { get; init; } public Guid GuideId { get; init; } public int Position { get; set; } public GuideSectionType Type { get; set; } public required string Title { get; set; } public required string Body { get; set; } }
public sealed class GuideMedia { public Guid Id { get; init; } public Guid GuideId { get; init; } public Guid? DayId { get; init; } public Guid? NodeId { get; init; } public int Position { get; set; } public required string StorageKey { get; init; } public required string Url { get; init; } public string? Caption { get; set; } }
public sealed class GuideAuditEntry { public Guid Id { get; init; } public Guid GuideId { get; init; } public Guid ActorUserId { get; init; } public required string Action { get; init; } public DateTimeOffset OccurredAt { get; init; } }
public sealed class GuideCommandReceipt { public Guid OwnerUserId { get; init; } public required string IdempotencyKey { get; init; } public required string Operation { get; init; } public Guid ResourceId { get; init; } public DateTimeOffset CreatedAt { get; init; } }

public sealed class GuideConfiguration : IEntityTypeConfiguration<TravelGuide>
{
    public void Configure(EntityTypeBuilder<TravelGuide> entity)
    {
        entity.ToTable("travel_guides"); entity.HasKey(x => x.Id); entity.HasQueryFilter(x => x.DeletedAt == null); entity.Property(x => x.Title).HasMaxLength(160); entity.Property(x => x.Subtitle).HasMaxLength(240); entity.Property(x => x.Summary).HasMaxLength(4000); entity.Property(x => x.CountryCode).HasMaxLength(2); entity.Property(x => x.Lifecycle).HasConversion<string>().HasMaxLength(20); entity.Property(x => x.ConcurrencyToken).IsConcurrencyToken(); entity.HasIndex(x => new { x.OwnerUserId, x.UpdatedAt }); entity.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class GuideDayConfiguration : IEntityTypeConfiguration<GuideDay> { public void Configure(EntityTypeBuilder<GuideDay> e) { e.ToTable("guide_days"); e.HasKey(x => x.Id); e.Property(x => x.Title).HasMaxLength(160); e.HasIndex(x => new { x.GuideId, x.Position }).IsUnique(); e.HasOne<TravelGuide>().WithMany(x => x.Days).HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade); } }
public sealed class GuideNodeConfiguration : IEntityTypeConfiguration<GuideNode> { public void Configure(EntityTypeBuilder<GuideNode> e) { e.ToTable("guide_nodes"); e.HasKey(x => x.Id); e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30); e.Property(x => x.Name).HasMaxLength(200); e.HasIndex(x => new { x.DayId, x.Position }).IsUnique(); e.HasOne<GuideDay>().WithMany(x => x.Nodes).HasForeignKey(x => x.DayId).OnDelete(DeleteBehavior.Cascade); } }
public sealed class GuideSectionConfiguration : IEntityTypeConfiguration<GuideSection> { public void Configure(EntityTypeBuilder<GuideSection> e) { e.ToTable("guide_sections"); e.HasKey(x => x.Id); e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30); e.Property(x => x.Title).HasMaxLength(160); e.HasIndex(x => new { x.GuideId, x.Position }).IsUnique(); e.HasOne<TravelGuide>().WithMany(x => x.Sections).HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade); } }
public sealed class GuideMediaConfiguration : IEntityTypeConfiguration<GuideMedia> { public void Configure(EntityTypeBuilder<GuideMedia> e) { e.ToTable("guide_media"); e.HasKey(x => x.Id); e.Property(x => x.StorageKey).HasMaxLength(500); e.Property(x => x.Url).HasMaxLength(2000); e.HasIndex(x => new { x.GuideId, x.Position }); e.HasOne<TravelGuide>().WithMany(x => x.Media).HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade); } }
public sealed class GuideAuditConfiguration : IEntityTypeConfiguration<GuideAuditEntry> { public void Configure(EntityTypeBuilder<GuideAuditEntry> e) { e.ToTable("guide_audit_entries"); e.HasKey(x => x.Id); e.Property(x => x.Action).HasMaxLength(100); e.HasIndex(x => new { x.GuideId, x.OccurredAt }); } }
public sealed class GuideReceiptConfiguration : IEntityTypeConfiguration<GuideCommandReceipt> { public void Configure(EntityTypeBuilder<GuideCommandReceipt> e) { e.ToTable("guide_command_receipts"); e.HasKey(x => new { x.OwnerUserId, x.IdempotencyKey, x.Operation }); e.Property(x => x.IdempotencyKey).HasMaxLength(100); e.Property(x => x.Operation).HasMaxLength(50); } }
