using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trippify.Application;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class MapAndObjectStorageApiTests : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";
    private readonly TrippifyFactory _factory;

    public MapAndObjectStorageApiTests(TrippifyFactory factory) { _factory = factory; }

    [Fact]
    public async Task Media_upload_persists_bytes_to_object_storage_and_returns_signed_url_for_private_objects()
    {
        using var client = _factory.CreateClient();
        var creator = await CreateCreator("map-store-private@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, creator.Email!));
        var created = await client.PostAsJsonAsync("/api/v1/guides", Metadata("Persisted guide"));
        created.EnsureSuccessStatusCode();
        var json = await Json(created);
        var id = json.GetProperty("id").GetGuid();
        var token = json.GetProperty("concurrencyToken").GetGuid();

        var jpegBytes = MakeJpegBytes(2048);
        var sha = Convert.ToHexString(SHA256.HashData(jpegBytes)).ToLowerInvariant();
        var response = await client.PostAsJsonAsync($"/api/v1/guides/{id}/media", new
        {
            concurrencyToken = token,
            storageKey = "guides/persisted.jpg",
            contentBase64 = Convert.ToBase64String(jpegBytes),
            contentType = "image/jpeg",
            caption = "Persisted photo",
        });
        response.EnsureSuccessStatusCode();
        var upload = await Json(response);
        Assert.Equal("Private", upload.GetProperty("visibility").GetString());
        Assert.Equal(sha, upload.GetProperty("sha256").GetString());
        Assert.Equal(jpegBytes.LongLength, upload.GetProperty("sizeBytes").GetInt64());
        Assert.Equal("local", upload.GetProperty("storageProvider").GetString());
        var accessUrl = upload.GetProperty("accessUrl").GetString()!;
        Assert.Contains("signature=", accessUrl);
        Assert.Contains("expires=", accessUrl);
        Assert.False(string.IsNullOrEmpty(upload.GetProperty("url").GetString()));

        await WithDb(async db =>
        {
            var stored = await db.GuideMedia.SingleAsync(x => x.StorageKey == "guides/persisted.jpg");
            Assert.Equal("image/jpeg", stored.ContentType);
            Assert.Equal(jpegBytes.LongLength, stored.SizeBytes);
            Assert.Equal(sha, stored.Sha256);
            Assert.Equal(GuideMediaVisibility.Private, stored.Visibility);
            Assert.True(stored.UploadedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
        });
    }

    [Fact]
    public async Task Media_upload_rejects_oversized_mismatched_signature_and_duplicate_storage_keys()
    {
        using var client = _factory.CreateClient();
        var creator = await CreateCreator("map-store-validation@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, creator.Email!));
        var created = await client.PostAsJsonAsync("/api/v1/guides", Metadata("Validation guide"));
        created.EnsureSuccessStatusCode();
        var json = await Json(created);
        var id = json.GetProperty("id").GetGuid();
        var token = json.GetProperty("concurrencyToken").GetGuid();

        var pngHead = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var disguised = new byte[2048];
        Array.Copy(pngHead, disguised, pngHead.Length);
        for (var i = pngHead.Length; i < disguised.Length; i++) disguised[i] = 0xCC;
        var disguisedResp = await client.PostAsJsonAsync($"/api/v1/guides/{id}/media", new
        {
            concurrencyToken = token,
            storageKey = "guides/fake.jpg",
            contentBase64 = Convert.ToBase64String(disguised),
            contentType = "image/jpeg",
        });
        Assert.Equal(HttpStatusCode.BadRequest, disguisedResp.StatusCode);

        var oversized = new byte[(int)Trippify.Api.MediaRules.MaxBytes + 1];
        Array.Copy(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, oversized, 4);
        var oversizeResp = await client.PostAsJsonAsync($"/api/v1/guides/{id}/media", new
        {
            concurrencyToken = token,
            storageKey = "guides/huge.jpg",
            contentBase64 = Convert.ToBase64String(oversized),
            contentType = "image/jpeg",
        });
        Assert.Equal(HttpStatusCode.BadRequest, oversizeResp.StatusCode);

        var invalidKeyResp = await client.PostAsJsonAsync($"/api/v1/guides/{id}/media", new
        {
            concurrencyToken = token,
            storageKey = "../escape.jpg",
            contentBase64 = Convert.ToBase64String(MakeJpegBytes(1024)),
            contentType = "image/jpeg",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidKeyResp.StatusCode);

        var validJpeg = MakeJpegBytes(1024);
        var firstResp = await client.PostAsJsonAsync($"/api/v1/guides/{id}/media", new
        {
            concurrencyToken = token,
            storageKey = "guides/dup.jpg",
            contentBase64 = Convert.ToBase64String(validJpeg),
            contentType = "image/jpeg",
        });
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        var detail = await Json(await client.GetAsync($"/api/v1/guides/{id}"));
        var concurrency = detail.GetProperty("concurrencyToken").GetGuid();
        var dupResp = await client.PostAsJsonAsync($"/api/v1/guides/{id}/media", new
        {
            concurrencyToken = concurrency,
            storageKey = "guides/dup.jpg",
            contentBase64 = Convert.ToBase64String(MakeJpegBytes(1024)),
            contentType = "image/jpeg",
        });
        Assert.Equal(HttpStatusCode.Conflict, dupResp.StatusCode);

        await WithDb(db => Task.FromResult(Assert.Single(db.GuideMedia)));
    }

    [Fact]
    public async Task Object_storage_signed_url_is_verifiable_and_other_users_cannot_access_storage_bytes()
    {
        using var ownerClient = _factory.CreateClient();
        var owner = await CreateCreator("map-store-owner@example.com");
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(ownerClient, owner.Email!));
        var created = await ownerClient.PostAsJsonAsync("/api/v1/guides", Metadata("Owner only guide"));
        created.EnsureSuccessStatusCode();
        var createdJson = await Json(created);
        var id = createdJson.GetProperty("id").GetGuid();
        var token = createdJson.GetProperty("concurrencyToken").GetGuid();
        var payload = MakeJpegBytes(4096);
        var uploadResp = await ownerClient.PostAsJsonAsync($"/api/v1/guides/{id}/media", new
        {
            concurrencyToken = token,
            storageKey = "guides/private.jpg",
            contentBase64 = Convert.ToBase64String(payload),
            contentType = "image/jpeg",
        });
        uploadResp.EnsureSuccessStatusCode();
        var uploadJson = await Json(uploadResp);
        var accessUrl = uploadJson.GetProperty("accessUrl").GetString()!;
        var storageKey = "guides/private.jpg";

        var storage = _factory.Services.GetRequiredService<IObjectStorage>();
        Assert.True(await storage.ExistsAsync(storageKey, default));

        var internalUri = new Uri(uploadJson.GetProperty("url").GetString()!);
        Assert.Contains("local://", internalUri.ToString(), StringComparison.OrdinalIgnoreCase);

        var signedQuery = accessUrl[(accessUrl.IndexOf('?') + 1)..];
        var signedExpires = ExtractSignedExpiry(signedQuery);
        var signedSignature = ExtractSignedSignature(signedQuery);
        var configSecret = _factory.Services.GetRequiredService<IConfiguration>()["ObjectStorage:SignedUrlSecret"];
        var manualCheck = SignedUrlSigner.Sign(configSecret ?? "test-secret-do-not-use-in-production", storageKey, signedExpires);
        Assert.True(signedExpires > DateTimeOffset.UtcNow, $"expires must be future but was {signedExpires:o}; accessUrl={accessUrl}");
        Assert.True(SignedUrlSigner.Verify(configSecret ?? "test-secret-do-not-use-in-production", storageKey, signedExpires, signedSignature), $"signature verification failed; configSecret={configSecret}; signedQuery={signedQuery}; signature={signedSignature}; manualCheck={manualCheck}; same={signedSignature == manualCheck}");

        var forgedUrl = BuildForgedSignedUrl(storageKey, signedExpires, "deadbeef");
        Assert.False(SignedUrlSigner.Verify("test-secret-do-not-use-in-production", storageKey, signedExpires, ExtractSignedSignature(forgedUrl[(forgedUrl.IndexOf('?') + 1)..])));

        var expired = signedExpires.AddSeconds(-(Trippify.Api.MediaRules.SignedReadLifetime.TotalSeconds + 60));
        Assert.True(expired < DateTimeOffset.UtcNow);
        Assert.False(expired > DateTimeOffset.UtcNow);

        using var otherClient = _factory.CreateClient();
        var other = await CreateCreator("map-store-other@example.com");
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(otherClient, other.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/v1/guides/{id}")).StatusCode);

        await WithDb(async db =>
        {
            var bytes = await File.ReadAllBytesAsync(Path.Combine(TrippifyFactory.ObjectStorageRoot, storageKey));
            Assert.Equal(payload.LongLength, bytes.LongLength);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());
        });
    }

    [Fact]
    public async Task Structure_replacement_geocodes_addresses_and_caches_resolved_coordinates_with_attribution()
    {
        using var client = _factory.CreateClient();
        var creator = await CreateCreator("map-store-geocode@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, creator.Email!));
        var created = await client.PostAsJsonAsync("/api/v1/guides", Metadata("Geocode guide"));
        created.EnsureSuccessStatusCode();
        var createdJson = await Json(created);
        var id = createdJson.GetProperty("id").GetGuid();
        var token = createdJson.GetProperty("concurrencyToken").GetGuid();

        var structure = new
        {
            concurrencyToken = token,
            days = new[]
            {
                new
                {
                    title = "Tokyo day",
                    notes = "",
                    nodes = new object[]
                    {
                        Node("Attraction", "Shibuya Crossing", address: "Tokyo", lat: null, lng: null),
                        Node("Restaurant", "Hidden Sushi", address: "Somewhere Unknown Place", lat: null, lng: null),
                    }
                }
            },
            sections = Array.Empty<object>()
        };
        var replaceResp = await client.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure);
        replaceResp.EnsureSuccessStatusCode();
        var detail = await Json(await client.GetAsync($"/api/v1/guides/{id}"));
        var nodes = detail.GetProperty("days")[0].GetProperty("nodes");
        Assert.Equal(2, nodes.GetArrayLength());
        var resolved = nodes[0];
        Assert.Equal("Resolved", resolved.GetProperty("geocodeStatus").GetString());
        Assert.Equal(35.6762, resolved.GetProperty("latitude").GetDouble(), 3);
        Assert.Equal(139.6503, resolved.GetProperty("longitude").GetDouble(), 3);
        Assert.Equal("Local geocoder", resolved.GetProperty("geocodeAttribution").GetString());
        var unresolved = nodes[1];
        Assert.Equal("Unresolved", unresolved.GetProperty("geocodeStatus").GetString());
        Assert.True(!unresolved.TryGetProperty("latitude", out var lat) || lat.ValueKind == JsonValueKind.Null);
        Assert.True(!unresolved.TryGetProperty("longitude", out var lng) || lng.ValueKind == JsonValueKind.Null);

        await WithDb(async db =>
        {
            var cache = await db.GeocodeCache.ToListAsync();
            var queries = new HashSet<string>(cache.Select(x => x.OriginalQuery), StringComparer.OrdinalIgnoreCase);
            Assert.Contains("tokyo", queries);
            Assert.Contains("somewhere unknown place", queries);
            Assert.Contains(cache, entry => entry.Status == GeocodeResolutionStatus.Resolved && Math.Abs(entry.Latitude!.Value - 35.6762) < 0.001);
            Assert.Contains(cache, entry => entry.Status == GeocodeResolutionStatus.Unresolved);
            Assert.All(cache, entry => Assert.False(string.IsNullOrWhiteSpace(entry.ProviderAttribution)));
        });
    }

    [Fact]
    public async Task Structure_replacement_returns_validation_error_when_geocoder_is_offline_and_recovers_after_configured_provider_change()
    {
        using var offline = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMapProvider>();
            services.AddSingleton<IMapProvider, OfflineMapProvider>();
        }));
        var creator = await CreateCreator(offline.Services, "map-store-offline@example.com");
        using var client = offline.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, creator.Email!));
        var created = await client.PostAsJsonAsync("/api/v1/guides", Metadata("Offline map guide"));
        created.EnsureSuccessStatusCode();
        var json = await Json(created);
        var id = json.GetProperty("id").GetGuid();
        var token = json.GetProperty("concurrencyToken").GetGuid();
        var structure = new
        {
            concurrencyToken = token,
            days = new[]
            {
                new
                {
                    title = "Day",
                    notes = "",
                    nodes = new object[] { Node("Attraction", "Hidden Spot", address: "Unknown Place", lat: null, lng: null) }
                }
            },
            sections = Array.Empty<object>()
        };
        var resp = await client.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure);
        resp.EnsureSuccessStatusCode();
        var detail = await Json(await client.GetAsync($"/api/v1/guides/{id}"));
        Assert.Equal("Unresolved", detail.GetProperty("days")[0].GetProperty("nodes")[0].GetProperty("geocodeStatus").GetString());
    }

    [Fact]
    public async Task Map_endpoint_attribution_is_returned_and_no_provider_keys_leak_to_clients()
    {
        using var client = _factory.CreateClient();
        var creator = await CreateCreator("map-store-attribution@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, creator.Email!));
        var created = await client.PostAsJsonAsync("/api/v1/guides", Metadata("Attribution guide"));
        created.EnsureSuccessStatusCode();
        var createdJson = await Json(created);
        var id = createdJson.GetProperty("id").GetGuid();
        var token = createdJson.GetProperty("concurrencyToken").GetGuid();
        var structure = new
        {
            concurrencyToken = token,
            days = new[]
            {
                new
                {
                    title = "Day",
                    notes = "",
                    nodes = new object[] { Node("Attraction", "Eiffel Tower", address: "Paris", lat: null, lng: null) }
                }
            },
            sections = Array.Empty<object>()
        };
        (await client.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var detail = await client.GetStringAsync($"/api/v1/guides/{id}");
        Assert.Contains("geocodeAttribution", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Local geocoder", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Map:ApiKey", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("access_token", detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Provider_healthchecks_register_for_map_and_object_storage()
    {
        using var client = _factory.CreateClient();
        var health = await client.GetAsync("/health/ready");
        health.EnsureSuccessStatusCode();
        var body = await health.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] MakeJpegBytes(int length, int salt = 0)
    {
        var bytes = new byte[length];
        Array.Copy(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, bytes, 4);
        for (var i = 4; i < bytes.Length; i++) bytes[i] = (byte)((i + salt) % 251);
        return bytes;
    }

    private static object Metadata(string title, Guid? token = null) => new { title, subtitle = "Subtitle", summary = "Summary", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1, concurrencyToken = token };
    private static object Node(string type, string name, string? address, double? lat, double? lng) => new { type, name, address, latitude = lat, longitude = lng, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" };

    private static DateTimeOffset ExtractSignedExpiry(string query)
    {
        var pair = query.Split('&').First(p => p.StartsWith("expires="));
        var raw = Uri.UnescapeDataString(pair["expires=".Length..]);
        return DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture);
    }

    private static string ExtractSignedSignature(string query)
    {
        var pair = query.Split('&').First(p => p.StartsWith("signature="));
        return pair["signature=".Length..];
    }

    private static string BuildForgedSignedUrl(string key, DateTimeOffset expires, string signature)
    {
        return $"local://trippify-tests/{Uri.EscapeDataString(key)}?expires={Uri.EscapeDataString(expires.ToString("o"))}&signature={Uri.EscapeDataString(signature)}";
    }

    private Task<AppUser> CreateCreator(string email) => CreateCreator(_factory.Services, email);
    private static async Task<AppUser> CreateCreator(IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
        db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active });
        await db.SaveChangesAsync();
        return user;
    }
    private static async Task<string> Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("accessToken").GetString()!;
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response) { var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }
    private Task WithDb(Func<AppDbContext, Task> action) => WithDb(_factory, action);
    private static async Task WithDb(TrippifyFactory factory, Func<AppDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private sealed class OfflineMapProvider : IMapProvider
    {
        public string ProviderName => "offline";
        public Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken)
            => throw new IOException("Map provider offline.");
    }
}
