using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trippify.Application;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class VersioningApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";

    [Fact]
    public async Task Publishing_creates_initial_release_automatically()
    {
        var creator = await CreateUser("ver-creator@example.com", creator: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        var list = await Json(creatorClient, $"/api/v1/guides/{guideId}/releases");
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(1, list.GetProperty("items")[0].GetProperty("versionNumber").GetInt32());
    }

    [Fact]
    public async Task Publishing_a_second_release_with_new_content_bumps_version()
    {
        var creator = await CreateUser("ver-bump-creator@example.com", creator: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);
        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{guideId}"))).GetProperty("concurrencyToken").GetGuid();
        var structure = new
        {
            concurrencyToken = token,
            days = new[] { new { title = "Day A", notes = "", nodes = new[] { new { type = "Attraction", name = "Gate", address = "", latitude = 35.0, longitude = 135.5, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 30, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } },
            sections = Array.Empty<object>()
        };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{guideId}/structure", structure)).EnsureSuccessStatusCode();
        var result = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/releases", new { changelog = "Updated Gate info." });
        result.EnsureSuccessStatusCode();
        var release = await Json(result);
        Assert.True(release.GetProperty("versionNumber").GetInt32() >= 2);
        Assert.Equal("Updated Gate info.", release.GetProperty("changelog").GetString());
    }

    [Fact]
    public async Task Publishing_idempotent_when_node_summary_unchanged_returns_latest()
    {
        var creator = await CreateUser("ver-idem-creator@example.com", creator: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        var first = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/releases", new { changelog = "Duplicate attempt" });
        first.EnsureSuccessStatusCode();
        var firstVersion = (await Json(first)).GetProperty("versionNumber").GetInt32();

        var second = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/releases", new { changelog = "Another duplicate attempt" });
        second.EnsureSuccessStatusCode();
        var secondVersion = (await Json(second)).GetProperty("versionNumber").GetInt32();
        Assert.Equal(firstVersion, secondVersion);
    }

    [Fact]
    public async Task Non_creator_cannot_publish_release_but_can_read()
    {
        var creator = await CreateUser("ver-pub-creator@example.com", creator: true);
        var stranger = await CreateUser("ver-pub-stranger@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var strangerClient = factory.CreateClient();
        strangerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(strangerClient, stranger.Email!));
        Assert.Equal(HttpStatusCode.Forbidden, (await strangerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/releases", new { changelog = "nope" })).StatusCode);

        var list = await Json(strangerClient, $"/api/v1/guides/{guideId}/releases");
        Assert.True(list.GetProperty("total").GetInt32() >= 1);
    }

    [Fact]
    public async Task Freshness_endpoint_returns_days_since_latest_release()
    {
        var creator = await CreateUser("ver-fresh-creator@example.com", creator: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var anon = factory.CreateClient();
        var freshness = await Json(anon, $"/api/v1/guides/{guideId}/freshness");
        Assert.Equal(1, freshness.GetProperty("latestVersion").GetInt32());
        Assert.True(freshness.GetProperty("daysSinceLatest").GetInt32() >= 0);
    }

    [Fact]
    public async Task Public_release_lookup_returns_404_for_unknown_id()
    {
        using var anon = factory.CreateClient();
        var response = await anon.GetAsync($"/api/v1/releases/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Buyers_who_purchased_a_paid_guide_receive_release_notifications()
    {
        var creator = await CreateUser("ver-notif-creator@example.com", creator: true);
        var buyer = await CreateUser("ver-notif-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var paidGuideId = await PublishFreeGuide(creatorClient);
        await GrantEntitlement(paidGuideId, buyer.Id);

        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{paidGuideId}"))).GetProperty("concurrencyToken").GetGuid();
        var structure = new {
            concurrencyToken = token,
            days = new[] { new { title = "Day Renamed", notes = "", nodes = new[] { new { type = "Attraction", name = "Spot", address = "", latitude = 34.0, longitude = 135.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } },
            sections = Array.Empty<object>()
        };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{paidGuideId}/structure", structure)).EnsureSuccessStatusCode();

        var result = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{paidGuideId}/releases", new { changelog = "Renamed the day." });
        result.EnsureSuccessStatusCode();
        var release = await Json(result);
        Assert.True(release.GetProperty("versionNumber").GetInt32() >= 2);

        await WithDb(async db =>
        {
            var notifications = await db.Notifications.AsNoTracking().Where(x => x.UserId == buyer.Id).ToListAsync();
            Assert.True(notifications.Count >= 1, $"notifications count = {notifications.Count}");
        });
    }

    private async Task<Guid> PublishFreeGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "V guide", subtitle = "S", summary = "S", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Osaka" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new { concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(), days = new[] { new { title = "Day", notes = "", nodes = new[] { new { type = "Attraction", name = "Spot", address = "", latitude = 34.0, longitude = 135.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        (await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = (object?)null })).EnsureSuccessStatusCode();
        return id;
    }

    private async Task<Guid> PublishPaidGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "V paid guide", subtitle = "S", summary = "S", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new { concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(), days = new[] { new { title = "Day", notes = "", nodes = new[] { new { type = "Attraction", name = "Tower", address = "", latitude = 35.0, longitude = 139.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        (await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = new { priceMinorUnits = 5000, currencyCode = "JPY" } })).EnsureSuccessStatusCode();
        return id;
    }

    private async Task GrantEntitlement(Guid guideId, Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PurchaseEntitlements.Add(new PurchaseEntitlement { Id = Guid.NewGuid(), GuideId = guideId, UserId = userId, OrderId = Guid.NewGuid(), GrantedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    private async Task WithDb(Func<AppDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<JsonElement> Json(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private Task<AppUser> CreateUser(string email, bool creator = false) => CreateUser(factory.Services, email, creator);
    private static async Task<AppUser> CreateUser(IServiceProvider services, string email, bool creator = false)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
        if (creator) db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<string> Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString()!;
    }
}
