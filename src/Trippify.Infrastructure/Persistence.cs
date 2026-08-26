using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;

namespace Trippify.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<CreatorProfile> CreatorProfiles => Set<CreatorProfile>();
    public DbSet<RevokedAccessToken> RevokedAccessTokens => Set<RevokedAccessToken>();
    public DbSet<IdentityAuditEntry> IdentityAuditEntries => Set<IdentityAuditEntry>();
    public DbSet<TravelGuide> TravelGuides => Set<TravelGuide>();
    public DbSet<GuideDay> GuideDays => Set<GuideDay>();
    public DbSet<GuideNode> GuideNodes => Set<GuideNode>();
    public DbSet<GuideSection> GuideSections => Set<GuideSection>();
    public DbSet<GuideMedia> GuideMedia => Set<GuideMedia>();
    public DbSet<GuideAuditEntry> GuideAuditEntries => Set<GuideAuditEntry>();
    public DbSet<GuideCommandReceipt> GuideCommandReceipts => Set<GuideCommandReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.Entity<OutboxMessage>(entity => { entity.ToTable("outbox_messages"); entity.HasKey(x => x.Id); entity.Property(x => x.Type).HasMaxLength(200); entity.HasIndex(x => x.OccurredAt); });
        modelBuilder.Entity<AppUser>(entity => { entity.ToTable("users"); entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20); entity.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("EmailIndex"); });
        modelBuilder.Entity<IdentityRole<Guid>>().ToTable("roles");
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        modelBuilder.Entity<UserProfile>(entity => { entity.ToTable("user_profiles"); entity.HasKey(x => x.UserId); entity.Property(x => x.DisplayName).HasMaxLength(80); entity.HasOne<AppUser>().WithOne().HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<CreatorProfile>(entity => { entity.ToTable("creator_profiles"); entity.HasKey(x => x.UserId); entity.HasIndex(x => x.Slug).IsUnique(); entity.Property(x => x.Slug).HasMaxLength(80); entity.Property(x => x.Biography).HasMaxLength(2000); entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20); entity.HasOne<AppUser>().WithOne().HasForeignKey<CreatorProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<RevokedAccessToken>(entity => { entity.ToTable("revoked_access_tokens"); entity.HasKey(x => x.TokenHash); entity.Property(x => x.TokenHash).HasMaxLength(64); entity.HasIndex(x => x.ExpiresAt); });
        modelBuilder.Entity<IdentityAuditEntry>(entity => { entity.ToTable("identity_audit_entries"); entity.HasKey(x => x.Id); entity.Property(x => x.Action).HasMaxLength(100); entity.Property(x => x.Reason).HasMaxLength(500); entity.HasIndex(x => x.OccurredAt); });
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

public sealed class OutboxMessage { public Guid Id { get; init; } public DateTimeOffset OccurredAt { get; init; } public required string Type { get; init; } public required string Payload { get; init; } public DateTimeOffset? ProcessedAt { get; set; } }
public enum AccountStatus { Active, Suspended }
public enum CreatorStatus { Pending, Active, Suspended }
public sealed class AppUser : IdentityUser<Guid> { public AccountStatus Status { get; set; } = AccountStatus.Active; }
public sealed class UserProfile { public Guid UserId { get; init; } public required string DisplayName { get; set; } public string? AvatarUrl { get; set; } public string? Locale { get; set; } }
public sealed class CreatorProfile { public Guid UserId { get; init; } public required string Slug { get; set; } public string Biography { get; set; } = string.Empty; public string[] TravelCountries { get; set; } = []; public CreatorStatus Status { get; set; } = CreatorStatus.Pending; }
public sealed class RevokedAccessToken { public required string TokenHash { get; init; } public DateTimeOffset ExpiresAt { get; init; } }
public sealed class IdentityAuditEntry { public Guid Id { get; init; } public Guid ActorUserId { get; init; } public Guid TargetUserId { get; init; } public required string Action { get; init; } public required string Reason { get; init; } public DateTimeOffset OccurredAt { get; init; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
public sealed class LocalProviders : IObjectStorage, IEmailSender, IMapProvider, IPaymentGateway, IAiAssistant, IBackgroundJobQueue
{
    public Task<Uri> PutAsync(string key, Stream content, CancellationToken cancellationToken) => Task.FromResult(new Uri("file:///tmp/trippify/" + Uri.EscapeDataString(key)));
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<string?> GeocodeAsync(string address, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    public Task<string> CreateCheckoutAsync(long minorUnits, string currency, CancellationToken cancellationToken) => throw new NotSupportedException("Payments are disabled in local mode.");
    public Task<string> AssistAsync(string input, CancellationToken cancellationToken) => Task.FromResult(input);
    public ValueTask EnqueueAsync(string jobName, string payload, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
