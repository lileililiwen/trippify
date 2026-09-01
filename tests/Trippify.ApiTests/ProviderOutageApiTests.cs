using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trippify.Api;
using Trippify.Application;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

/// <summary>
/// Release-gate coverage for provider outage behavior. Each scenario in this
/// suite replaces a single provider with one that fails on every call. The
/// API must fail safely — never return a 2xx with fabricated success — and
/// must surface enough context for the operator to diagnose the failure.
/// </summary>
public sealed class ProviderOutageApiTests
{
    [Fact]
    public async Task Object_storage_outage_marks_guide_media_upload_as_failed_and_never_returns_success()
    {
        await using var outageFactory = new OutageFactory(o =>
        {
            o.StorageError = new IOException("object storage offline");
        });
        using var client = outageFactory.CreateClient();
        var creator = await outageFactory.CreateCreatorAsync("outage-storage@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await outageFactory.LoginAsync(client, creator.Email!));

        var created = await client.PostAsJsonAsync("/api/v1/guides", Metadata("Outage guide"));
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.IsSuccessStatusCode, "Create guide should succeed without object storage. Body: " + createdBody);
        var createdJson = JsonDocument.Parse(createdBody).RootElement.Clone();
        var id = createdJson.GetProperty("id").GetGuid();
        var token = createdJson.GetProperty("concurrencyToken").GetGuid();
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F' };

        var upload = await client.PostAsJsonAsync($"/api/v1/guides/{id}/media", new
        {
            concurrencyToken = token,
            storageKey = "guides/outage.jpg",
            contentBase64 = Convert.ToBase64String(jpeg),
            contentType = "image/jpeg",
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, upload.StatusCode);
        var body = await upload.Content.ReadAsStringAsync();
        // The 5xx must be the "fails safely" answer, not a 2xx with a
        // fabricated success payload.
        Assert.DoesNotContain("\"sha256\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Map_provider_outage_does_not_fabricate_resolved_coordinates()
    {
        await using var outageFactory = new OutageFactory(o =>
        {
            o.MapError = new IOException("map provider offline");
        });
        using var client = outageFactory.CreateClient();
        var creator = await outageFactory.CreateCreatorAsync("outage-map@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await outageFactory.LoginAsync(client, creator.Email!));

        var created = await client.PostAsJsonAsync("/api/v1/guides", Metadata("Map outage guide"));
        created.EnsureSuccessStatusCode();
        var id = (await Json(created)).GetProperty("id").GetGuid();
        var token = (await Json(created)).GetProperty("concurrencyToken").GetGuid();

        // Replace the structure with a node that requires geocoding. The
        // API must not fabricate coordinates when the geocoder is offline;
        // it must record an Unresolved status instead.
        var structure = new
        {
            concurrencyToken = token,
            days = new[]
            {
                new
                {
                    title = "Day",
                    notes = "",
                    nodes = new object[]
                    {
                        MapNode("Attraction", "Hidden Spot", "Unknown Place", null, null)
                    }
                }
            },
            sections = Array.Empty<object>()
        };
        var resp = await client.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure);
        resp.EnsureSuccessStatusCode();
        var detail = await Json(await client.GetAsync($"/api/v1/guides/{id}"));
        var node = detail.GetProperty("days")[0].GetProperty("nodes")[0];
        Assert.Equal("Unresolved", node.GetProperty("geocodeStatus").GetString());
    }

    private static object MapNode(string type, string name, string? address, double? lat, double? lng) => new
    {
        type,
        name,
        address,
        latitude = lat,
        longitude = lng,
        arrivalTime = (string?)null,
        departureTime = (string?)null,
        stayMinutes = 60,
        ticketInformation = (string?)null,
        reservationInformation = (string?)null,
        openingHours = (string?)null,
        notes = "",
    };

    private static object Metadata(string title) => new
    {
        title,
        subtitle = "Subtitle",
        summary = "Summary",
        coverUrl = (string?)null,
        countryCode = "JP",
        cities = new[] { "Tokyo" },
        tags = Array.Empty<string>(),
        tripDays = 1,
    };

    [Fact]
    public async Task Evidence_scanner_outage_fails_closed_and_does_not_mark_attachment_ready()
    {
        await using var outageFactory = new OutageFactory(o =>
        {
            o.ScannerError = new EvidenceScannerUnavailableException("scanner offline");
        });
        using var client = outageFactory.CreateClient();
        var creator = await outageFactory.CreateCreatorAsync("outage-scanner@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await outageFactory.LoginAsync(client, creator.Email!));

        var bytes = MakeJpegBytes(2048);
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var stage = await client.PostAsJsonAsync("/api/v1/evidence/attachments", new
        {
            fileName = "evidence.jpg",
            contentType = "image/jpeg",
            sizeBytes = bytes.Length,
            sha256 = sha,
        });
        stage.EnsureSuccessStatusCode();
        var id = (await Json(stage)).GetProperty("attachmentId").GetGuid();

        // Upload the attachment content. The scanner runs as a background
        // job; with the scanner unavailable, the job must dead-letter and
        // the attachment must be Rejected, never Ready.
        var token = client.DefaultRequestHeaders.Authorization?.Parameter;
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/evidence/attachments/{id}/content");
        if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        var upload = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        // Force the queued background scan job to run.
        await outageFactory.WithDbAsync(async db =>
        {
            var job = await db.BackgroundJobs.SingleAsync(x =>
                x.Type == BackgroundJobTypes.EvidenceAttachmentScan && x.Payload.Contains(id.ToString()));
            job.AvailableAt = DateTimeOffset.UtcNow;
            job.MaxAttempts = 1;
            await db.SaveChangesAsync();
        });
        await using var scope = outageFactory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<Trippify.Api.BackgroundJobProcessor>().ProcessBatchAsync("outage-test", 20, default);

        await outageFactory.WithDbAsync(async db =>
        {
            var row = await db.EvidenceAttachments.SingleAsync(x => x.Id == id);
            Assert.NotEqual("Ready", row.State.ToString());
        });
    }

    [Fact]
    public async Task Email_outage_does_not_block_account_creation()
    {
        await using var outageFactory = new OutageFactory(o =>
        {
            o.EmailError = new InvalidOperationException("SMTP offline");
        });
        using var client = outageFactory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = "outage-email@example.com",
            password = "Strong!Pass123",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await outageFactory.WithDbAsync(async db =>
        {
            var created = await db.Users.SingleAsync(u => u.Email == "outage-email@example.com");
            Assert.NotNull(created);
        });
    }

    private static byte[] MakeJpegBytes(int size)
    {
        var bytes = new byte[size];
        var head = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F' };
        Array.Copy(head, bytes, head.Length);
        return bytes;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private sealed class OutageOverrides
    {
        public Exception? StorageError { get; set; }
        public Exception? MapError { get; set; }
        public Exception? ScannerError { get; set; }
        public Exception? EmailError { get; set; }
    }

    private sealed class OutageFactory : WebApplicationFactory<Program>
    {
        private readonly OutageOverrides _overrides;
        private readonly string _databaseName = "trippify-outage-" + Guid.NewGuid();
        private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "trippify-outage-" + Guid.NewGuid().ToString("N"));

        public OutageFactory(Action<OutageOverrides> configure)
        {
            _overrides = new OutageOverrides();
            configure(_overrides);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoSeed:Enabled"] = "false",
                ["BackgroundJobs:WorkersEnabled"] = "false",
                ["ObjectStorage:Provider"] = "local",
                ["ObjectStorage:LocalRoot"] = _storageRoot,
                ["ObjectStorage:PublicBaseUrl"] = "local://trippify-outage/",
                ["ObjectStorage:SignedUrlSecret"] = "outage-test-secret",
                ["Map:Provider"] = "local",
                ["EvidenceScanner:Provider"] = "local",
                ["EvidenceScanner:Enabled"] = "true",
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_databaseName));

                if (_overrides.StorageError is not null)
                {
                    services.RemoveAll<IObjectStorage>();
                    services.AddSingleton<IObjectStorage>(new ThrowingObjectStorage(_overrides.StorageError));
                }
                if (_overrides.MapError is not null)
                {
                    services.RemoveAll<IMapProvider>();
                    services.AddSingleton<IMapProvider>(new ThrowingMapProvider(_overrides.MapError));
                }
                if (_overrides.ScannerError is not null)
                {
                    services.RemoveAll<IEvidenceScanner>();
                    services.AddSingleton<IEvidenceScanner>(new ThrowingEvidenceScanner(_overrides.ScannerError));
                }
                if (_overrides.EmailError is not null)
                {
                    services.RemoveAll<IEmailSender>();
                    services.AddSingleton<IEmailSender>(new ThrowingEmailSender(_overrides.EmailError));
                }
            });
        }

        public async Task<AppUser> CreateCreatorAsync(string email)
        {
            await using var scope = Services.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
            Assert.True((await users.CreateAsync(user, "Strong!Pass123")).Succeeded);
            db.CreatorProfiles.Add(new CreatorProfile
            {
                UserId = user.Id,
                Slug = email.Split('@')[0],
                Status = CreatorStatus.Active,
            });
            await db.SaveChangesAsync();
            return user;
        }

        public async Task<string> LoginAsync(HttpClient client, string email)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Strong!Pass123" });
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return json.RootElement.GetProperty("accessToken").GetString()!;
        }

        public async Task WithDbAsync(Func<AppDbContext, Task> action)
        {
            await using var scope = Services.CreateAsyncScope();
            await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }
    }

    private sealed class ThrowingObjectStorage : IObjectStorage
    {
        private readonly Exception _error;
        public ThrowingObjectStorage(Exception error) { _error = error; }
        public string ProviderName => "throwing";
        public Task<Uri> PutAsync(string key, Stream content, CancellationToken cancellationToken) => throw _error;
        public Task DeleteAsync(string key, CancellationToken cancellationToken) => throw _error;
        public Task<Uri> CreateSignedReadAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken) => throw _error;
        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) => throw _error;
    }

    private sealed class ThrowingMapProvider : IMapProvider
    {
        private readonly Exception _error;
        public ThrowingMapProvider(Exception error) { _error = error; }
        public string ProviderName => "throwing";
        public Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken) => throw _error;
    }

    private sealed class ThrowingEvidenceScanner : IEvidenceScanner
    {
        private readonly Exception _error;
        public ThrowingEvidenceScanner(Exception error) { _error = error; }
        public string ProviderName => "throwing";
        public Task<EvidenceScanResult> ScanAsync(EvidenceScanRequest request, CancellationToken cancellationToken) => throw _error;
    }

    private sealed class ThrowingEmailSender : IEmailSender
    {
        private readonly Exception _error;
        public ThrowingEmailSender(Exception error) { _error = error; }
        public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken) => throw _error;
    }
}
