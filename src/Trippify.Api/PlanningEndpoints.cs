using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class PlanningEndpoints
{
    private static readonly Meter PlanningMeter = new("Trippify.Planning");
    private static readonly Counter<long> PlanningCommands = PlanningMeter.CreateCounter<long>("trippify.planning.commands");
    public static void MapPlanning(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/guides/{guideId:guid}").RequireAuthorization();
        group.MapGet("/days/{dayPosition:int}/route", GetDayRoute);
        group.MapPut("/days/{dayPosition:int}/route", ReplaceDayRoute);
        group.MapGet("/budget", GetBudget);
        group.MapPut("/budget", ReplaceBudget);
    }

    private static async Task<IResult> GetDayRoute(Guid guideId, int dayPosition, ClaimsPrincipal principal, AppDbContext db)
    {
        if (!DayPositionValid(dayPosition)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["dayPosition"] = ["Day position must be between 0 and 59."] });
        var guide = await db.TravelGuides.AsNoTracking().Where(x => x.Id == guideId && x.OwnerUserId == IdentityEndpoints.CurrentUserId(principal)).Include(x => x.Days.OrderBy(d => d.Position)).ThenInclude(x => x.Nodes.OrderBy(n => n.Position)).SingleOrDefaultAsync();
        var day = guide?.Days.SingleOrDefault(x => x.Position == dayPosition);
        if (day is null) return Results.NotFound();
        var segments = await db.GuideTransportSegments.AsNoTracking().Where(x => x.DayId == day.Id).OrderBy(x => x.Position).ToListAsync();
        return Results.Ok(new DayRouteResponse(guide!.ConcurrencyToken, day.Nodes.OrderBy(x => x.Position).Select(x => new RouteMarkerResponse(x.Id, x.Position, x.Name, x.Latitude, x.Longitude)).ToArray(), segments.Select(x => new RouteSegmentResponse(x.Id, x.Position, x.Mode.ToString(), x.Label, x.OriginName, x.DestinationName, x.DurationMinutes, x.CostPerPersonMinorUnits, x.CurrencyCode)).ToArray()));
    }

    private static async Task<IResult> ReplaceDayRoute(Guid guideId, int dayPosition, DayRouteRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        if (!DayPositionValid(dayPosition)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["dayPosition"] = ["Day position must be between 0 and 59."] });
        var errors = ValidateSegments(request.Segments); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var guide = await db.TravelGuides.Where(x => x.Id == guideId && x.OwnerUserId == IdentityEndpoints.CurrentUserId(principal)).Include(x => x.Days).ThenInclude(x => x.Nodes).SingleOrDefaultAsync();
        if (guide is null) return Results.NotFound();
        var day = guide.Days.SingleOrDefault(x => x.Position == dayPosition);
        if (day is null) return Results.NotFound();
        if (guide.ConcurrencyToken != request.ConcurrencyToken) return Results.Conflict(new { detail = "The guide changed since it was loaded.", concurrencyToken = guide.ConcurrencyToken });
        db.GuideTransportSegments.RemoveRange(db.GuideTransportSegments.Where(x => x.DayId == day.Id));
        for (var position = 0; position < request.Segments.Count; position++)
        {
            var source = request.Segments[position];
            Enum.TryParse<TransportMode>(source.Mode, true, out var mode);
            db.GuideTransportSegments.Add(new GuideTransportSegment { Id = Guid.NewGuid(), DayId = day.Id, Position = position, Mode = mode, Label = source.Label.Trim(), OriginName = source.OriginName.Trim(), DestinationName = source.DestinationName.Trim(), DurationMinutes = source.DurationMinutes, CostPerPersonMinorUnits = source.CostPerPersonMinorUnits, CurrencyCode = source.CurrencyCode.Trim().ToUpperInvariant() });
        }
        Touch(guide, clock.UtcNow); db.GuideAuditEntries.Add(Audit(guide.Id, guide.OwnerUserId, "route-replaced", guide.UpdatedAt)); await db.SaveChangesAsync();
        PlanningCommands.Add(1, new KeyValuePair<string, object?>("operation", "route-replaced"));
        return Results.Ok(await RouteResponse(guide, day.Id, db));
    }

    private static async Task<IResult> GetBudget(Guid guideId, ClaimsPrincipal principal, AppDbContext db, [FromQuery] int? partySize)
    {
        var size = partySize ?? 1;
        if (size is < 1 or > 20) return Results.ValidationProblem(new Dictionary<string, string[]> { ["partySize"] = ["Party size must be between 1 and 20."] });
        var guide = await db.TravelGuides.AsNoTracking().Where(x => x.Id == guideId && x.OwnerUserId == IdentityEndpoints.CurrentUserId(principal)).Select(x => new { x.ConcurrencyToken }).SingleOrDefaultAsync();
        if (guide is null) return Results.NotFound();
        var entries = await db.GuideBudgetEntries.AsNoTracking().Where(x => x.GuideId == guideId).OrderBy(x => x.Category).ToListAsync();
        return Results.Ok(new BudgetResponse(size, entries.Select(x => new BudgetLineResponse(x.Category.ToString(), x.AmountPerPersonMinorUnits, checked(x.AmountPerPersonMinorUnits * size), x.CurrencyCode)).ToArray()));
    }

    private static async Task<IResult> ReplaceBudget(Guid guideId, BudgetRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var errors = ValidateBudget(request.Entries); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var guide = await db.TravelGuides.Where(x => x.Id == guideId && x.OwnerUserId == IdentityEndpoints.CurrentUserId(principal)).SingleOrDefaultAsync();
        if (guide is null) return Results.NotFound();
        if (guide.ConcurrencyToken != request.ConcurrencyToken) return Results.Conflict(new { detail = "The guide changed since it was loaded.", concurrencyToken = guide.ConcurrencyToken });
        db.GuideBudgetEntries.RemoveRange(db.GuideBudgetEntries.Where(x => x.GuideId == guide.Id));
        foreach (var source in request.Entries)
        {
            Enum.TryParse<BudgetCategory>(source.Category, true, out var category);
            db.GuideBudgetEntries.Add(new GuideBudgetEntry { Id = Guid.NewGuid(), GuideId = guide.Id, Category = category, AmountPerPersonMinorUnits = source.AmountPerPersonMinorUnits, CurrencyCode = source.CurrencyCode.Trim().ToUpperInvariant() });
        }
        Touch(guide, clock.UtcNow); db.GuideAuditEntries.Add(Audit(guide.Id, guide.OwnerUserId, "budget-replaced", guide.UpdatedAt)); await db.SaveChangesAsync();
        PlanningCommands.Add(1, new KeyValuePair<string, object?>("operation", "budget-replaced"));
        var entries = await db.GuideBudgetEntries.AsNoTracking().Where(x => x.GuideId == guide.Id).OrderBy(x => x.Category).ToListAsync();
        return Results.Ok(new BudgetResponse(1, entries.Select(x => new BudgetLineResponse(x.Category.ToString(), x.AmountPerPersonMinorUnits, x.AmountPerPersonMinorUnits, x.CurrencyCode)).ToArray()));
    }

    private static async Task<DayRouteResponse> RouteResponse(TravelGuide guide, Guid dayId, AppDbContext db)
    {
        var segments = await db.GuideTransportSegments.AsNoTracking().Where(x => x.DayId == dayId).OrderBy(x => x.Position).ToListAsync();
        var nodes = await db.GuideNodes.AsNoTracking().Where(x => x.DayId == dayId).OrderBy(x => x.Position).ToListAsync();
        return new DayRouteResponse(guide.ConcurrencyToken, nodes.Select(x => new RouteMarkerResponse(x.Id, x.Position, x.Name, x.Latitude, x.Longitude)).ToArray(), segments.Select(x => new RouteSegmentResponse(x.Id, x.Position, x.Mode.ToString(), x.Label, x.OriginName, x.DestinationName, x.DurationMinutes, x.CostPerPersonMinorUnits, x.CurrencyCode)).ToArray());
    }

    private static bool DayPositionValid(int dayPosition) => dayPosition is >= 0 and <= 59;
    private static Dictionary<string, string[]> ValidateSegments(IReadOnlyList<RouteSegmentRequest> segments)
    {
        var errors = new Dictionary<string, string[]>();
        if (segments.Count > 40) errors["segments"] = ["A day supports at most 40 transport segments."];
        if (segments.Any(x => !Enum.TryParse<TransportMode>(x.Mode, true, out _))) errors["mode"] = ["Unknown transport mode."];
        if (segments.Any(x => x.DurationMinutes is < 0 or > 1440)) errors["durationMinutes"] = ["Duration must be between 0 and 1440 minutes."];
        if (segments.Any(x => x.CostPerPersonMinorUnits is < 0 or > 1_000_000_000)) errors["costPerPersonMinorUnits"] = ["Cost must be between 0 and 1000000000 minor units."];
        if (segments.Any(x => x.CurrencyCode.Trim().Length != 3)) errors["currencyCode"] = ["Currency code must contain three characters."];
        if (segments.Any(x => x.Label.Trim().Length > 160 || x.OriginName.Trim().Length > 200 || x.DestinationName.Trim().Length > 200)) errors["names"] = ["Segment labels and place names exceed supported lengths."];
        return errors;
    }
    private static Dictionary<string, string[]> ValidateBudget(IReadOnlyList<BudgetEntryRequest> entries)
    {
        var errors = new Dictionary<string, string[]>();
        if (entries.Count > 12) errors["entries"] = ["A guide supports at most 12 budget categories."];
        if (entries.Select(x => x.Category.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count) errors["category"] = ["Budget categories must be unique."];
        if (entries.Any(x => !Enum.TryParse<BudgetCategory>(x.Category, true, out _))) errors["category"] = ["Unknown budget category."];
        if (entries.Any(x => x.AmountPerPersonMinorUnits is < 0 or > 1_000_000_000)) errors["amountPerPersonMinorUnits"] = ["Amounts must be between 0 and 1000000000 minor units."];
        if (entries.Any(x => x.CurrencyCode.Trim().Length != 3)) errors["currencyCode"] = ["Currency code must contain three characters."];
        return errors;
    }
    private static void Touch(TravelGuide guide, DateTimeOffset now) { guide.UpdatedAt = now; guide.ConcurrencyToken = Guid.NewGuid(); }
    private static GuideAuditEntry Audit(Guid guide, Guid actor, string action, DateTimeOffset now) => new() { Id = Guid.NewGuid(), GuideId = guide, ActorUserId = actor, Action = action, OccurredAt = now };
}

public sealed record RouteSegmentRequest(string Mode, string Label, string OriginName, string DestinationName, int DurationMinutes, long CostPerPersonMinorUnits, string CurrencyCode);
public sealed record DayRouteRequest(Guid ConcurrencyToken, IReadOnlyList<RouteSegmentRequest> Segments);
public sealed record RouteMarkerResponse(Guid NodeId, int Position, string Name, double? Latitude, double? Longitude);
public sealed record RouteSegmentResponse(Guid Id, int Position, string Mode, string Label, string OriginName, string DestinationName, int DurationMinutes, long CostPerPersonMinorUnits, string CurrencyCode);
public sealed record DayRouteResponse(Guid ConcurrencyToken, RouteMarkerResponse[] Markers, RouteSegmentResponse[] Segments);
public sealed record BudgetEntryRequest(string Category, long AmountPerPersonMinorUnits, string CurrencyCode);
public sealed record BudgetRequest(Guid ConcurrencyToken, IReadOnlyList<BudgetEntryRequest> Entries);
public sealed record BudgetLineResponse(string Category, long AmountPerPersonMinorUnits, long PartyTotalMinorUnits, string CurrencyCode);
public sealed record BudgetResponse(int PartySize, BudgetLineResponse[] Lines);
