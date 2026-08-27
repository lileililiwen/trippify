using System.Diagnostics.Metrics;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class VersioningEndpoints
{
    private static readonly Meter VersioningMeter = new("Trippify.Versioning");
    private static readonly Counter<long> VersioningCommands = VersioningMeter.CreateCounter<long>("trippify.versioning.commands");

    public static void MapVersioning(this WebApplication app)
    {
        var creatorGroup = app.MapGroup("/api/v1/guides/{guideId:guid}").RequireAuthorization();
        creatorGroup.MapPost("/releases", PublishRelease);

        var publicGroup = app.MapGroup("/api/v1").AllowAnonymous();
        publicGroup.MapGet("/guides/{guideId:guid}/releases", ListReleases);
        publicGroup.MapGet("/guides/{guideId:guid}/freshness", GetFreshness);
        publicGroup.MapGet("/releases/{releaseId:guid}", GetRelease);
    }

    public static async Task<Guid> CreateInitialReleaseAsync(AppDbContext db, TravelGuide guide, Guid publisherUserId, IClock clock, BackgroundJobProcessor processor)
    {
        var version = 1;
        var release = new GuideRelease
        {
            Id = Guid.NewGuid(),
            GuideId = guide.Id,
            VersionNumber = version,
            Changelog = "Initial release.",
            Title = guide.Title,
            PublishedAt = clock.UtcNow,
            PublisherUserId = publisherUserId,
            NodeSummary = BuildNodeSummary(guide),
        };
        db.GuideReleases.Add(release);
        await NotificationFanOut.QueueGuideUpdatedAsync(db, guide, release, clock, processor);
        return release.Id;
    }

    private static async Task<IResult> PublishRelease(Guid guideId, PublishReleaseRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock, BackgroundJobProcessor processor)
    {
        var publisher = IdentityEndpoints.CurrentUserId(principal);
        var guide = await db.TravelGuides.AsNoTracking().Include(x => x.Days).ThenInclude(x => x.Nodes).SingleOrDefaultAsync(x => x.Id == guideId);
        if (guide is null) return Results.NotFound();
        if (guide.OwnerUserId != publisher) return Results.Forbid();
        if (guide.Lifecycle is not (GuideLifecycle.FreePublic or GuideLifecycle.Paid))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["lifecycle"] = ["Only published guides can receive releases."] });
        if (string.IsNullOrWhiteSpace(request.Changelog) || request.Changelog.Trim().Length > 4000)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["changelog"] = ["Changelog must contain 1 to 4000 characters."] });
        var latest = await db.GuideReleases.AsNoTracking().Where(x => x.GuideId == guideId).OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync();
        if (latest is not null && latest.NodeSummary == BuildNodeSummary(guide))
            return Results.Ok(new GuideReleaseResponse(latest.Id, latest.GuideId, latest.VersionNumber, latest.Changelog, latest.Title, latest.PublishedAt, latest.NodeSummary));
        var nextVersion = (latest?.VersionNumber ?? 0) + 1;
        var release = new GuideRelease
        {
            Id = Guid.NewGuid(),
            GuideId = guideId,
            VersionNumber = nextVersion,
            Changelog = request.Changelog.Trim(),
            Title = guide.Title,
            PublishedAt = clock.UtcNow,
            PublisherUserId = publisher,
            NodeSummary = BuildNodeSummary(guide),
        };
        db.GuideReleases.Add(release);
        await NotificationFanOut.QueueGuideUpdatedAsync(db, guide, release, clock, processor);
        VersioningCommands.Add(1, new KeyValuePair<string, object?>("operation", "release-published"));
        return Results.Created($"/api/v1/releases/{release.Id}", ToResponse(release));
    }

    private static async Task<IResult> ListReleases(Guid guideId, int? limit, AppDbContext db)
    {
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        var pageSize = Math.Clamp(limit ?? 20, 1, 100);
        var releases = await db.GuideReleases.AsNoTracking()
            .Where(x => x.GuideId == guideId)
            .OrderByDescending(x => x.VersionNumber)
            .Take(pageSize)
            .ToListAsync();
        VersioningCommands.Add(1, new KeyValuePair<string, object?>("operation", "releases-listed"));
        return Results.Ok(new GuideReleaseListResponse(releases.Count, releases.Select(ToResponse).ToList()));
    }

    private static async Task<IResult> GetRelease(Guid releaseId, AppDbContext db)
    {
        var release = await db.GuideReleases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == releaseId);
        if (release is null) return Results.NotFound();
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == release.GuideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        VersioningCommands.Add(1, new KeyValuePair<string, object?>("operation", "release-read"));
        return Results.Ok(ToResponse(release));
    }

    private static async Task<IResult> GetFreshness(Guid guideId, AppDbContext db, IClock clock)
    {
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == guideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        var latest = await db.GuideReleases.AsNoTracking().Where(x => x.GuideId == guideId).OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync();
        var days = latest is null ? (int?)null : (int)(clock.UtcNow - latest.PublishedAt).TotalDays;
        return Results.Ok(new FreshnessSignalResponse(guideId, latest?.VersionNumber ?? 0, latest?.PublishedAt, days));
    }

    private static GuideReleaseResponse ToResponse(GuideRelease r) => new(r.Id, r.GuideId, r.VersionNumber, r.Changelog, r.Title, r.PublishedAt, r.NodeSummary);

    private static string BuildNodeSummary(TravelGuide guide)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var day in guide.Days.OrderBy(d => d.Position))
        {
            sb.Append("Day ").Append(day.Position).Append(':').Append(day.Title).Append('|');
            foreach (var node in day.Nodes.OrderBy(n => n.Position))
            {
                sb.Append(node.Name).Append('@').Append(node.Latitude).Append(',').Append(node.Longitude).Append(';');
            }
        }
        return sb.ToString();
    }
}

public sealed record PublishReleaseRequest(string Changelog);
public sealed record GuideReleaseResponse(Guid Id, Guid GuideId, int VersionNumber, string Changelog, string Title, DateTimeOffset PublishedAt, string NodeSummary);
public sealed record GuideReleaseListResponse(int Total, IReadOnlyList<GuideReleaseResponse> Items);
public sealed record FreshnessSignalResponse(Guid GuideId, int LatestVersion, DateTimeOffset? LatestPublishedAt, int? DaysSinceLatest);
