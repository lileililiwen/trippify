using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public sealed class FeatureFlag
{
    public Guid Id { get; init; }
    public required string Key { get; init; }
    public bool Enabled { get; set; }
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedByUserId { get; set; }
}

public sealed class BackupSnapshot
{
    public Guid Id { get; init; }
    public required string Label { get; init; }
    public string Payload { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; init; }
    public Guid CreatedByUserId { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public string Status { get; set; } = "legacy";
    public bool Restorable { get; set; }
    public bool Encrypted { get; set; }
    public string? ArtifactPath { get; set; }
    public string? Sha256 { get; set; }
    public DateTimeOffset? RetentionUntil { get; set; }
}

public sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> e)
    {
        e.ToTable("feature_flags"); e.HasKey(x => x.Id);
        e.Property(x => x.Key).HasMaxLength(80);
        e.Property(x => x.Value).HasMaxLength(2000);
        e.HasIndex(x => x.Key).IsUnique();
    }
}

public sealed class BackupSnapshotConfiguration : IEntityTypeConfiguration<BackupSnapshot>
{
    public void Configure(EntityTypeBuilder<BackupSnapshot> e)
    {
        e.ToTable("backup_snapshots"); e.HasKey(x => x.Id);
        e.Property(x => x.Label).HasMaxLength(160);
        e.Property(x => x.Payload).HasMaxLength(200000);
        e.Property(x => x.Status).HasMaxLength(32);
        e.Property(x => x.ArtifactPath).HasMaxLength(1000);
        e.Property(x => x.Sha256).HasMaxLength(64);
        e.HasIndex(x => x.CreatedAt);
    }
}
