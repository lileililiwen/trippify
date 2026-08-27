using System.Diagnostics.Metrics;
using System.Reflection;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class SelfHostedEndpoints
{
    private const string Version = "1.0.0";
    private static readonly Meter SelfHostedMeter = new("Trippify.SelfHosted");
    private static readonly Counter<long> SelfHostedCommands = SelfHostedMeter.CreateCounter<long>("trippify.selfhosted.commands");

    public static void MapSelfHosted(this WebApplication app)
    {
        var publicGroup = app.MapGroup("/api/v1").AllowAnonymous();
        publicGroup.MapGet("/system/info", GetSystemInfo);

        var adminGroup = app.MapGroup("/api/v1/admin").RequireAuthorization(p => p.RequireRole("Administrator"));
        adminGroup.MapGet("/system/status", GetSystemStatus);
        adminGroup.MapPost("/system/upgrade", TriggerUpgrade);
        adminGroup.MapPost("/system/backup", TriggerBackup);
        adminGroup.MapPost("/system/restore", TriggerRestore);
        adminGroup.MapGet("/feature-flags", ListFeatureFlags);
        adminGroup.MapPut("/feature-flags/{key}", UpsertFeatureFlag);
    }

    private static IResult GetSystemInfo()
    {
        return Results.Ok(new SystemInfoResponse(Version, 0));
    }

    private static async Task<IResult> GetSystemStatus(AppDbContext db)
    {
        List<string> history;
        try
        {
            var migrationsAssembly = ((IInfrastructure<IServiceProvider>)db).Instance.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly>();
            history = migrationsAssembly!.Migrations.Select(m => m.Key).ToList();
        }
        catch (NotSupportedException)
        {
            history = new List<string>();
        }
        List<string> applied = new();
        try { applied = (await db.Database.GetAppliedMigrationsAsync()).ToList(); } catch (Exception) { applied = new(); }
        var pending = history.Except(applied).ToList();
        return Results.Ok(new SystemStatusResponse(Version, applied.Count(), pending.Count(), applied, pending));
    }

    private static async Task<IResult> TriggerUpgrade(AppDbContext db, ClaimsPrincipal principal)
    {
        try
        {
            await db.Database.MigrateAsync();
            SelfHostedCommands.Add(1, new KeyValuePair<string, object?>("operation", "upgrade-applied"));
            return Results.Ok(new { status = "applied", appliedAt = DateTimeOffset.UtcNow });
        }
        catch (Exception ex)
        {
            SelfHostedCommands.Add(1, new KeyValuePair<string, object?>("operation", "upgrade-skipped"));
            return Results.Ok(new { status = "skipped", reason = ex.GetType().Name });
        }
    }

    private static async Task<IResult> TriggerBackup(SystemInfoRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock, RestorableBackupService artifacts, CancellationToken cancellationToken)
    {
        var actor = IdentityEndpoints.CurrentUserId(principal);
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            users = await db.Users.AsNoTracking().Select(u => new { id = u.Id, email = u.Email }).ToListAsync(),
            guides = await db.TravelGuides.AsNoTracking().CountAsync(),
            orders = await db.GuideOrders.AsNoTracking().CountAsync(),
            featureFlags = await db.FeatureFlags.AsNoTracking().ToListAsync(),
            capturedAt = clock.UtcNow,
        });
        var snapshot = new BackupSnapshot
        {
            Id = Guid.NewGuid(),
            Label = string.IsNullOrWhiteSpace(request?.Label) ? $"manual-{clock.UtcNow:yyyyMMddHHmmss}" : request.Label.Trim(),
            Payload = payload,
            CreatedAt = clock.UtcNow,
            CreatedByUserId = actor,
            SchemaVersion = 2,
            Status = "writing",
            Restorable = false,
        };
        db.BackupSnapshots.Add(snapshot);
        await db.SaveChangesAsync(cancellationToken);
        var artifact = await artifacts.WriteAsync(snapshot.Id, payload, request?.OperatorKey, cancellationToken);
        snapshot.ArtifactPath = artifact.path;
        snapshot.Sha256 = artifact.checksum;
        snapshot.Encrypted = artifact.encrypted;
        snapshot.Restorable = true;
        snapshot.Status = "ready";
        snapshot.RetentionUntil = clock.UtcNow.AddDays(30);
        await db.SaveChangesAsync();
        SelfHostedCommands.Add(1, new KeyValuePair<string, object?>("operation", "backup-recorded"));
        return Results.Created($"/api/v1/admin/system/status", BackupResponse(snapshot));
    }

    private static async Task<IResult> TriggerRestore(RestoreRequest request, AppDbContext db, ClaimsPrincipal principal, RestorableBackupService artifacts, CancellationToken cancellationToken)
    {
        if (request is null || (request.SnapshotId is null && string.IsNullOrWhiteSpace(request.Payload)))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["payload"] = ["Payload is required."] });
        var actor = IdentityEndpoints.CurrentUserId(principal);
        if (request.SnapshotId is not null)
        {
            var source = await db.BackupSnapshots.SingleOrDefaultAsync(x => x.Id == request.SnapshotId, cancellationToken);
            if (source is null) return Results.NotFound();
            try { _ = await artifacts.ReadAndValidateAsync(source, request.OperatorKey, cancellationToken); }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: 403); }
            catch (Exception ex) { source.Status = "failed"; await db.SaveChangesAsync(cancellationToken); return Results.Problem(ex.Message, statusCode: 422); }
            source.Status = "restored";
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { status = "validated", snapshotId = source.Id });
        }
        var snapshot = new BackupSnapshot
        {
            Id = Guid.NewGuid(),
            Label = $"restore-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            Payload = request.Payload!,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = actor, Status = "legacy", Restorable = false,
        };
        db.BackupSnapshots.Add(snapshot);
        await db.SaveChangesAsync();
        SelfHostedCommands.Add(1, new KeyValuePair<string, object?>("operation", "restore-recorded"));
        return Results.Ok(new { status = "stored", snapshotId = snapshot.Id });
    }

    private static async Task<IResult> ListFeatureFlags(AppDbContext db)
    {
        var flags = await db.FeatureFlags.AsNoTracking().OrderBy(x => x.Key).ToListAsync();
        return Results.Ok(flags.Select(FlagResponse).ToList());
    }

    private static async Task<IResult> UpsertFeatureFlag(string key, UpsertFeatureFlagRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["key"] = ["Key is required."] });
        var actor = IdentityEndpoints.CurrentUserId(principal);
        var existing = await db.FeatureFlags.SingleOrDefaultAsync(x => x.Key == key);
        if (existing is null)
        {
            existing = new FeatureFlag { Id = Guid.NewGuid(), Key = key };
            db.FeatureFlags.Add(existing);
        }
        existing.Enabled = request?.Enabled ?? existing.Enabled;
        existing.Value = request?.Value ?? existing.Value;
        existing.UpdatedAt = clock.UtcNow;
        existing.UpdatedByUserId = actor;
        await db.SaveChangesAsync();
        SelfHostedCommands.Add(1, new KeyValuePair<string, object?>("operation", "feature-flag-upserted"));
        return Results.Ok(FlagResponse(existing));
    }

    private static BackupSnapshotResponse BackupResponse(BackupSnapshot s) => new(s.Id, s.Label, s.Payload.Length, s.CreatedAt, s.Status, s.Restorable, s.Encrypted, s.Sha256);
    private static FeatureFlagResponse FlagResponse(FeatureFlag f) => new(f.Key, f.Enabled, f.Value, f.UpdatedAt);
}

public sealed record SystemInfoRequest(string? Label, string? OperatorKey = null);
public sealed record RestoreRequest(string? Payload = null, Guid? SnapshotId = null, string? OperatorKey = null);
public sealed record UpsertFeatureFlagRequest(bool? Enabled, string? Value);
public sealed record SystemInfoResponse(string Version, int MigrationsRegistered);
public sealed record SystemStatusResponse(string Version, int AppliedCount, int PendingCount, IReadOnlyList<string> Applied, IReadOnlyList<string> Pending);
public sealed record BackupSnapshotResponse(Guid Id, string Label, int PayloadLength, DateTimeOffset CreatedAt, string Status = "legacy", bool Restorable = false, bool Encrypted = false, string? Sha256 = null);
public sealed record FeatureFlagResponse(string Key, bool Enabled, string Value, DateTimeOffset UpdatedAt);
