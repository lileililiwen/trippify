using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class PluginsEndpoints
{
    private static readonly Meter PluginsMeter = new("Trippify.Plugins");
    private static readonly Counter<long> PluginsCommands = PluginsMeter.CreateCounter<long>("trippify.plugins.commands");

    public static void MapPlugins(this WebApplication app)
    {
        var adminGroup = app.MapGroup("/api/v1/admin/plugins").RequireAuthorization(p => p.RequireRole("Administrator"));
        adminGroup.MapPost("", RegisterPlugin);
        adminGroup.MapGet("/audit", ListPluginAudit);

        var publicGroup = app.MapGroup("/api/v1").AllowAnonymous();
        publicGroup.MapGet("/plugins", ListApprovedPlugins);
        publicGroup.MapGet("/plugins/{pluginId:guid}", GetApprovedPlugin);

        var ownerGroup = app.MapGroup("/api/v1/me/plugins").RequireAuthorization();
        ownerGroup.MapPost("/{pluginId:guid}/install", InstallPlugin);
        ownerGroup.MapPost("/{pluginId:guid}/enable", EnablePlugin);
        ownerGroup.MapPost("/{pluginId:guid}/disable", DisablePlugin);
        ownerGroup.MapDelete("/{pluginId:guid}", UninstallPlugin);
        ownerGroup.MapGet("/installations", ListMyInstallations);
    }

    private static string ResolveSigningSecret(IConfiguration configuration, IWebHostEnvironment environment) =>
        PluginSignature.ResolveSecret(configuration, environment.IsDevelopment());

    private static async Task<IResult> RegisterPlugin(RegisterPluginRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock, IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Slug) || string.IsNullOrWhiteSpace(request.DisplayName)
            || string.IsNullOrWhiteSpace(request.Version) || string.IsNullOrWhiteSpace(request.Publisher)
            || string.IsNullOrWhiteSpace(request.Manifest) || string.IsNullOrWhiteSpace(request.Signature))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["manifest"] = ["Manifest, signature, slug, displayName, version, and publisher are required."] });
        var signingSecret = ResolveSigningSecret(configuration, environment);
        if (!PluginSignature.Verify(request.Manifest, request.Signature, signingSecret))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["signature"] = ["Plugin signature failed verification."] });
        if (await db.Plugins.AsNoTracking().AnyAsync(x => x.Slug == request.Slug && x.Version == request.Version))
            return Results.Conflict(new Dictionary<string, string[]> { ["plugin"] = ["This plugin version is already registered."] });
        var actor = IdentityEndpoints.CurrentUserId(principal);
        var plugin = new Plugin
        {
            Id = Guid.NewGuid(),
            Slug = request.Slug.Trim().ToLowerInvariant(),
            DisplayName = request.DisplayName.Trim(),
            Version = request.Version.Trim(),
            Publisher = request.Publisher.Trim(),
            Manifest = request.Manifest.Trim(),
            Signature = request.Signature.Trim(),
            Status = PluginStatus.Approved,
            CreatedAt = clock.UtcNow,
        };
        db.Plugins.Add(plugin);
        db.PluginAuditEntries.Add(new PluginAuditEntry
        {
            Id = Guid.NewGuid(),
            PluginId = plugin.Id,
            ActorUserId = actor,
            Action = "registered",
            Reason = "manifest signed",
            OccurredAt = clock.UtcNow,
        });
        await db.SaveChangesAsync();
        PluginsCommands.Add(1, new KeyValuePair<string, object?>("operation", "plugin-registered"));
        return Results.Created($"/api/v1/plugins/{plugin.Id}", ToResponse(plugin));
    }

    private static async Task<IResult> ListApprovedPlugins(int? limit, AppDbContext db)
    {
        var pageSize = Math.Clamp(limit ?? 50, 1, 200);
        var plugins = await db.Plugins.AsNoTracking()
            .Where(x => x.Status == PluginStatus.Approved)
            .OrderBy(x => x.Slug)
            .Take(pageSize)
            .ToListAsync();
        PluginsCommands.Add(1, new KeyValuePair<string, object?>("operation", "plugins-listed"));
        return Results.Ok(new PluginListResponse(plugins.Count, plugins.Select(ToResponse).ToList()));
    }

    private static async Task<IResult> GetApprovedPlugin(Guid pluginId, AppDbContext db)
    {
        var plugin = await db.Plugins.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pluginId && x.Status == PluginStatus.Approved);
        if (plugin is null) return Results.NotFound();
        return Results.Ok(ToResponse(plugin));
    }

    private static async Task<IResult> InstallPlugin(Guid pluginId, InstallPluginRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var plugin = await db.Plugins.AsNoTracking().SingleOrDefaultAsync(x => x.Id == pluginId && x.Status == PluginStatus.Approved);
        if (plugin is null) return Results.NotFound();
        var existing = await db.PluginInstallations.AsNoTracking().SingleOrDefaultAsync(x => x.PluginId == pluginId && x.UserId == user);
        if (existing is not null)
            return Results.Ok(ToInstallationResponse(existing));
        if (request is null || request.Scopes is null || request.Scopes.Count == 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["scopes"] = ["At least one permission scope is required to install."] });
        var invalidScopes = request.Scopes.Where(s => !Enum.IsDefined(typeof(PluginPermissionScope), s)).ToList();
        if (invalidScopes.Count > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["scopes"] = ["Unknown plugin permission scopes supplied."] });
        var now = clock.UtcNow;
        var installation = new PluginInstallation
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            UserId = user,
            Lifecycle = PluginLifecycle.Installed,
            InstalledAt = now,
        };
        db.PluginInstallations.Add(installation);
        await db.SaveChangesAsync();
        foreach (var scope in request.Scopes)
        {
            db.PluginPermissionGrants.Add(new PluginPermissionGrant
            {
                Id = Guid.NewGuid(),
                InstallationId = installation.Id,
                Scope = scope,
                GrantedAt = now,
            });
        }
        db.PluginAuditEntries.Add(new PluginAuditEntry
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            ActorUserId = user,
            Action = "installed",
            Reason = string.Join(',', request.Scopes),
            OccurredAt = now,
        });
        await db.SaveChangesAsync();
        PluginsCommands.Add(1, new KeyValuePair<string, object?>("operation", "plugin-installed"));
        return Results.Created($"/api/v1/me/plugins/{pluginId}", ToInstallationResponse(installation, request.Scopes));
    }

    private static async Task<IResult> EnablePlugin(Guid pluginId, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var installation = await OwnedInstallation(pluginId, principal, db);
        if (installation is null) return Results.NotFound();
        if (installation.Lifecycle != PluginLifecycle.Enabled)
        {
            installation.Lifecycle = PluginLifecycle.Enabled;
            installation.EnabledAt = clock.UtcNow;
            db.PluginAuditEntries.Add(Audit(installation.PluginId, CurrentUser(principal), "enabled", installation.Id, "toggle"));
            await db.SaveChangesAsync();
            PluginsCommands.Add(1, new KeyValuePair<string, object?>("operation", "plugin-enabled"));
        }
        return Results.NoContent();
    }

    private static async Task<IResult> DisablePlugin(Guid pluginId, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var installation = await OwnedInstallation(pluginId, principal, db);
        if (installation is null) return Results.NotFound();
        if (installation.Lifecycle != PluginLifecycle.Disabled)
        {
            installation.Lifecycle = PluginLifecycle.Disabled;
            installation.DisabledAt = clock.UtcNow;
            db.PluginAuditEntries.Add(Audit(installation.PluginId, CurrentUser(principal), "disabled", installation.Id, "toggle"));
            await db.SaveChangesAsync();
            PluginsCommands.Add(1, new KeyValuePair<string, object?>("operation", "plugin-disabled"));
        }
        return Results.NoContent();
    }

    private static async Task<IResult> UninstallPlugin(Guid pluginId, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var installation = await OwnedInstallation(pluginId, principal, db);
        if (installation is null) return Results.NotFound();
        installation.Lifecycle = PluginLifecycle.Disabled;
        installation.UninstalledAt = clock.UtcNow;
        installation.DisabledAt = clock.UtcNow;
        var grants = await db.PluginPermissionGrants.Where(x => x.InstallationId == installation.Id).ToListAsync();
        foreach (var grant in grants) grant.RevokedAt = clock.UtcNow;
        db.PluginAuditEntries.Add(Audit(installation.PluginId, CurrentUser(principal), "uninstalled", installation.Id, "user-initiated"));
        await db.SaveChangesAsync();
        PluginsCommands.Add(1, new KeyValuePair<string, object?>("operation", "plugin-uninstalled"));
        return Results.NoContent();
    }

    private static async Task<IResult> ListMyInstallations(ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var installations = await db.PluginInstallations.AsNoTracking()
            .Where(x => x.UserId == user)
            .OrderByDescending(x => x.InstalledAt)
            .ToListAsync();
        var installationIds = installations.Select(x => x.Id).ToList();
        var pluginIds = installations.Select(x => x.PluginId).Distinct().ToList();
        var pluginMap = await db.Plugins.AsNoTracking()
            .Where(x => pluginIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);
        var grants = await db.PluginPermissionGrants.AsNoTracking()
            .Where(x => installationIds.Contains(x.InstallationId))
            .ToListAsync();
        var grantMap = grants.GroupBy(x => x.InstallationId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Scope).ToList());
        return Results.Ok(installations.Select(i => ToInstallationResponse(i, grantMap.GetValueOrDefault(i.Id), pluginMap.GetValueOrDefault(i.PluginId))).ToList());
    }

    private static async Task<IResult> ListPluginAudit(int? limit, AppDbContext db)
    {
        var pageSize = Math.Clamp(limit ?? 50, 1, 200);
        var entries = await db.PluginAuditEntries.AsNoTracking()
            .OrderByDescending(x => x.OccurredAt)
            .Take(pageSize)
            .ToListAsync();
        PluginsCommands.Add(1, new KeyValuePair<string, object?>("operation", "plugin-audit-listed"));
        return Results.Ok(new PluginAuditListResponse(entries.Count, entries.Select(ToAuditResponse).ToList()));
    }

    private static async Task<PluginInstallation?> OwnedInstallation(Guid pluginId, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        return await db.PluginInstallations.SingleOrDefaultAsync(x => x.PluginId == pluginId && x.UserId == user);
    }

    private static Guid CurrentUser(ClaimsPrincipal principal) => IdentityEndpoints.CurrentUserId(principal);

    private static PluginAuditEntry Audit(Guid pluginId, Guid actor, string action, Guid installationId, string reason) => new()
    {
        Id = Guid.NewGuid(),
        PluginId = pluginId,
        ActorUserId = actor,
        Action = action + ":" + installationId,
        Reason = reason,
        OccurredAt = DateTimeOffset.UtcNow,
    };

    private static PluginResponse ToResponse(Plugin p) => new(p.Id, p.Slug, p.DisplayName, p.Version, p.Publisher, p.Status.ToString(), p.CreatedAt);

    private static PluginInstallationResponse ToInstallationResponse(PluginInstallation i, IEnumerable<PluginPermissionScope>? scopes = null, Plugin? plugin = null) =>
        new(i.Id, i.PluginId, plugin?.Slug ?? string.Empty, plugin?.DisplayName ?? string.Empty, i.Lifecycle.ToString(), i.InstalledAt, i.EnabledAt, i.DisabledAt, i.UninstalledAt, (scopes ?? Array.Empty<PluginPermissionScope>()).Select(s => s.ToString()).ToList());

    private static PluginAuditEntryResponse ToAuditResponse(PluginAuditEntry e) => new(e.Id, e.PluginId, e.ActorUserId, e.Action, e.Reason, e.OccurredAt);
}

public sealed record RegisterPluginRequest(string Slug, string DisplayName, string Version, string Publisher, string Manifest, string Signature);
public sealed record InstallPluginRequest(IReadOnlyList<PluginPermissionScope> Scopes);
public sealed record PluginResponse(Guid Id, string Slug, string DisplayName, string Version, string Publisher, string Status, DateTimeOffset CreatedAt);
public sealed record PluginListResponse(int Total, IReadOnlyList<PluginResponse> Items);
public sealed record PluginInstallationResponse(Guid Id, Guid PluginId, string PluginSlug, string PluginDisplayName, string Lifecycle, DateTimeOffset InstalledAt, DateTimeOffset? EnabledAt, DateTimeOffset? DisabledAt, DateTimeOffset? UninstalledAt, IReadOnlyList<string> Scopes);
public sealed record PluginAuditEntryResponse(Guid Id, Guid PluginId, Guid ActorUserId, string Action, string Reason, DateTimeOffset OccurredAt);
public sealed record PluginAuditListResponse(int Total, IReadOnlyList<PluginAuditEntryResponse> Items);
