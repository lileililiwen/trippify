using System.Security.Claims;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class LibraryEndpoints
{
    private static readonly Meter LibraryMeter = new("Trippify.Library");
    private static readonly Counter<long> LibraryCommands = LibraryMeter.CreateCounter<long>("trippify.library.commands");
    public static void MapLibrary(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/library").RequireAuthorization();
        group.MapGet("/favorites", ListFavorites);
        group.MapPost("/favorites/{guideId:guid}", AddFavorite);
        group.MapDelete("/favorites/{guideId:guid}", RemoveFavorite);
        group.MapGet("/trips", ListTrips);
        group.MapPost("/trips", CreateTrip);
        group.MapPatch("/trips/{tripId:guid}", UpdateTrip);
        group.MapDelete("/trips/{tripId:guid}", DeleteTrip);
        group.MapPost("/forks", ForkGuide);
    }

    private static async Task<IResult> ListFavorites(ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var favorites = await db.GuideFavorites.AsNoTracking().Where(x => x.UserId == user)
            .Join(db.TravelGuides.AsNoTracking().Where(x => x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid), f => f.GuideId, g => g.Id, (f, g) => new FavoriteResponse(g.Id.ToString(), g.Slug, g.Title, g.Subtitle, g.CountryCode, g.TripDays, g.Lifecycle == GuideLifecycle.Paid ? "paid" : "free", f.CreatedAt))            .OrderByDescending(x => x.FavoritedAt).ToListAsync();
        return Results.Ok(favorites);
    }

    private static async Task<IResult> AddFavorite(Guid guideId, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var guide = await db.TravelGuides.AsNoTracking().FirstOrDefaultAsync(x => x.Id == guideId && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid));
        if (guide is null) return Results.NotFound();
        if (await db.GuideFavorites.AnyAsync(x => x.UserId == user && x.GuideId == guideId)) return Results.Conflict(new { detail = "Guide is already a favorite." });
        db.GuideFavorites.Add(new GuideFavorite { Id = Guid.NewGuid(), UserId = user, GuideId = guideId, CreatedAt = clock.UtcNow });
        await db.SaveChangesAsync(); LibraryCommands.Add(1, new KeyValuePair<string, object?>("operation", "favorite-added"));
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveFavorite(Guid guideId, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var favorite = await db.GuideFavorites.SingleOrDefaultAsync(x => x.UserId == user && x.GuideId == guideId);
        if (favorite is null) return Results.NotFound();
        db.GuideFavorites.Remove(favorite); await db.SaveChangesAsync();
        LibraryCommands.Add(1, new KeyValuePair<string, object?>("operation", "favorite-removed"));
        return Results.NoContent();
    }

    private static async Task<IResult> ListTrips(ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var trips = await db.UserTrips.AsNoTracking().Where(x => x.UserId == user).OrderByDescending(x => x.UpdatedAt).Select(x => new TripResponse(x.Id, x.Title, x.SourceGuideId, x.ForkedGuideId, x.Status.ToString(), x.Notes, x.CreatedAt, x.UpdatedAt)).ToListAsync();
        return Results.Ok(trips);
    }

    private static async Task<IResult> CreateTrip(CreateTripRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var guide = await db.TravelGuides.AsNoTracking().Include(x => x.Days).ThenInclude(x => x.Nodes).Include(x => x.Sections).SingleOrDefaultAsync(x => x.Id == request.GuideId);
        if (guide is null || !await CanAccessSourceAsync(guide, user, db)) return Results.NotFound();
        var now = clock.UtcNow;
        var trip = new UserTrip { Id = Guid.NewGuid(), UserId = user, Title = string.IsNullOrWhiteSpace(request.Title) ? guide.Title : request.Title.Trim(), SourceGuideId = guide.Id, Status = TripStatus.Planning, CreatedAt = now, UpdatedAt = now };
        db.UserTrips.Add(trip); await db.SaveChangesAsync();
        LibraryCommands.Add(1, new KeyValuePair<string, object?>("operation", "trip-created"));
        return Results.Created($"/api/v1/library/trips/{trip.Id}", new TripResponse(trip.Id, trip.Title, trip.SourceGuideId, trip.ForkedGuideId, trip.Status.ToString(), trip.Notes, trip.CreatedAt, trip.UpdatedAt));
    }

    private static async Task<IResult> UpdateTrip(Guid tripId, UpdateTripRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var trip = await db.UserTrips.SingleOrDefaultAsync(x => x.Id == tripId && x.UserId == user);
        if (trip is null) return Results.NotFound();
        if (request.Title is not null) { var trimmed = request.Title.Trim(); if (trimmed.Length is < 1 or > 160) return Results.ValidationProblem(new Dictionary<string, string[]> { ["title"] = ["Title must contain 1 to 160 characters."] }); trip.Title = trimmed; }
        if (request.Notes is not null) { if (request.Notes.Length > 4000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["notes"] = ["Notes cannot exceed 4000 characters."] }); trip.Notes = request.Notes; }
        if (request.Status is not null) { if (!Enum.TryParse<TripStatus>(request.Status, true, out var status)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Unknown trip status."] }); trip.Status = status; }
        trip.UpdatedAt = clock.UtcNow; await db.SaveChangesAsync();
        LibraryCommands.Add(1, new KeyValuePair<string, object?>("operation", "trip-updated"));
        return Results.Ok(new TripResponse(trip.Id, trip.Title, trip.SourceGuideId, trip.ForkedGuideId, trip.Status.ToString(), trip.Notes, trip.CreatedAt, trip.UpdatedAt));
    }

    private static async Task<IResult> DeleteTrip(Guid tripId, ClaimsPrincipal principal, AppDbContext db)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var trip = await db.UserTrips.SingleOrDefaultAsync(x => x.Id == tripId && x.UserId == user);
        if (trip is null) return Results.NotFound();
        db.UserTrips.Remove(trip); await db.SaveChangesAsync();
        LibraryCommands.Add(1, new KeyValuePair<string, object?>("operation", "trip-deleted"));
        return Results.NoContent();
    }

    private static async Task<IResult> ForkGuide(ForkGuideRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var user = IdentityEndpoints.CurrentUserId(principal);
        var source = await db.TravelGuides.AsNoTracking().Include(x => x.Days.OrderBy(d => d.Position)).ThenInclude(x => x.Nodes.OrderBy(n => n.Position)).Include(x => x.Sections.OrderBy(s => s.Position)).SingleOrDefaultAsync(x => x.Id == request.GuideId);
        if (source is null || !await CanAccessSourceAsync(source, user, db)) return Results.NotFound();
        if (await db.TravelGuides.IgnoreQueryFilters().AnyAsync(x => x.OwnerUserId == user && x.SourceGuideId == source.Id && x.DeletedAt == null)) return Results.Conflict(new { detail = "You already forked this guide." });
        var now = clock.UtcNow;
        var slug = await UniqueSlug(db, source.Title);
        var fork = new TravelGuide { Id = Guid.NewGuid(), OwnerUserId = user, Title = source.Title, Subtitle = source.Subtitle, Summary = source.Summary, CoverUrl = source.CoverUrl, CountryCode = source.CountryCode, Cities = (string[])source.Cities.Clone(), Tags = (string[])source.Tags.Clone(), TripDays = source.TripDays, Slug = slug, Lifecycle = GuideLifecycle.Draft, SourceGuideId = source.Id, ForkedAt = now, CreatedAt = now, UpdatedAt = now };
        var dayIdMap = new Dictionary<Guid, Guid>();
        var nodeIdMap = new Dictionary<Guid, Guid>();
        foreach (var sourceDay in source.Days)
        {
            var day = new GuideDay { Id = Guid.NewGuid(), GuideId = fork.Id, Position = sourceDay.Position, Title = sourceDay.Title, Notes = sourceDay.Notes };
            dayIdMap[sourceDay.Id] = day.Id;
            foreach (var sourceNode in sourceDay.Nodes) { var node = new GuideNode { Id = Guid.NewGuid(), DayId = day.Id, Position = sourceNode.Position, Type = sourceNode.Type, Name = sourceNode.Name, Address = sourceNode.Address, Latitude = sourceNode.Latitude, Longitude = sourceNode.Longitude, ArrivalTime = sourceNode.ArrivalTime, DepartureTime = sourceNode.DepartureTime, StayMinutes = sourceNode.StayMinutes, Notes = sourceNode.Notes }; nodeIdMap[sourceNode.Id] = node.Id; db.GuideNodes.Add(node); }
            db.GuideDays.Add(day);
        }
        foreach (var sourceSection in source.Sections) db.GuideSections.Add(new GuideSection { Id = Guid.NewGuid(), GuideId = fork.Id, Position = sourceSection.Position, Type = sourceSection.Type, Title = sourceSection.Title, Body = sourceSection.Body });
        db.TravelGuides.Add(fork);
        db.GuideAuditEntries.Add(new GuideAuditEntry { Id = Guid.NewGuid(), GuideId = source.Id, ActorUserId = user, Action = "forked", OccurredAt = now });
        await db.SaveChangesAsync();
        LibraryCommands.Add(1, new KeyValuePair<string, object?>("operation", "fork-created"));
        return Results.Created($"/api/v1/guides/{fork.Id}", new ForkResponse(fork.Id, fork.Slug, fork.Title, fork.ConcurrencyToken, source.Id, source.Title));
    }

    private static async Task<bool> CanAccessSourceAsync(TravelGuide guide, Guid userId, AppDbContext db)
    {
        if (guide.Lifecycle is not (GuideLifecycle.FreePublic or GuideLifecycle.Paid)) return false;
        if (guide.OwnerUserId == userId || guide.Lifecycle == GuideLifecycle.FreePublic) return true;
        return await db.PurchaseEntitlements.AsNoTracking().AnyAsync(x => x.GuideId == guide.Id && x.UserId == userId && x.RevokedAt == null);
    }

    private static async Task<string> UniqueSlug(AppDbContext db, string title)
    {
        var baseSlug = Slugify(title); var candidate = baseSlug; var suffix = 1;
        while (await db.TravelGuides.IgnoreQueryFilters().AnyAsync(x => x.Slug == candidate)) { candidate = ++suffix > 50 ? $"{baseSlug}-{Guid.NewGuid().ToString("N")[..8]}" : $"{baseSlug}-{suffix}"; }
        return candidate;
    }
    private static string Slugify(string title)
    {
        var slug = new string(title.Trim().ToLowerInvariant().Select(x => char.IsAsciiLetterOrDigit(x) ? x : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        return slug.Length switch { 0 => "guide", > 160 => slug[..160].Trim('-'), _ => slug };
    }
}

public sealed record FavoriteResponse(string GuideId, string Slug, string Title, string Subtitle, string CountryCode, int TripDays, string Pricing, DateTimeOffset FavoritedAt);
public sealed record CreateTripRequest(Guid GuideId, string? Title = null);
public sealed record UpdateTripRequest(string? Title = null, string? Notes = null, string? Status = null);
public sealed record TripResponse(Guid Id, string Title, Guid? SourceGuideId, Guid? ForkedGuideId, string Status, string Notes, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ForkGuideRequest(Guid GuideId);
public sealed record ForkResponse(Guid Id, string Slug, string Title, Guid ConcurrencyToken, Guid SourceGuideId, string SourceTitle);
