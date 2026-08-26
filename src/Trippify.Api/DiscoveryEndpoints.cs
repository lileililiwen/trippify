using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class DiscoveryEndpoints
{
    private static readonly Meter DiscoveryMeter = new("Trippify.Discovery");
    private static readonly Counter<long> DiscoveryQueries = DiscoveryMeter.CreateCounter<long>("trippify.discovery.queries");
    public static void MapDiscovery(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/discovery").AllowAnonymous();
        group.MapGet("/guides", Search);
        group.MapGet("/guides/{slug}", GetBySlug);
        group.MapGet("/authors/{slug}", Author);
    }

    private static async Task<IResult> Search(AppDbContext db, [FromQuery] string? country, [FromQuery] string? city, [FromQuery] string? tag, [FromQuery] string? pricing, [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page is < 1 or > 100 || pageSize is < 1 or > 50) return Results.ValidationProblem(new Dictionary<string, string[]> { ["page"] = ["Page must be 1-100 and page size 1-50."] });
        var query = db.TravelGuides.AsNoTracking().Where(x => x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid);
        if (!string.IsNullOrWhiteSpace(country)) query = query.Where(x => x.CountryCode == country.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(city)) { var cityTerm = city.Trim().ToLowerInvariant(); query = query.Where(x => x.Cities.Any(c => c.ToLower().Contains(cityTerm))); }
        if (!string.IsNullOrWhiteSpace(tag)) query = query.Where(x => x.Tags.Contains(tag.Trim().ToLowerInvariant()));
        query = pricing?.Trim().ToLowerInvariant() switch { "free" => query.Where(x => x.Lifecycle == GuideLifecycle.FreePublic), "paid" => query.Where(x => x.Lifecycle == GuideLifecycle.Paid), _ => query };
        if (!string.IsNullOrWhiteSpace(q)) { var term = q.Trim().ToLowerInvariant(); query = query.Where(x => x.Title.ToLower().Contains(term) || x.Subtitle.ToLower().Contains(term) || x.Summary.ToLower().Contains(term)); }
        var total = await query.CountAsync();
        var authors = db.Users.AsNoTracking().Join(db.CreatorProfiles.AsNoTracking(), u => u.Id, c => c.UserId, (u, c) => new { u.Id, c.Slug, c.Biography });
        var items = await query.OrderByDescending(x => x.PublishedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new DiscoveryItem(x.Id.ToString(), x.Slug, x.Title, x.Subtitle, x.Summary, x.CoverUrl, x.CountryCode, x.Cities, x.Tags, x.TripDays, x.Lifecycle == GuideLifecycle.Paid ? "paid" : "free", x.PriceMinorUnits, x.CurrencyCode, authors.Where(a => a.Id == x.OwnerUserId).Select(a => a.Slug).First())).ToListAsync();
        var facets = items.SelectMany(x => x.Tags).GroupBy(t => t, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Take(20).Select(g => new FacetCount(g.Key, g.Count())).ToArray();
        DiscoveryQueries.Add(1, new KeyValuePair<string, object?>("operation", "search"));
        return Results.Ok(new SearchResult(total, page, pageSize, facets, items.ToArray()));
    }

    private static async Task<IResult> GetBySlug(string slug, AppDbContext db)
    {
        var guide = await db.TravelGuides.AsNoTracking().Where(x => x.Slug == slug && (x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid)).Include(x => x.Days.OrderBy(d => d.Position)).ThenInclude(x => x.Nodes.OrderBy(n => n.Position)).Include(x => x.Sections.OrderBy(s => s.Position)).SingleOrDefaultAsync();
        if (guide is null) return Results.NotFound();
        var authorSlug = await db.CreatorProfiles.AsNoTracking().Where(x => x.UserId == guide.OwnerUserId).Select(x => x.Slug).FirstAsync();
        DiscoveryQueries.Add(1, new KeyValuePair<string, object?>("operation", "detail"));
        if (guide.Lifecycle == GuideLifecycle.Paid)
        {
            return Results.Ok(new PublicGuideResponse(guide.Id.ToString(), guide.Slug, guide.Title, guide.Subtitle, guide.Summary, guide.CoverUrl, guide.CountryCode, guide.Cities, guide.Tags, guide.TripDays, "paid", authorSlug, $"/guides/{guide.Slug}", new PurchaseMetadata(guide.PriceMinorUnits ?? 0, guide.CurrencyCode ?? ""), guide.Days.OrderBy(d => d.Position).Select(d => new DayOutline(d.Position, d.Title, d.Nodes.OrderBy(n => n.Position).Select(n => new NodeOutline(n.Id, n.Position, n.Type.ToString(), n.Name)).ToArray())).ToArray()));
        }
        return Results.Ok(new PublicGuideResponse(guide.Id.ToString(), guide.Slug, guide.Title, guide.Subtitle, guide.Summary, guide.CoverUrl, guide.CountryCode, guide.Cities, guide.Tags, guide.TripDays, "free", authorSlug, $"/guides/{guide.Slug}", null, guide.Days.OrderBy(d => d.Position).Select(d => new DayOutline(d.Position, d.Title, d.Nodes.OrderBy(n => n.Position).Select(n => new NodeOutline(n.Id, n.Position, n.Type.ToString(), n.Name, n.Address, n.Latitude, n.Longitude, n.ArrivalTime, n.DepartureTime, n.StayMinutes, n.Notes)).ToArray())).ToArray()));
    }

    private static async Task<IResult> Author(string slug, AppDbContext db)
    {
        var creator = await db.CreatorProfiles.AsNoTracking().Where(x => x.Slug == slug && x.Status == CreatorStatus.Active).Select(x => new { x.Slug, x.Biography, x.TravelCountries, DisplayName = db.UserProfiles.Where(p => p.UserId == x.UserId).Select(p => p.DisplayName).FirstOrDefault() }).SingleOrDefaultAsync();
        if (creator is null) return Results.NotFound();
        var published = await db.TravelGuides.AsNoTracking().Where(x => x.Lifecycle == GuideLifecycle.FreePublic || x.Lifecycle == GuideLifecycle.Paid).Where(x => db.CreatorProfiles.Any(c => c.UserId == x.OwnerUserId && c.Slug == slug)).OrderByDescending(x => x.PublishedAt).Select(x => new DiscoveryItem(x.Id.ToString(), x.Slug, x.Title, x.Subtitle, x.Summary, x.CoverUrl, x.CountryCode, x.Cities, x.Tags, x.TripDays, x.Lifecycle == GuideLifecycle.Paid ? "paid" : "free", x.PriceMinorUnits, x.CurrencyCode, slug)).ToListAsync();
        DiscoveryQueries.Add(1, new KeyValuePair<string, object?>("operation", "author"));
        return Results.Ok(new AuthorPage(creator.Slug, creator.DisplayName ?? creator.Slug, creator.Biography, creator.TravelCountries, published.ToArray()));
    }
}

public sealed record FacetCount(string Value, int Count);
public sealed record DiscoveryItem(string Id, string Slug, string Title, string Subtitle, string Summary, string? CoverUrl, string CountryCode, string[] Cities, string[] Tags, int TripDays, string Pricing, long? PriceMinorUnits, string? CurrencyCode, string AuthorSlug);
public sealed record SearchResult(int Total, int Page, int PageSize, FacetCount[] TagFacets, DiscoveryItem[] Items);
public sealed record PurchaseMetadata(long PriceMinorUnits, string CurrencyCode);
public sealed record NodeOutline(Guid Id, int Position, string Type, string Name, string? Address = null, double? Latitude = null, double? Longitude = null, TimeOnly? ArrivalTime = null, TimeOnly? DepartureTime = null, int? StayMinutes = null, string? Notes = null);
public sealed record DayOutline(int Position, string Title, NodeOutline[] Nodes);
public sealed record PublicGuideResponse(string Id, string Slug, string Title, string Subtitle, string Summary, string? CoverUrl, string CountryCode, string[] Cities, string[] Tags, int TripDays, string Pricing, string AuthorSlug, string ShareUrl, PurchaseMetadata? Purchase, DayOutline[] Days);
public sealed record AuthorPage(string Slug, string DisplayName, string Biography, string[] TravelCountries, DiscoveryItem[] Guides);
