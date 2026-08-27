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
        group.MapPost("/{guideId:guid}/publish", Publish);
        group.MapPost("/{guideId:guid}/unpublish", Unpublish);
        group.MapDelete("/{guideId:guid}", Delete);
    }

    private static async Task<IResult> ListMine(ClaimsPrincipal principal, AppDbContext db)
    {
        var owner = IdentityEndpoints.CurrentUserId(principal);
        var guides = await db.TravelGuides.AsNoTracking().Where(x => x.OwnerUserId == owner).OrderByDescending(x => x.UpdatedAt).Select(x => new GuideListItem(x.Id, x.Title, x.CountryCode, x.TripDays, x.Lifecycle.ToString(), x.ConcurrencyToken, x.UpdatedAt)).ToListAsync();
        return Results.Ok(guides);
    }

    private static async Task<IResult> Create(GuideMetadataRequest request, HttpRequest httpRequest, ClaimsPrincipal principal, AppDbContext db, IClock clock, IQuotaService quotas)
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
        var reservation = await quotas.ReserveAsync(owner, QuotaMetrics.Guides, 1);
        if (!reservation.Succeeded) return QuotaProblem.From(reservation);
        var now = clock.UtcNow;
        var guide = new TravelGuide { Id = Guid.NewGuid(), OwnerUserId = owner, Title = request.Title.Trim(), Subtitle = request.Subtitle.Trim(), Summary = request.Summary.Trim(), CoverUrl = request.CoverUrl, CountryCode = request.CountryCode.Trim().ToUpperInvariant(), Cities = Normalize(request.Cities, 50), Tags = Normalize(request.Tags, 50), TripDays = request.TripDays, Slug = await UniqueSlug(db, request.Title), CreatedAt = now, UpdatedAt = now };
        db.TravelGuides.Add(guide); db.GuideAuditEntries.Add(Audit(guide.Id, owner, "created", now));
        if (key.Length > 0) db.GuideCommandReceipts.Add(new GuideCommandReceipt { OwnerUserId = owner, IdempotencyKey = key, Operation = "create", ResourceId = guide.Id, CreatedAt = now });
        try { await db.SaveChangesAsync(); await quotas.FinalizeAsync(reservation.ReservationId!.Value); }
        catch { await quotas.ReleaseAsync(reservation.ReservationId!.Value, "guide-create-failed"); throw; }
        GuideCommands.Add(1, new KeyValuePair<string, object?>("operation", "create"));
        return Results.Created($"/api/v1/guides/{guide.Id}", new { guide.Id, guide.Slug, guide.ConcurrencyToken });
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

    private static async Task<IResult> ReplaceStructure(Guid guideId, GuideStructureRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock, IMapProvider map)
    {
        if (request.Days.Count > 60 || request.Sections.Count > 50 || request.Days.Any(x => x.Nodes.Count > 100)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["structure"] = ["Guide structure exceeds supported limits."] });
        var guide = await OwnedGuide(guideId, principal, db).Include(x => x.Days).ThenInclude(x => x.Nodes).Include(x => x.Sections).SingleOrDefaultAsync(); if (guide is null) return Results.NotFound();
        if (guide.ConcurrencyToken != request.ConcurrencyToken) return Conflict(guide.ConcurrencyToken);
        db.GuideNodes.RemoveRange(guide.Days.SelectMany(x => x.Nodes)); db.GuideDays.RemoveRange(guide.Days); db.GuideSections.RemoveRange(guide.Sections);
        for (var dayPosition = 0; dayPosition < request.Days.Count; dayPosition++)
        {
            var source = request.Days[dayPosition];
            var day = new GuideDay { Id = Guid.NewGuid(), GuideId = guide.Id, Position = dayPosition, Title = source.Title.Trim(), Notes = source.Notes.Trim() };
            db.GuideDays.Add(day);
            for (var nodePosition = 0; nodePosition < source.Nodes.Count; nodePosition++)
            {
                var node = source.Nodes[nodePosition];
                if (!Enum.TryParse<GuideNodeType>(node.Type, true, out var type)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["nodeType"] = [$"Unknown node type: {node.Type}"] });
                if (!CoordinatesValid(node.Latitude, node.Longitude)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["coordinates"] = ["Latitude and longitude are invalid."] });
                var (geocodeStatus, lat, lng, providerName, attribution, placeId, resolvedQuery) = await ResolveLocationAsync(node, map);
                db.GuideNodes.Add(new GuideNode
                {
                    Id = Guid.NewGuid(),
                    DayId = day.Id,
                    Position = nodePosition,
                    Type = type,
                    Name = node.Name.Trim(),
                    Address = node.Address,
                    Latitude = lat,
                    Longitude = lng,
                    ArrivalTime = node.ArrivalTime,
                    DepartureTime = node.DepartureTime,
                    StayMinutes = node.StayMinutes,
                    TicketInformation = node.TicketInformation,
                    ReservationInformation = node.ReservationInformation,
                    OpeningHours = node.OpeningHours,
                    Notes = node.Notes.Trim(),
                    GeocodeStatus = geocodeStatus,
                    ResolvedQuery = resolvedQuery,
                    GeocodeProviderName = providerName,
                    GeocodeProviderAttribution = attribution,
                    GeocodeProviderPlaceId = placeId,
                });
            }
        }
        for (var position = 0; position < request.Sections.Count; position++)
        {
            var source = request.Sections[position];
            if (!Enum.TryParse<GuideSectionType>(source.Type, true, out var type)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["sectionType"] = [$"Unknown section type: {source.Type}"] });
            db.GuideSections.Add(new GuideSection { Id = Guid.NewGuid(), GuideId = guide.Id, Position = position, Type = type, Title = source.Title.Trim(), Body = source.Body.Trim() });
        }
        guide.TripDays = request.Days.Count; Touch(guide, clock.UtcNow); db.GuideAuditEntries.Add(Audit(guide.Id, guide.OwnerUserId, "structure-replaced", guide.UpdatedAt)); await db.SaveChangesAsync(); GuideCommands.Add(1, new KeyValuePair<string, object?>("operation", "structure-replaced")); return Results.Ok(new { guide.ConcurrencyToken });
    }

    private static async Task<(GeocodeResolutionStatus Status, double? Latitude, double? Longitude, string? ProviderName, string? Attribution, string? PlaceId, string? ResolvedQuery)> ResolveLocationAsync(NodeRequest node, IMapProvider map)
    {
        if (node.Latitude.HasValue && node.Longitude.HasValue)
            return (GeocodeResolutionStatus.Manual, node.Latitude, node.Longitude, null, null, null, null);
        var address = string.IsNullOrWhiteSpace(node.Address) ? node.Name?.Trim() : node.Address?.Trim();
        if (string.IsNullOrWhiteSpace(address)) return (GeocodeResolutionStatus.Unresolved, null, null, null, null, null, null);
        try
        {
            var result = await map.GeocodeAsync(address, CancellationToken.None);
            return result.Status switch
            {
                GeocodeStatus.Resolved when result.Latitude.HasValue && result.Longitude.HasValue =>
                    (GeocodeResolutionStatus.Resolved, result.Latitude, result.Longitude, result.ProviderName, result.ProviderAttribution, result.ProviderPlaceId, address),
                _ => (GeocodeResolutionStatus.Unresolved, null, null, result.ProviderName, result.ProviderAttribution, null, address),
            };
        }
        catch (Exception)
        {
            return (GeocodeResolutionStatus.Unresolved, null, null, null, null, null, address);
        }
    }

    private static async Task<IResult> AddMedia(Guid guideId, MediaRequest request, ClaimsPrincipal principal, AppDbContext db, IObjectStorage storage, IClock clock, IQuotaService quotas)
    {
        var guide = await OwnedGuide(guideId, principal, db).SingleOrDefaultAsync(); if (guide is null) return Results.NotFound(); if (guide.ConcurrencyToken != request.ConcurrencyToken) return Conflict(guide.ConcurrencyToken);
        if (string.IsNullOrWhiteSpace(request.StorageKey)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["storageKey"] = ["Storage key is required."] });
        if (request.StorageKey.Length > 500 || request.StorageKey.Contains("..", StringComparison.Ordinal) || !request.StorageKey.StartsWith("guides/", StringComparison.OrdinalIgnoreCase)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["storageKey"] = ["Storage key must start with 'guides/' and not contain '..'."] });
        byte[] bytes; try { bytes = Convert.FromBase64String(request.ContentBase64); } catch (FormatException) { return Results.ValidationProblem(new Dictionary<string, string[]> { ["contentBase64"] = ["Media content is not valid base64."] }); }
        if (bytes.LongLength is 0 or > MediaRules.MaxBytes) return Results.ValidationProblem(new Dictionary<string, string[]> { ["contentBase64"] = [$"Media must contain 1 byte to {MediaRules.MaxBytes / 1_000_000L} MB."] });
        var headLen = Math.Min(16, bytes.Length);
        var head = new byte[headLen];
        Array.Copy(bytes, 0, head, 0, headLen);
        var (signatureOk, signatureFailure, contentType, extension) = MediaRules.Validate(request.ContentType, head);
        if (!signatureOk) return Results.ValidationProblem(new Dictionary<string, string[]> { ["contentType"] = [$"Media rejected: {signatureFailure}."] });
        var normalizedKey = extension.Length > 0 && !request.StorageKey.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? $"{request.StorageKey.TrimEnd('.')}{extension}"
            : request.StorageKey;
        var sha = MediaRules.ComputeSha256Hex(bytes);
        var visibility = string.Equals(request.Visibility, "Public", StringComparison.OrdinalIgnoreCase) ? GuideMediaVisibility.Public : GuideMediaVisibility.Private;
        var totalBytes = await db.GuideMedia.Where(x => x.GuideId == guide.Id && x.StorageKey != request.StorageKey).SumAsync(x => (long?)x.SizeBytes) ?? 0L;
        if (totalBytes + bytes.LongLength > MediaRules.MaxTotalBytesPerGuide) return Results.ValidationProblem(new Dictionary<string, string[]> { ["contentBase64"] = ["Guide media exceeds the per-guide quota."] });
        if (await db.GuideMedia.AnyAsync(x => x.GuideId == guide.Id && x.StorageKey == normalizedKey)) return Results.Conflict(new Dictionary<string, string[]> { ["storageKey"] = ["Storage key is already in use for this guide."] });
        var reservation = await quotas.ReserveAsync(guide.OwnerUserId, QuotaMetrics.MediaMegabytes, Math.Max(1, (int)Math.Ceiling(bytes.LongLength / 1_000_000d)));
        if (!reservation.Succeeded) return QuotaProblem.From(reservation);
        Uri rawUri;
        try
        {
            await using var stream = new MemoryStream(bytes);
            rawUri = await storage.PutAsync(normalizedKey, stream, default);
        }
        catch (Exception error) when (error is IOException or NotSupportedException)
        {
            await quotas.ReleaseAsync(reservation.ReservationId!.Value, "media-storage-failed");
            return Results.Problem("Media storage is unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        var providerName = (storage as Trippify.Infrastructure.LocalFileObjectStorage)?.ProviderName ?? (storage as Trippify.Infrastructure.RemoteHttpObjectStorage)?.ProviderName ?? "unknown";
        var media = new GuideMedia
        {
            Id = Guid.NewGuid(),
            GuideId = guide.Id,
            Position = await db.GuideMedia.CountAsync(x => x.GuideId == guide.Id),
            StorageKey = normalizedKey,
            Url = rawUri.ToString(),
            Caption = request.Caption,
            ContentType = contentType,
            SizeBytes = bytes.LongLength,
            Sha256 = sha,
            Visibility = visibility,
            StorageProvider = providerName,
            UploadedAt = clock.UtcNow,
        };
        db.GuideMedia.Add(media); Touch(guide, clock.UtcNow);
        await db.SaveChangesAsync();
        await quotas.FinalizeAsync(reservation.ReservationId!.Value);
        var accessUrl = visibility == GuideMediaVisibility.Private
            ? await storage.CreateSignedReadAsync(normalizedKey, MediaRules.SignedReadLifetime, default)
            : rawUri;
        return Results.Ok(new GuideMediaResponse(media.Id, media.Position, media.Url, accessUrl, media.ContentType, media.SizeBytes, media.Sha256, media.Visibility.ToString(), media.StorageProvider, media.Caption, media.UploadedAt, clock.UtcNow.Add(MediaRules.SignedReadLifetime)));
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

    private static async Task<IResult> Publish(Guid guideId, PublishRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock, BackgroundJobProcessor processor)
    {
        var guide = await OwnedGuide(guideId, principal, db).Include(x => x.Days).ThenInclude(x => x.Nodes).SingleOrDefaultAsync(); if (guide is null) return Results.NotFound();
        if (guide.ConcurrencyToken != request.ConcurrencyToken) return Conflict(guide.ConcurrencyToken);
        var errors = ValidateReadiness(guide); if (request.Pricing is { } pricing && (pricing.PriceMinorUnits is < 1 or > 1_000_000_000 || pricing.CurrencyCode.Trim().Length != 3)) errors["pricing"] = ["Paid guides require a positive price and three-letter currency."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var now = clock.UtcNow;
        guide.Lifecycle = request.Pricing is null ? GuideLifecycle.FreePublic : GuideLifecycle.Paid;
        guide.PriceMinorUnits = request.Pricing?.PriceMinorUnits; guide.CurrencyCode = request.Pricing is null ? null : request.Pricing.CurrencyCode.Trim().ToUpperInvariant();
        guide.PublishedAt = now; Touch(guide, now);
        db.GuideAuditEntries.Add(Audit(guide.Id, guide.OwnerUserId, "published:" + guide.Lifecycle, now));
        await db.SaveChangesAsync();
        await VersioningEndpoints.CreateInitialReleaseAsync(db, guide, guide.OwnerUserId, clock, processor);
        GuideCommands.Add(1, new KeyValuePair<string, object?>("operation", "publish"));
        return Results.Ok(new { guide.ConcurrencyToken, guide.Slug, lifecycle = guide.Lifecycle.ToString() });
    }

    private static async Task<IResult> Unpublish(Guid guideId, [FromBody] ConcurrencyRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var guide = await OwnedGuide(guideId, principal, db).SingleOrDefaultAsync(); if (guide is null) return Results.NotFound();
        if (guide.ConcurrencyToken != request.ConcurrencyToken) return Conflict(guide.ConcurrencyToken);
        if (guide.Lifecycle is not (GuideLifecycle.FreePublic or GuideLifecycle.Paid or GuideLifecycle.Unlisted)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["lifecycle"] = ["Only published guides can be unpublished."] });
        guide.Lifecycle = GuideLifecycle.Private; guide.PublishedAt = null; Touch(guide, clock.UtcNow);
        db.GuideAuditEntries.Add(Audit(guide.Id, guide.OwnerUserId, "unpublished", guide.UpdatedAt));
        await db.SaveChangesAsync(); GuideCommands.Add(1, new KeyValuePair<string, object?>("operation", "unpublish"));
        return Results.Ok(new { guide.ConcurrencyToken });
    }

    private static Dictionary<string, string[]> ValidateReadiness(TravelGuide guide)
    {
        var errors = new Dictionary<string, string[]>();
        if (guide.Summary.Trim().Length == 0) errors["summary"] = ["A summary is required before publishing."];
        if (guide.Days.Count == 0) errors["days"] = ["At least one day is required before publishing."];
        else if (guide.Days.Any(x => x.Nodes.Count == 0)) errors["days"] = ["Every day needs at least one place before publishing."];
        return errors;
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
    private static IQueryable<TravelGuide> OwnedGuide(Guid id, ClaimsPrincipal principal, AppDbContext db) { var owner = IdentityEndpoints.CurrentUserId(principal); return db.TravelGuides.Where(x => x.Id == id && x.OwnerUserId == owner); }
    private static Dictionary<string, string[]> ValidateMetadata(GuideMetadataRequest request) { var errors = new Dictionary<string, string[]>(); if (request.Title.Trim().Length is < 3 or > 160) errors["title"] = ["Title must contain 3 to 160 characters."]; if (request.CountryCode.Trim().Length != 2) errors["countryCode"] = ["Country code must contain two characters."]; if (request.TripDays is < 0 or > 60) errors["tripDays"] = ["Trip days must be between 0 and 60."]; return errors; }
    private static bool CoordinatesValid(double? latitude, double? longitude) => latitude is null && longitude is null || latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
    private static string[] Normalize(IEnumerable<string> values, int limit) => values.Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(limit).ToArray();
    private static void Touch(TravelGuide guide, DateTimeOffset now) { guide.UpdatedAt = now; guide.ConcurrencyToken = Guid.NewGuid(); }
    private static GuideAuditEntry Audit(Guid guide, Guid actor, string action, DateTimeOffset now) => new() { Id = Guid.NewGuid(), GuideId = guide, ActorUserId = actor, Action = action, OccurredAt = now };
    private static IResult Conflict(Guid token) => Results.Conflict(new { detail = "The guide changed since it was loaded.", concurrencyToken = token });
    private static GuideDetail Project(TravelGuide x) => new(x.Id, x.Title, x.Subtitle, x.Summary, x.CoverUrl, x.CountryCode, x.Cities, x.Tags, x.TripDays, x.Lifecycle.ToString(), x.ConcurrencyToken, x.Days.OrderBy(d => d.Position).Select(d => new DayResponse(d.Id, d.Position, d.Title, d.Notes, d.Nodes.OrderBy(n => n.Position).Select(n => new NodeResponse(n.Id, n.Position, n.Type.ToString(), n.Name, n.Address, n.Latitude, n.Longitude, n.ArrivalTime, n.DepartureTime, n.StayMinutes, n.Notes, n.GeocodeStatus.ToString(), n.GeocodeProviderAttribution)).ToArray())).ToArray(), x.Sections.OrderBy(s => s.Position).Select(s => new SectionResponse(s.Id, s.Position, s.Type.ToString(), s.Title, s.Body)).ToArray(), x.Media.OrderBy(m => m.Position).Select(m => new MediaResponse(m.Id, m.Position, m.Url, m.Caption)).ToArray());
}

public sealed record GuideMetadataRequest(string Title, string Subtitle, string Summary, string? CoverUrl, string CountryCode, string[] Cities, string[] Tags, int TripDays, Guid? ConcurrencyToken = null);
public sealed record PricingRequest(long PriceMinorUnits, string CurrencyCode);
public sealed record PublishRequest(Guid ConcurrencyToken, PricingRequest? Pricing = null);
public sealed record GuideStructureRequest(Guid ConcurrencyToken, List<DayRequest> Days, List<SectionRequest> Sections);
public sealed record DayRequest(string Title, string Notes, List<NodeRequest> Nodes);
public sealed record NodeRequest(string Type, string Name, string? Address, double? Latitude, double? Longitude, TimeOnly? ArrivalTime, TimeOnly? DepartureTime, int? StayMinutes, string? TicketInformation, string? ReservationInformation, string? OpeningHours, string Notes);
public sealed record SectionRequest(string Type, string Title, string Body);
public sealed record MediaRequest(Guid ConcurrencyToken, string StorageKey, string ContentBase64, string ContentType, string? Caption, string? Visibility = null);
public sealed record LifecycleRequest(Guid ConcurrencyToken, string Lifecycle);
public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
public sealed record GuideListItem(Guid Id, string Title, string CountryCode, int TripDays, string Lifecycle, Guid ConcurrencyToken, DateTimeOffset UpdatedAt);
public sealed record GuideDetail(Guid Id, string Title, string Subtitle, string Summary, string? CoverUrl, string CountryCode, string[] Cities, string[] Tags, int TripDays, string Lifecycle, Guid ConcurrencyToken, DayResponse[] Days, SectionResponse[] Sections, MediaResponse[] Media);
public sealed record DayResponse(Guid Id, int Position, string Title, string Notes, NodeResponse[] Nodes);
public sealed record NodeResponse(Guid Id, int Position, string Type, string Name, string? Address, double? Latitude, double? Longitude, TimeOnly? ArrivalTime, TimeOnly? DepartureTime, int? StayMinutes, string Notes, string GeocodeStatus = "Manual", string? GeocodeAttribution = null);
public sealed record SectionResponse(Guid Id, int Position, string Type, string Title, string Body);
public sealed record MediaResponse(Guid Id, int Position, string Url, string? Caption);
public sealed record GuideMediaResponse(Guid Id, int Position, string Url, Uri AccessUrl, string ContentType, long SizeBytes, string Sha256, string Visibility, string StorageProvider, string? Caption, DateTimeOffset UploadedAt, DateTimeOffset AccessExpiresAt);
