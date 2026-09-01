using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trippify.Application;

namespace Trippify.Infrastructure;

public enum BackgroundJobStatus { Pending, Running, Completed, DeadLetter }

public sealed class BackgroundJob
{
    public Guid Id { get; init; }
    public required string Type { get; init; }
    public int PayloadVersion { get; init; } = 1;
    public required string Payload { get; init; }
    public required string IdempotencyKey { get; init; }
    public BackgroundJobStatus Status { get; set; } = BackgroundJobStatus.Pending;
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTimeOffset AvailableAt { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string FailureCode { get; set; } = string.Empty;
}

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> e)
    {
        e.ToTable("background_jobs"); e.HasKey(x => x.Id);
        e.Property(x => x.Type).HasMaxLength(100);
        e.Property(x => x.Payload).HasMaxLength(8000);
        e.Property(x => x.IdempotencyKey).HasMaxLength(200);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.LeaseOwner).HasMaxLength(100);
        e.Property(x => x.FailureCode).HasMaxLength(100);
        e.HasIndex(x => x.IdempotencyKey).IsUnique();
        e.HasIndex(x => new { x.Status, x.AvailableAt, x.LeaseExpiresAt });
    }
}

public sealed class DurableBackgroundJobQueue(AppDbContext db, IClock clock) : IBackgroundJobQueue
{
    public async ValueTask EnqueueAsync(string jobName, string payload, CancellationToken cancellationToken, string? idempotencyKey = null, DateTimeOffset? availableAt = null)
    {
        if (string.IsNullOrWhiteSpace(jobName) || jobName.Length > 100) throw new ArgumentException("Job name is required and limited to 100 characters.", nameof(jobName));
        if (payload.Length > 8000) throw new ArgumentException("Job payload exceeds 8000 characters.", nameof(payload));
        idempotencyKey ??= Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{jobName}\n{payload}")));
        if (await db.BackgroundJobs.AsNoTracking().AnyAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken)) return;
        db.BackgroundJobs.Add(new BackgroundJob
        {
            Id = Guid.NewGuid(), Type = jobName, Payload = payload, IdempotencyKey = idempotencyKey,
            AvailableAt = availableAt ?? clock.UtcNow, CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
