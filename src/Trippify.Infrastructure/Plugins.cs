using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum PluginStatus { PendingReview, Approved, Rejected }
public enum PluginLifecycle { Installed, Enabled, Disabled }
public enum PluginPermissionScope { ReadGuides, ReadOrders, ReadNotifications, AuditAccess, ProviderAdapter }

public sealed class Plugin
{
    public Guid Id { get; init; }
    public required string Slug { get; init; }
    public required string DisplayName { get; init; }
    public required string Version { get; init; }
    public required string Publisher { get; init; }
    public required string Manifest { get; init; }
    public required string Signature { get; init; }
    public PluginStatus Status { get; set; } = PluginStatus.PendingReview;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class PluginInstallation
{
    public Guid Id { get; init; }
    public Guid PluginId { get; init; }
    public Guid UserId { get; init; }
    public PluginLifecycle Lifecycle { get; set; } = PluginLifecycle.Installed;
    public DateTimeOffset InstalledAt { get; init; }
    public DateTimeOffset? EnabledAt { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
    public DateTimeOffset? UninstalledAt { get; set; }
}

public sealed class PluginPermissionGrant
{
    public Guid Id { get; init; }
    public Guid InstallationId { get; init; }
    public PluginPermissionScope Scope { get; init; }
    public DateTimeOffset GrantedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class PluginAuditEntry
{
    public Guid Id { get; init; }
    public Guid PluginId { get; init; }
    public Guid ActorUserId { get; init; }
    public required string Action { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public sealed class PluginConfiguration : IEntityTypeConfiguration<Plugin>
{
    public void Configure(EntityTypeBuilder<Plugin> e)
    {
        e.ToTable("plugins"); e.HasKey(x => x.Id);
        e.Property(x => x.Slug).HasMaxLength(120);
        e.Property(x => x.DisplayName).HasMaxLength(200);
        e.Property(x => x.Version).HasMaxLength(40);
        e.Property(x => x.Publisher).HasMaxLength(200);
        e.Property(x => x.Manifest).HasMaxLength(8000);
        e.Property(x => x.Signature).HasMaxLength(200);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.Slug, x.Version }).IsUnique();
    }
}

public sealed class PluginInstallationConfiguration : IEntityTypeConfiguration<PluginInstallation>
{
    public void Configure(EntityTypeBuilder<PluginInstallation> e)
    {
        e.ToTable("plugin_installations"); e.HasKey(x => x.Id);
        e.Property(x => x.Lifecycle).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.PluginId, x.UserId }).IsUnique();
        e.HasOne<Plugin>().WithMany().HasForeignKey(x => x.PluginId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PluginPermissionGrantConfiguration : IEntityTypeConfiguration<PluginPermissionGrant>
{
    public void Configure(EntityTypeBuilder<PluginPermissionGrant> e)
    {
        e.ToTable("plugin_permission_grants"); e.HasKey(x => x.Id);
        e.Property(x => x.Scope).HasConversion<string>().HasMaxLength(40);
        e.HasIndex(x => x.InstallationId);
        e.HasOne<PluginInstallation>().WithMany().HasForeignKey(x => x.InstallationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PluginAuditEntryConfiguration : IEntityTypeConfiguration<PluginAuditEntry>
{
    public void Configure(EntityTypeBuilder<PluginAuditEntry> e)
    {
        e.ToTable("plugin_audit_entries"); e.HasKey(x => x.Id);
        e.Property(x => x.Action).HasMaxLength(80);
        e.Property(x => x.Reason).HasMaxLength(500);
        e.HasIndex(x => x.PluginId);
        e.HasIndex(x => x.OccurredAt);
        e.HasOne<Plugin>().WithMany().HasForeignKey(x => x.PluginId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
