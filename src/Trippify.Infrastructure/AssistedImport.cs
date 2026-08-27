using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum ImportKind { Text, Photo, Video }
public enum ImportStatus { Queued, Processing, Completed, Failed }
public enum ImportDraftStatus { PendingReview, Approved, Rejected }
public enum TranslationStatus { Linked, Outdated, Approved }

public sealed class ImportJob
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public ImportKind Kind { get; set; }
    public string SourceText { get; set; } = string.Empty;
    public string? ObjectKey { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Queued;
    public string FailureReason { get; set; } = string.Empty;
    public string FailureCode { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTimeOffset SubmittedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class ImportDraft
{
    public Guid Id { get; init; }
    public Guid ImportJobId { get; init; }
    public Guid UserId { get; init; }
    public string SuggestedTitle { get; set; } = string.Empty;
    public string SuggestedNodesJson { get; set; } = "[]";
    public string ProvenanceJson { get; set; } = "{}";
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string OutputSchemaVersion { get; set; } = string.Empty;
    public ImportDraftStatus Status { get; set; } = ImportDraftStatus.PendingReview;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class Translation
{
    public Guid Id { get; init; }
    public Guid SourceDraftId { get; init; }
    public Guid UserId { get; init; }
    public required string Locale { get; init; }
    public string Body { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public TranslationStatus Status { get; set; } = TranslationStatus.Linked;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class AiQuotaUsage
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string Metric { get; init; }
    public int Used { get; set; }
    public int Limit { get; set; }
    public DateTimeOffset PeriodStart { get; init; }
    public DateTimeOffset PeriodEnd { get; init; }
}

public sealed class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> e)
    {
        e.ToTable("import_jobs"); e.HasKey(x => x.Id);
        e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.SourceText).HasMaxLength(20000);
        e.Property(x => x.ObjectKey).HasMaxLength(400);
        e.Property(x => x.FailureReason).HasMaxLength(500);
        e.Property(x => x.FailureCode).HasMaxLength(80);
        e.Property(x => x.ProviderName).HasMaxLength(80);
        e.Property(x => x.ModelName).HasMaxLength(80);
        e.Property(x => x.SchemaVersion).HasMaxLength(40);
        e.HasIndex(x => new { x.UserId, x.SubmittedAt });
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ImportDraftConfiguration : IEntityTypeConfiguration<ImportDraft>
{
    public void Configure(EntityTypeBuilder<ImportDraft> e)
    {
        e.ToTable("import_drafts"); e.HasKey(x => x.Id);
        e.Property(x => x.SuggestedTitle).HasMaxLength(200);
        e.Property(x => x.SuggestedNodesJson).HasMaxLength(16000);
        e.Property(x => x.ProvenanceJson).HasMaxLength(4000);
        e.Property(x => x.ProviderName).HasMaxLength(80);
        e.Property(x => x.ModelName).HasMaxLength(80);
        e.Property(x => x.SchemaVersion).HasMaxLength(40);
        e.Property(x => x.OutputSchemaVersion).HasMaxLength(40);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => x.ImportJobId).IsUnique();
        e.HasOne<ImportJob>().WithMany().HasForeignKey(x => x.ImportJobId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TranslationConfiguration : IEntityTypeConfiguration<Translation>
{
    public void Configure(EntityTypeBuilder<Translation> e)
    {
        e.ToTable("translations"); e.HasKey(x => x.Id);
        e.Property(x => x.Locale).HasMaxLength(8);
        e.Property(x => x.Body).HasMaxLength(16000);
        e.Property(x => x.ProviderName).HasMaxLength(80);
        e.Property(x => x.ModelName).HasMaxLength(80);
        e.Property(x => x.SchemaVersion).HasMaxLength(40);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.SourceDraftId, x.Locale }).IsUnique();
        e.HasOne<ImportDraft>().WithMany().HasForeignKey(x => x.SourceDraftId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AiQuotaUsageConfiguration : IEntityTypeConfiguration<AiQuotaUsage>
{
    public void Configure(EntityTypeBuilder<AiQuotaUsage> e)
    {
        e.ToTable("ai_quota_usages"); e.HasKey(x => x.Id);
        e.Property(x => x.Metric).HasMaxLength(40);
        e.HasIndex(x => new { x.UserId, x.Metric, x.PeriodStart }).IsUnique();
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
