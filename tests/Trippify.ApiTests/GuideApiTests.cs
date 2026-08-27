using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trippify.Application;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class GuideApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";

    [Fact]
    public async Task Creator_can_create_idempotently_reorder_and_read_an_audited_guide()
    {
        using var client = factory.CreateClient();
        var creator = await CreateCreator("guide-author@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, creator.Email!));
        client.DefaultRequestHeaders.Add("Idempotency-Key", "create-kansai");
        var create = new { title = "Kansai seven days", subtitle = "Tested route", summary = "A structured guide", coverUrl = (string?)null, countryCode = "jp", cities = new[] { "Osaka", "Kyoto" }, tags = new[] { "food" }, tripDays = 2 };
        var first = await client.PostAsJsonAsync("/api/v1/guides", create); first.EnsureSuccessStatusCode();
        var firstJson = await Json(first); var guideId = firstJson.GetProperty("id").GetGuid(); var token = firstJson.GetProperty("concurrencyToken").GetGuid();
        var replay = await client.PostAsJsonAsync("/api/v1/guides", create); replay.EnsureSuccessStatusCode();
        Assert.Equal(guideId, (await Json(replay)).GetProperty("id").GetGuid());

        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        var structure = new
        {
            concurrencyToken = token,
            days = new[]
            {
                new { title = "Kyoto", notes = "Second planned first", nodes = new[] { Node("Restaurant", "Nishiki Market", 35.005, 135.765) } },
                new { title = "Osaka", notes = "First planned second", nodes = new[] { Node("Attraction", "Osaka Castle", 34.687, 135.526) } }
            },
            sections = new[] { new { type = "Preparation", title = "Before departure", body = "Load the transit card." } }
        };
        var replaced = await client.PutAsJsonAsync($"/api/v1/guides/{guideId}/structure", structure); replaced.EnsureSuccessStatusCode();
        var detail = await Json(await client.GetAsync($"/api/v1/guides/{guideId}"));
        Assert.Equal("Kyoto", detail.GetProperty("days")[0].GetProperty("title").GetString());
        Assert.Equal("Osaka", detail.GetProperty("days")[1].GetProperty("title").GetString());
        await WithDb(async db => Assert.True(await db.GuideAuditEntries.CountAsync(x => x.GuideId == guideId) >= 2));
    }

    [Fact]
    public async Task Guide_writes_enforce_owner_validation_and_concurrency()
    {
        using var ownerClient = factory.CreateClient(); var owner = await CreateCreator("owner@example.com");
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(ownerClient, owner.Email!));
        var created = await ownerClient.PostAsJsonAsync("/api/v1/guides", Metadata("Private guide")); created.EnsureSuccessStatusCode();
        var createdJson = await Json(created); var id = createdJson.GetProperty("id").GetGuid(); var token = createdJson.GetProperty("concurrencyToken").GetGuid();
        var update = Metadata("Updated private guide", token);
        var updated = await ownerClient.PutAsJsonAsync($"/api/v1/guides/{id}/metadata", update); updated.EnsureSuccessStatusCode();
        var currentToken = (await Json(updated)).GetProperty("concurrencyToken").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await ownerClient.PutAsJsonAsync($"/api/v1/guides/{id}/metadata", update)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", new { concurrencyToken = currentToken, days = new[] { new { title = "Bad", notes = "", nodes = new[] { Node("Attraction", "Impossible", 100, 20) } } }, sections = Array.Empty<object>() })).StatusCode);

        using var otherClient = factory.CreateClient(); var other = await CreateCreator("other@example.com");
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(otherClient, other.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/v1/guides/{id}")).StatusCode);
        using var anonymous = factory.CreateClient(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/guides/{id}")).StatusCode);
    }

    [Fact]
    public async Task Media_provider_failure_returns_service_unavailable_without_persistence()
    {
        using var unavailable = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => { services.RemoveAll<IObjectStorage>(); services.AddSingleton<IObjectStorage, ThrowingStorage>(); }));
        var creator = await CreateCreator(unavailable.Services, "media-author@example.com");
        using var client = unavailable.CreateClient(); client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, creator.Email!));
        var created = await client.PostAsJsonAsync("/api/v1/guides", Metadata("Media guide")); created.EnsureSuccessStatusCode(); var json = await Json(created); var id = json.GetProperty("id").GetGuid(); var token = json.GetProperty("concurrencyToken").GetGuid();
        var response = await client.PostAsJsonAsync($"/api/v1/guides/{id}/media", new { concurrencyToken = token, storageKey = "guides/photo.jpg", contentBase64 = "/9j/4AAQ", contentType = "image/jpeg", caption = "Photo" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var scope = unavailable.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().GuideMedia.AnyAsync());
    }

    private static object Metadata(string title, Guid? token = null) => new { title, subtitle = "Subtitle", summary = "Summary", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1, concurrencyToken = token };
    private static object Node(string type, string name, double latitude, double longitude) => new { type, name, address = "", latitude, longitude, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" };
    private static async Task<JsonElement> Json(HttpResponseMessage response) { var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }
    private Task<AppUser> CreateCreator(string email) => CreateCreator(factory.Services, email);
    private static async Task<AppUser> CreateCreator(IServiceProvider services, string email) { await using var scope = services.CreateAsyncScope(); var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true }; Assert.True((await manager.CreateAsync(user, Password)).Succeeded); db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active }); await db.SaveChangesAsync(); return user; }
    private static async Task<string> Login(HttpClient client, string email) { var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }); response.EnsureSuccessStatusCode(); return (await Json(response)).GetProperty("accessToken").GetString()!; }
    private async Task WithDb(Func<AppDbContext, Task> action) { await using var scope = factory.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
    private sealed class ThrowingStorage : IObjectStorage
    {
        public Task<Uri> PutAsync(string key, Stream content, CancellationToken cancellationToken) => throw new IOException("offline");
        public Task DeleteAsync(string key, CancellationToken cancellation) => throw new IOException("offline");
        public Task<Uri> CreateSignedReadAsync(string key, TimeSpan lifetime, CancellationToken cancellationToken) => throw new IOException("offline");
        public Task<bool> ExistsAsync(string key, CancellationToken cancellation) => throw new IOException("offline");
    }
}
