using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trippify.Application;

namespace Trippify.Infrastructure;

public sealed class ProviderConfigurationException : InvalidOperationException
{
    public ProviderConfigurationException(string message) : base(message) { }
}

public sealed class MapProviderOptions
{
    public string Provider { get; set; } = "local";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string Attribution { get; set; } = "Local geocoder";
    public int TimeoutMilliseconds { get; set; } = 5000;
    public int CacheRetentionHours { get; set; } = 168;

    public static MapProviderOptions Bind(IConfiguration configuration)
    {
        var options = new MapProviderOptions();
        configuration.GetSection("Map").Bind(options);
        if (string.IsNullOrWhiteSpace(options.Provider)) options.Provider = "local";
        if (string.IsNullOrWhiteSpace(options.Attribution)) options.Attribution = "Local geocoder";
        if (options.TimeoutMilliseconds <= 0) options.TimeoutMilliseconds = 5000;
        if (options.CacheRetentionHours <= 0) options.CacheRetentionHours = 168;
        return options;
    }
}

public sealed class ObjectStorageOptions
{
    public string Provider { get; set; } = "local";
    public string? Endpoint { get; set; }
    public string? Bucket { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string SignedUrlSecret { get; set; } = string.Empty;
    public string LocalRoot { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string SignedUrlHost { get; set; } = string.Empty;
    public int TimeoutMilliseconds { get; set; } = 10000;

    public static ObjectStorageOptions Bind(IConfiguration configuration)
    {
        var options = new ObjectStorageOptions();
        configuration.GetSection("ObjectStorage").Bind(options);
        if (string.IsNullOrWhiteSpace(options.Provider)) options.Provider = "local";
        if (options.TimeoutMilliseconds <= 0) options.TimeoutMilliseconds = 10000;
        if (string.IsNullOrWhiteSpace(options.SignedUrlSecret))
            options.SignedUrlSecret = configuration["ObjectStorage:SignedUrlSecret"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
            options.PublicBaseUrl = configuration["ObjectStorage:PublicBaseUrl"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(options.SignedUrlHost))
            options.SignedUrlHost = configuration["ObjectStorage:SignedUrlHost"] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(options.LocalRoot))
            options.LocalRoot = configuration["ObjectStorage:LocalRoot"] ?? string.Empty;
        return options;
    }
}

public sealed class LocalFileObjectStorage : IObjectStorage
{
    private readonly ObjectStorageOptions _options;
    private readonly ILogger<LocalFileObjectStorage>? _logger;

    public string ProviderName { get; }

    public LocalFileObjectStorage(ObjectStorageOptions options, ILogger<LocalFileObjectStorage>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(options.LocalRoot))
            throw new ProviderConfigurationException("ObjectStorage:LocalRoot must be configured for the local object-storage provider.");
        _options = options;
        _logger = logger;
        ProviderName = "local";
        Directory.CreateDirectory(options.LocalRoot);
    }

    public async Task<Uri> PutAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        var fullPath = ResolveSafePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await using (var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(output, cancellationToken);
        }
        return BuildPublicUri(key);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        var fullPath = ResolveSafePath(key);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        return Task.FromResult(File.Exists(ResolveSafePath(key)));
    }

    public Task<Uri> CreateSignedReadAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        ValidateLifetime(lifetime);
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var signature = SignedUrlSigner.Sign(_options.SignedUrlSecret, key, expiresAt);
        var query = $"expires={Uri.EscapeDataString(expiresAt.ToString("o"))}&signature={Uri.EscapeDataString(signature)}";
        return Task.FromResult(new Uri($"{BuildPublicUri(key)}?{query}"));
    }

    public Stream OpenRead(string key)
    {
        ValidateKey(key);
        return new FileStream(ResolveSafePath(key), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
    }

    private string ResolveSafePath(string key)
    {
        var safe = key.Replace('\\', '/');
        if (safe.Contains("..", StringComparison.Ordinal)) throw new ProviderConfigurationException("Object keys may not contain '..'.");
        var fullPath = Path.GetFullPath(Path.Combine(_options.LocalRoot, safe));
        var rootFull = Path.GetFullPath(_options.LocalRoot) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootFull, StringComparison.Ordinal))
            throw new ProviderConfigurationException("Object key escapes the storage root.");
        return fullPath;
    }

    private Uri BuildPublicUri(string key)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            var baseUri = new Uri(_options.PublicBaseUrl);
            return new Uri(baseUri, key);
        }
        return new Uri($"local://{Uri.EscapeDataString(key)}");
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Object storage key is required.", nameof(key));
        if (key.Length > 500) throw new ArgumentException("Object storage key exceeds 500 characters.", nameof(key));
        if (key.Contains("..", StringComparison.Ordinal)) throw new ArgumentException("Object storage key may not contain '..'.", nameof(key));
    }

    private static void ValidateLifetime(TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(24))
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Signed URL lifetime must be 0 < lifetime <= 24h.");
    }
}

public sealed class RemoteHttpObjectStorage : IObjectStorage
{
    private readonly ObjectStorageOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<RemoteHttpObjectStorage>? _logger;

    public string ProviderName => "s3-compatible";

    public RemoteHttpObjectStorage(ObjectStorageOptions options, HttpClient http, ILogger<RemoteHttpObjectStorage>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            throw new ProviderConfigurationException("ObjectStorage:Endpoint must be configured for the remote object-storage provider.");
        if (string.IsNullOrWhiteSpace(options.Bucket))
            throw new ProviderConfigurationException("ObjectStorage:Bucket must be configured for the remote object-storage provider.");
        if (string.IsNullOrWhiteSpace(options.AccessKey) || string.IsNullOrWhiteSpace(options.SecretKey))
            throw new ProviderConfigurationException("ObjectStorage:AccessKey and ObjectStorage:SecretKey must be configured for the remote object-storage provider.");
        _options = options;
        _http = http;
        _logger = logger;
        _http.BaseAddress ??= new Uri(options.Endpoint);
        _http.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);
    }

    public async Task<Uri> PutAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        var url = BuildObjectUrl(key);
        using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = new StreamContent(content) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "REDACTED");
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogWarning("Remote object storage PUT failed: {Status}", (int)response.StatusCode);
            throw new IOException($"Remote object storage PUT failed with status {(int)response.StatusCode}.");
        }
        return BuildPublicUri(key);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        var url = BuildObjectUrl(key);
        using var response = await _http.DeleteAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            _logger?.LogWarning("Remote object storage DELETE failed: {Status}", (int)response.StatusCode);
            throw new IOException($"Remote object storage DELETE failed with status {(int)response.StatusCode}.");
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        var url = BuildObjectUrl(key);
        using var response = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, url), cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public Task<Uri> CreateSignedReadAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ValidateKey(key);
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(24))
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Signed URL lifetime must be 0 < lifetime <= 24h.");
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        var signature = SignedUrlSigner.Sign(_options.SignedUrlSecret, key, expiresAt);
        var builder = new UriBuilder(BuildPublicUri(key)) { Host = string.IsNullOrWhiteSpace(_options.SignedUrlHost) ? _http.BaseAddress!.Host : _options.SignedUrlHost };
        var query = $"expires={Uri.EscapeDataString(expiresAt.ToString("o"))}&signature={Uri.EscapeDataString(signature)}";
        return Task.FromResult(new Uri($"{builder.Uri}?{query}"));
    }

    private Uri BuildObjectUrl(string key) => new(_http.BaseAddress!, $"{_options.Bucket}/{Uri.EscapeDataString(key)}");

    private Uri BuildPublicUri(string key)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            var baseUri = new Uri(_options.PublicBaseUrl);
            return new Uri(baseUri, $"{_options.Bucket}/{key}");
        }
        return new Uri(_http.BaseAddress!, $"{_options.Bucket}/{Uri.EscapeDataString(key)}");
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Object storage key is required.", nameof(key));
        if (key.Length > 500) throw new ArgumentException("Object storage key exceeds 500 characters.", nameof(key));
        if (key.Contains("..", StringComparison.Ordinal)) throw new ArgumentException("Object storage key may not contain '..'.", nameof(key));
    }
}

public static class SignedUrlSigner
{
    public static string Sign(string secret, string key, DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ProviderConfigurationException("ObjectStorage:SignedUrlSecret must be configured to issue signed URLs.");
        var payload = $"{key}|{expiresAt.ToUnixTimeSeconds()}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(string secret, string key, DateTimeOffset expiresAt, string signature)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature)) return false;
        var expected = Sign(secret, key, expiresAt);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature.ToLowerInvariant()));
    }
}

public sealed class LocalMapProvider : IMapProvider
{
    private readonly MapProviderOptions _options;
    private readonly ILogger<LocalMapProvider>? _logger;

    public string ProviderName => "local";

    public LocalMapProvider(MapProviderOptions options, ILogger<LocalMapProvider>? logger = null)
    {
        _options = options;
        _logger = logger;
    }

    public Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address)) return Task.FromResult(GeocodeResult.Unresolved(ProviderName, _options.Attribution));
        var normalized = address.Trim();
        if (LocalCatalog.TryMatch(normalized, out var entry))
            return Task.FromResult(GeocodeResult.Resolved(entry.Latitude, entry.Longitude, ProviderName, _options.Attribution, entry.PlaceId));
        _logger?.LogDebug("Local geocoder has no match for {Query}.", normalized);
        return Task.FromResult(GeocodeResult.Unresolved(ProviderName, _options.Attribution));
    }

    private static class LocalCatalog
    {
        private static readonly Dictionary<string, Entry> Catalog = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tokyo"] = new("Tokyo", 35.6762, 139.6503, "local-tokyo"),
            ["Osaka"] = new("Osaka", 34.6937, 135.5023, "local-osaka"),
            ["Kyoto"] = new("Kyoto", 35.0116, 135.7681, "local-kyoto"),
            ["Yokohama"] = new("Yokohama", 35.4437, 139.6380, "local-yokohama"),
            ["Nara"] = new("Nara", 34.6851, 135.8048, "local-nara"),
            ["Hiroshima"] = new("Hiroshima", 34.3853, 132.4553, "local-hiroshima"),
            ["Sapporo"] = new("Sapporo", 43.0618, 141.3545, "local-sapporo"),
            ["Fukuoka"] = new("Fukuoka", 33.5904, 130.4017, "local-fukuoka"),
            ["Paris"] = new("Paris", 48.8566, 2.3522, "local-paris"),
            ["Lyon"] = new("Lyon", 45.7640, 4.8357, "local-lyon"),
            ["Marseille"] = new("Marseille", 43.2965, 5.3698, "local-marseille"),
            ["Bordeaux"] = new("Bordeaux", 44.8378, -0.5792, "local-bordeaux"),
            ["New York"] = new("New York", 40.7128, -74.0060, "local-newyork"),
            ["Brooklyn"] = new("Brooklyn", 40.6782, -73.9442, "local-brooklyn"),
            ["San Francisco"] = new("San Francisco", 37.7749, -122.4194, "local-sanfrancisco"),
            ["Los Angeles"] = new("Los Angeles", 34.0522, -118.2437, "local-losangeles"),
            ["London"] = new("London", 51.5074, -0.1278, "local-london"),
            ["Edinburgh"] = new("Edinburgh", 55.9533, -3.1883, "local-edinburgh"),
            ["Berlin"] = new("Berlin", 52.5200, 13.4050, "local-berlin"),
            ["Munich"] = new("Munich", 48.1351, 11.5820, "local-munich"),
            ["Rome"] = new("Rome", 41.9028, 12.4964, "local-rome"),
            ["Milan"] = new("Milan", 45.4642, 9.1900, "local-milan"),
            ["Barcelona"] = new("Barcelona", 41.3851, 2.1734, "local-barcelona"),
            ["Madrid"] = new("Madrid", 40.4168, -3.7038, "local-madrid"),
            ["Lisbon"] = new("Lisbon", 38.7223, -9.1393, "local-lisbon"),
            ["Sydney"] = new("Sydney", -33.8688, 151.2093, "local-sydney"),
            ["Melbourne"] = new("Melbourne", -37.8136, 144.9631, "local-melbourne"),
            ["Singapore"] = new("Singapore", 1.3521, 103.8198, "local-singapore"),
            ["Bangkok"] = new("Bangkok", 13.7563, 100.5018, "local-bangkok"),
            ["Seoul"] = new("Seoul", 37.5665, 126.9780, "local-seoul"),
            ["Busan"] = new("Busan", 35.1796, 129.0756, "local-busan"),
            ["Beijing"] = new("Beijing", 39.9042, 116.4074, "local-beijing"),
            ["Shanghai"] = new("Shanghai", 31.2304, 121.4737, "local-shanghai"),
            ["Hong Kong"] = new("Hong Kong", 22.3193, 114.1694, "local-hongkong"),
            ["Taipei"] = new("Taipei", 25.0330, 121.5654, "local-taipei"),
        };

        public static bool TryMatch(string query, out Entry entry)
        {
            var canonical = query.Trim();
            if (Catalog.TryGetValue(canonical, out var match)) { entry = match; return true; }
            var tokens = canonical.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length > 0 && Catalog.TryGetValue(tokens[0], out match)) { entry = match; return true; }
            foreach (var (key, value) in Catalog)
            {
                if (canonical.Contains(key, StringComparison.OrdinalIgnoreCase)) { entry = value; return true; }
            }
            entry = default!;
            return false;
        }

        public readonly record struct Entry(string Name, double Latitude, double Longitude, string PlaceId);
    }
}

public sealed class HttpMapProvider : IMapProvider
{
    private readonly MapProviderOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<HttpMapProvider>? _logger;

    public string ProviderName => _options.Provider;

    public HttpMapProvider(MapProviderOptions options, HttpClient http, ILogger<HttpMapProvider>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            throw new ProviderConfigurationException("Map:Endpoint must be configured for the remote map provider.");
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ProviderConfigurationException("Map:ApiKey must be configured for the remote map provider.");
        _options = options;
        _http = http;
        _logger = logger;
        _http.BaseAddress ??= new Uri(options.Endpoint);
        _http.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);
    }

    public async Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address)) return GeocodeResult.Unresolved(ProviderName, _options.Attribution);
        var url = $"/geocode?q={Uri.EscapeDataString(address)}&access_token={Uri.EscapeDataString(_options.ApiKey!)}&limit=1";
        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Remote geocoder returned {Status}", (int)response.StatusCode);
                throw new IOException($"Remote geocoder returned status {(int)response.StatusCode}.");
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
                return GeocodeResult.Unresolved(ProviderName, _options.Attribution);
            var first = features[0];
            if (!first.TryGetProperty("center", out var center) || center.GetArrayLength() < 2)
                return GeocodeResult.Unresolved(ProviderName, _options.Attribution);
            var longitude = center[0].GetDouble();
            var latitude = center[1].GetDouble();
            var placeId = first.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            return GeocodeResult.Resolved(latitude, longitude, ProviderName, _options.Attribution, placeId);
        }
        catch (JsonException)
        {
            _logger?.LogWarning("Remote geocoder returned malformed JSON.");
            throw new IOException("Remote geocoder returned malformed JSON.");
        }
    }
}

public sealed class CachedMapProvider : IMapProvider
{
    private readonly IMapProvider _inner;
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    private readonly TimeSpan _retention;
    private readonly ILogger<CachedMapProvider>? _logger;

    public string ProviderName => _inner.ProviderName;

    public CachedMapProvider(IMapProvider inner, AppDbContext db, IClock clock, MapProviderOptions options, ILogger<CachedMapProvider>? logger = null)
    {
        _inner = inner;
        _db = db;
        _clock = clock;
        _retention = TimeSpan.FromHours(options.CacheRetentionHours);
        _logger = logger;
    }

    public async Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address)) return GeocodeResult.Unresolved(_inner.ProviderName, "Local geocoder");
        var normalized = Normalize(address);
        var hash = ComputeHash(normalized);
        var now = _clock.UtcNow;
        var cached = await _db.GeocodeCache.AsNoTracking().FirstOrDefaultAsync(x => x.QueryHash == hash && x.ExpiresAt > now, cancellationToken);
        if (cached is not null)
        {
            _logger?.LogDebug("Geocode cache hit for {Query}", normalized);
            return cached.Status switch
            {
                GeocodeResolutionStatus.Resolved when cached.Latitude.HasValue && cached.Longitude.HasValue =>
                    GeocodeResult.Resolved(cached.Latitude.Value, cached.Longitude.Value, cached.ProviderName, cached.ProviderAttribution, cached.ProviderPlaceId),
                GeocodeResolutionStatus.Unresolved =>
                    GeocodeResult.Unresolved(cached.ProviderName, cached.ProviderAttribution),
                _ => await ResolveAndStoreAsync(normalized, hash, cancellationToken),
            };
        }
        return await ResolveAndStoreAsync(normalized, hash, cancellationToken);
    }

    private async Task<GeocodeResult> ResolveAndStoreAsync(string normalized, string hash, CancellationToken cancellationToken)
    {
        var result = await _inner.GeocodeAsync(normalized, cancellationToken);
        var now = _clock.UtcNow;
        _db.GeocodeCache.Add(new GeocodeCache
        {
            Id = Guid.NewGuid(),
            QueryHash = hash,
            OriginalQuery = normalized,
            Status = result.Status == GeocodeStatus.Resolved ? GeocodeResolutionStatus.Resolved : GeocodeResolutionStatus.Unresolved,
            Latitude = result.Latitude,
            Longitude = result.Longitude,
            ProviderName = result.ProviderName,
            ProviderAttribution = result.ProviderAttribution,
            ProviderPlaceId = result.ProviderPlaceId,
            ResolvedAt = now,
            ExpiresAt = now.Add(_retention),
        });
        await _db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public static string Normalize(string address)
    {
        var lowered = address.Trim().ToLowerInvariant();
        var builder = new StringBuilder(lowered.Length);
        var previousWhitespace = false;
        foreach (var ch in lowered)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWhitespace) builder.Append(' ');
                previousWhitespace = true;
            }
            else
            {
                builder.Append(ch);
                previousWhitespace = false;
            }
        }
        return builder.ToString().Trim(',', ' ', '.');
    }

    public static string ComputeHash(string normalized)
    {
        var bytes = Encoding.UTF8.GetBytes(normalized);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}

internal static class ProviderRegistration
{
    public static IMapProvider BuildMapProvider(IServiceProvider services, MapProviderOptions options)
    {
        IMapProvider inner = options.Provider.Equals("local", StringComparison.OrdinalIgnoreCase)
            ? ActivatorUtilities.CreateInstance<LocalMapProvider>(services, options)
            : ActivatorUtilities.CreateInstance<HttpMapProvider>(services, options);
        return ActivatorUtilities.CreateInstance<CachedMapProvider>(services, inner, options);
    }

    public static IObjectStorage BuildObjectStorage(IServiceProvider services, ObjectStorageOptions options)
    {
        return options.Provider.Equals("local", StringComparison.OrdinalIgnoreCase)
            ? ActivatorUtilities.CreateInstance<LocalFileObjectStorage>(services, options)
            : ActivatorUtilities.CreateInstance<RemoteHttpObjectStorage>(services, options);
    }
}
