using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class GuideEndpoints
{
    private static readonly Meter GuideMeter = new("Trippify.Guides");
    private static readonly Counter<long> GuideCommands = GuideMeter.CreateCounter<long>("trippify.guide.commands");
    public static void MapGuides(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/guides").RequireAuthorization();
        group.MapGet("/", ListMine);
        group.MapPost("/", Create);
        group.MapGet("/{guideId:guid}", Get);
        group.MapPut("/{guideId:guid}/metadata", UpdateMetadata);
        group.MapPut("/{guideId:guid}/structure", ReplaceStructure);
        group.MapPost("/{guideId:guid}/media", AddMedia);
        group.MapPut("/{guideId:guid}/lifecycle", SetLifecycle);
        group.MapDelete("/{guideId:guid}", Delete);
    }

    private static async Task<IResult> ListMine(ClaimsPrincipal principal, AppDbContext db)
    {
        var owner = IdentityEndpoints.CurrentUserId(principal);
        var guides = await db.TravelGuides.AsNoTracking().Where(x => x.OwnerUserId == owner).OrderByDescending(x => x.UpdatedAt).Select(x => new GuideListItem(x.Id, x.Title, x.CountryCode, x.TripDays, x.Lifecycle.ToString(), x.ConcurrencyToken, x.UpdatedAt)).ToListAsync();
        return Results.Ok(guides);
    }

    private static async Task<IResult> Create(GuideMetadataRequest request, HttpRequest httpRequest, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var owner = IdentityEndpoints.CurrentUserId(principal);
        if (!await db.CreatorProfiles.AnyAsync(x => x.UserId == owner && x.Status == CreatorStatus.Active)) return Results.Forbid();
        var errors = ValidateMetadata(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var key = httpRequest.Headers["Idempotency-Key"].ToString();
        if (key.Length > 100) return Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["Idempotency key must not exceed 100 characters."] });
        if (key.Length > 0)
        {
            var existing = await db.GuideCommandReceipts.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerUserId == owner && x.IdempotencyKey == key && x.Operation == "create");
            if (existing is not null) return Results.Ok(new { id = existing.ResourceId, replayed = true });
        }
        var now = clock.UtcNow;
        var guide = new TravelGuide { Id = Guid.NewGuid(), OwnerUserId = owner, Title = request.Title.Trim(), Subtitle = request.Subtitle.Trim(), Summary = request.Summary.Trim(), CoverUrl = request.CoverUrl, CountryCode = request.CountryCode.Trim().ToUpperInvariant(), Cities = Normalize(request.Cities, 50), Tags = Normalize(request.Tags, 50), TripDays = request.TripDays, CreatedAt = now, UpdatedAt = now };
        db.TravelGuides.Add(guide); db.GuideAuditEntries.Add(Audit(guide.Id, owner, "created", now));
        if (key.Length > 0) db.GuideCommandReceipts.Add(new GuideCommandReceipt { OwnerUserId = owner, IdempotencyKey = key, Operation = "create", ResourceId = guide.Id, CreatedAt = now });
        await db.SaveChangesAsync();
        GuideCommands.Add(1, new KeyValuePair<string, object?>("operation", "create"));
        return Results.Created($"/api/v1/guides/{guide.Id}", new { guide.Id, guide.ConcurrencyToken });
    }

    private static async Task<IResult> Get(Guid guideId, ClaimsPrincipal principal, AppDbContext db)
    {
        var guide = await OwnedGuide(guideId, principal, db).AsNoTracking().Include(x => x.Days.OrderBy(d => d.Position)).ThenInclude(x => x.Nodes.OrderBy(n => n.Position)).Include(x => x.Sections.OrderBy(s => s.Position)).Include(x => x.Media.OrderBy(m => m.Position)).SingleOrDefaultAsync();
        return guide is null ? Results.NotFound() : Results.Ok(Project(guide));
    }

    private static async Task<IResult> UpdateMetadata(Guid guideId, GuideMetadataRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var errors = ValidateMetadata(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var guide = await OwnedGuide(guideId, principal, db).SingleOrDefaultAsync(); if (guide is null) return Results.NotFound();
        if (guide.ConcurrencyToken != request.ConcurrencyToken) return Conflict(guide.ConcurrencyToken);
        guide.Title = request.Title.Trim(); guide.Subtitle = request.Subtitle.Trim(); guide.Summary = request.Summary.Trim(); guide.CoverUrl = request.CoverUrl; guide.CountryCode = request.CountryCode.Trim().ToUpperInvariant(); guide.Cities = Normalize(request.Cities, 50); guide.Tags = Normalize(request.Tags, 50); guide.TripDays = request.TripDays; Touch(guide, clock.UtcNow); db.GuideAuditEntries.Add(Audit(guide.Id, guide.OwnerUserId, "metadata-updated", guide.UpdatedAt)); await db.SaveChangesAsync();
        return Results.Ok(new { guide.ConcurrencyToken });
    }

    private static async Task<IResult> ReplaceStructure(Guid guideId, GuideStructureRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        if (request.Days.Count > 60 || request.Sections.Count > 50 || request.Days.Any(x => x.Nodes.Count > 100)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["structure"] = ["Guide structure exceeds supported limits."] });
        var guide = await OwnedGuide(guideId, principal, db).Include(x => x.Days).ThenInclude(x => x.Nodes).Include(x => x.Sections).SingleOrDefaultAsync(); if (guide is null) return Results.NotFound();
        if (guide.ConcurrencyToken != request.ConcurrencyToken) return Conflict(guide.ConcurrencyToken);
        db.GuideNodes.RemoveRange(guide.Days.SelectMany(x => x.Nodes)); db.GuideDays.RemoveRange(guide.Days); db.GuideSections.RemoveRange(guide.Sections);
        for (var dayPosition = 0; dayPosition < request.Days.Count; dayPosition++) { var source = request.Days[dayPosition]; var day = new GuideDay { Id = Guid.NewGuid(), GuideId = guide.Id, Position = dayPosition, Title = source.Title.Trim(), Notes = source.Notes.Trim() }; db.GuideDays.Add(day); for (var nodePosition = 0; nodePosition < source.Nodes.Count; nodePosition++) { var node = source.Nodes[nodePosition]; if (!Enum.TryParse<GuideNodeType>(node.Type, true, out var type)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["nodeType"] = [$"Unknown node type: {node.Type}"] }); if (!CoordinatesValid(node.Latitude, node.Longitude)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["coordinates"] = ["Latitude and longitude are invalid."] }); db.GuideNodes.Add(new GuideNode { Id = Guid.NewGuid(), DayId = day.Id, Position = nodePosition, Type = type, Name = node.Name.Trim(), Address = node.Address, Latitude = node.Latitude, Longitude = node.Longitude, ArrivalTime = node.ArrivalTime, DepartureTime = node.DepartureTime, StayMinutes = node.StayMinutes, TicketInformation = node.TicketInformation, ReservationInformation = node.ReservationInformation, OpeningHours = node.OpeningHours, Notes = node.Notes.Trim() }); } }
        for (var position = 0; position < request.Sections.Count; position++) { var source = request.Sections[position]; if (!Enum.TryParse<GuideSectionType>(source.Type, true, out var type)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["sectionType"] = [$"Unknown section type: {source.Type}"] }); db.GuideSections.Add(new GuideSection { Id = Guid.NewGuid(), GuideId = guide.Id, Position = position, Type = type, Title = source.Title.Trim(), Body = source.Body.Trim() }); }
        guide.TripDays = request.Days.Count; Touch(guide, clock.UtcNow); db.GuideAuditEntries.Add(Audit(guide.Id, guide.OwnerUserId, "structure-replaced", guide.UpdatedAt)); await db.SaveChangesAsync(); GuideCommands.Add(1, new KeyValuePair<string, object?>("operation", "structure-replaced")); return Results.Ok(new { guide.ConcurrencyToken });
    }

    private static async Task<IResult> AddMedia(Guid guideId, MediaRequest request, ClaimsPrincipal principal, AppDbContext db, IObjectStorage storage, IClock clock)
    {
        var guide = await OwnedGuide(guideId, principal, db).SingleOrDefaultAsync(); if (guide is null) return Results.NotFound(); if (guide.ConcurrencyToken != request.ConcurrencyToken) return Conflict(guide.ConcurrencyToken);
        byte[] bytes; try { bytes = Convert.FromBase64String(request.ContentBase64); } catch (FormatException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["contentBase64"] = ["Media content is not valid base64."] }); }
        if (bytes.Length is 0 or > 10_000_000) return Results.ValidationProblem(new Dictionary<string, string[]> { ["contentBase64"] = ["Media must contain 1 byte to 10 MB."] });
        try { await using var stream = new MemoryStream(bytes); var uri = await storage.PutAsync(request.StorageKey, stream, default); db.GuideMedia.Add(new GuideMedia { Id = Guid.NewGuid(), GuideId = guide.Id, Position = await db.GuideMedia.CountAsync(x => x.GuideId == guide.Id), StorageKey = request.StorageKey, Url = uri.ToString(), Caption = request.Caption }); Touch(guide, clock.UtcNow); await db.SaveChangesAsync(); return Results.Ok(new { guide.ConcurrencyToken, url = uri }); }
        catch (Exception error) when (error is IOException or NotSupportedException) { return Results.Problem("Media storage is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable); }
    }

    private static async Task<IResult> SetLifecycle(Guid guideId, LifecycleRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var guide = await OwnedGuide(guideId, principal, db).SingleOrDefaultAsync(); if (guide is null) return Results.NotFound(); if (guide.ConcurrencyToken != request.ConcurrencyToken) return Conflict(guide.ConcurrencyToken);
        if (!Enum.TryParse<GuideLifecycle>(request.Lifecycle, true, out var lifecycle) || lifecycle is GuideLifecycle.FreePublic or GuideLifecycle.Paid or GuideLifecycle.Unlisted) return Results.ValidationProblem(new Dictionary<string, string[]> { ["lifecycle"] = ["This change supports Draft, Private, and Archived states only."] });
        guide.Lifecycle = lifecycle; Touch(guide, clock.UtcNow); db.GuideAuditEntries.Add(Audit(guide.Id, guide.OwnerUserId, "lifecycle:" + lifecycle, guide.UpdatedAt)); await db.SaveChangesAsync(); return Results.Ok(new { guide.ConcurrencyToken });
    }

    private static async Task<IResult> Delete(Guid guideId, [FromBody] ConcurrencyRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var guide = await OwnedGuide(guideId, principal, db).SingleOrDefaultAsync(); if (guide is null) return Results.NotFound(); if (guide.ConcurrencyToken != request.ConcurrencyToken) return Conflict(guide.ConcurrencyToken); guide.DeletedAt = clock.UtcNow; guide.UpdatedAt = clock.UtcNow; guide.ConcurrencyToken = Guid.NewGuid(); db.GuideAuditEntries.Add(Audit(guide.Id, guide.OwnerUserId, "deleted", guide.UpdatedAt)); await db.SaveChangesAsync(); return Results.NoContent();
    }

    private static IQueryable<TravelGuide> OwnedGuide(Guid id, ClaimsPrincipal principal, AppDbContext db) { var owner = IdentityEndpoints.CurrentUserId(principal); return db.TravelGuides.Where(x => x.Id == id && x.OwnerUserId == owner); }
    private static Dictionary<string, string[]> ValidateMetadata(GuideMetadataRequest request) { var errors = new Dictionary<string, string[]>(); if (request.Title.Trim().Length is < 3 or > 160) errors["title"] = ["Title must contain 3 to 160 characters."]; if (request.CountryCode.Trim().Length != 2) errors["countryCode"] = ["Country code must contain two characters."]; if (request.TripDays is < 0 or > 60) errors["tripDays"] = ["Trip days must be between 0 and 60."]; return errors; }
    private static bool CoordinatesValid(double? latitude, double? longitude) => latitude is null && longitude is null || latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    private static string[] Normalize(IEnumerable<string> values, int limit) => values.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(limit).ToArray();
    private static void Touch(TravelGuide guide, DateTimeOffset now) { guide.UpdatedAt = now; guide.ConcurrencyToken = Guid.NewGuid(); }
    private static GuideAuditEntry Audit(Guid guide, Guid actor, string action, DateTimeOffset now) => new() { Id = Guid.NewGuid(), GuideId = guide, ActorUserId = actor, Action = action, OccurredAt = now };
    private static IResult Conflict(Guid token) => Results.Conflict(new { detail = "The guide changed since it was loaded.", concurrencyToken = token });
    private static GuideDetail Project(TravelGuide x) => new(x.Id, x.Title, x.Subtitle, x.Summary, x.CoverUrl, x.CountryCode, x.Cities, x.Tags, x.TripDays, x.Lifecycle.ToString(), x.ConcurrencyToken, x.Days.OrderBy(d => d.Position).Select(d => new DayResponse(d.Id, d.Position, d.Title, d.Notes, d.Nodes.OrderBy(n => n.Position).Select(n => new NodeResponse(n.Id, n.Position, n.Type.ToString(), n.Name, n.Address, n.Latitude, n.Longitude, n.ArrivalTime, n.DepartureTime, n.StayMinutes, n.Notes)).ToArray())).ToArray(), x.Sections.OrderBy(s => s.Position).Select(s => new SectionResponse(s.Id, s.Position, s.Type.ToString(), s.Title, s.Body)).ToArray(), x.Media.OrderBy(m => m.Position).Select(m => new MediaResponse(m.Id, m.Position, m.Url, m.Caption)).ToArray());
}

public sealed record GuideMetadataRequest(string Title, string Subtitle, string Summary, string? CoverUrl, string CountryCode, string[] Cities, string[] Tags, int TripDays, Guid? ConcurrencyToken = null);
public sealed record GuideStructureRequest(Guid ConcurrencyToken, List<DayRequest> Days, List<SectionRequest> Sections);
public sealed record DayRequest(string Title, string Notes, List<NodeRequest> Nodes);
public sealed record NodeRequest(string Type, string Name, string? Address, double? Latitude, double? Longitude, TimeOnly? ArrivalTime, TimeOnly? DepartureTime, int? StayMinutes, string? TicketInformation, string? ReservationInformation, string? OpeningHours, string Notes);
public sealed record SectionRequest(string Type, string Title, string Body);
public sealed record MediaRequest(Guid ConcurrencyToken, string StorageKey, string ContentBase64, string? Caption);
public sealed record LifecycleRequest(Guid ConcurrencyToken, string Lifecycle);
public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
public sealed record GuideListItem(Guid Id, string Title, string CountryCode, int TripDays, string Lifecycle, Guid ConcurrencyToken, DateTimeOffset UpdatedAt);
public sealed record GuideDetail(Guid Id, string Title, string Subtitle, string Summary, string? CoverUrl, string CountryCode, string[] Cities, string[] Tags, int TripDays, string Lifecycle, Guid ConcurrencyToken, DayResponse[] Days, SectionResponse[] Sections, MediaResponse[] Media);
public sealed record DayResponse(Guid Id, int Position, string Title, string Notes, NodeResponse[] Nodes);
public sealed record NodeResponse(Guid Id, int Position, string Type, string Name, string? Address, double? Latitude, double? Longitude, TimeOnly? ArrivalTime, TimeOnly? DepartureTime, int? StayMinutes, string Notes);
public sealed record SectionResponse(Guid Id, int Position, string Type, string Title, string Body);
public sealed record MediaResponse(Guid Id, int Position, string Url, string? Caption);
