using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class LibraryApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";

    [Fact]
    public async Task Favorites_and_trips_require_access_and_respect_ownership()
    {
        var creator = await CreateUser("library-creator@example.com", creator: true);
        var buyer = await CreateUser("library-buyer@example.com");
        var snooper = await CreateUser("library-snooper@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var paidGuideId = await PublishPaidGuide(creatorClient);
        var freeGuideId = await PublishFreeGuide(creatorClient);
        var draftGuideId = await CreateDraft(creatorClient);

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/library/favorites/" + paidGuideId, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/library/trips", new { guideId = freeGuideId })).StatusCode);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await buyerClient.PostAsJsonAsync("/api/v1/library/favorites/" + draftGuideId, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await buyerClient.PostAsJsonAsync("/api/v1/library/trips", new { guideId = draftGuideId })).StatusCode);
        var favorited = await buyerClient.PostAsJsonAsync($"/api/v1/library/favorites/{freeGuideId}", new { }); favorited.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await buyerClient.PostAsJsonAsync($"/api/v1/library/favorites/{freeGuideId}", new { })).StatusCode);
        var favorites = await Json(await buyerClient.GetAsync("/api/v1/library/favorites"));
        Assert.Equal(1, favorites.GetArrayLength());

        var trip = await buyerClient.PostAsJsonAsync("/api/v1/library/trips", new { guideId = freeGuideId, title = "Trip plan" }); trip.EnsureSuccessStatusCode();
        var tripId = (await Json(trip)).GetProperty("id").GetGuid();
        var update = await buyerClient.PatchAsJsonAsync($"/api/v1/library/trips/{tripId}", new { notes = "Bring camera", status = "Active" }); update.EnsureSuccessStatusCode();
        var updated = await Json(await buyerClient.GetAsync("/api/v1/library/trips"));
        Assert.Equal("Active", updated[0].GetProperty("status").GetString());
        Assert.Equal("Bring camera", updated[0].GetProperty("notes").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await buyerClient.PostAsJsonAsync("/api/v1/library/trips", new { guideId = paidGuideId })).StatusCode);
        await GrantEntitlement(paidGuideId, buyer.Id);
        var paidTrip = await buyerClient.PostAsJsonAsync("/api/v1/library/trips", new { guideId = paidGuideId }); paidTrip.EnsureSuccessStatusCode();
        var paidTripId = (await Json(paidTrip)).GetProperty("id").GetGuid();
        (await buyerClient.DeleteAsync($"/api/v1/library/trips/{tripId}")).EnsureSuccessStatusCode();

        using var snooperClient = factory.CreateClient();
        snooperClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(snooperClient, snooper.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await snooperClient.DeleteAsync($"/api/v1/library/trips/{paidTripId}")).StatusCode);
        Assert.Empty((await Json(await snooperClient.GetAsync("/api/v1/library/favorites"))).EnumerateArray());
    }

    [Fact]
    public async Task Forks_create_attributed_private_guides_for_entitled_users_only()
    {
        var creator = await CreateUser("fork-creator@example.com", creator: true);
        var buyer = await CreateUser("fork-buyer@example.com");
        var nonBuyer = await CreateUser("fork-stranger@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var paidGuideId = await PublishPaidGuide(creatorClient);
        var freeGuideId = await PublishFreeGuide(creatorClient);

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/library/forks", new { guideId = freeGuideId })).StatusCode);

        using var nonBuyerClient = factory.CreateClient();
        nonBuyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(nonBuyerClient, nonBuyer.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await nonBuyerClient.PostAsJsonAsync("/api/v1/library/forks", new { guideId = paidGuideId })).StatusCode);
        var freeFork = await nonBuyerClient.PostAsJsonAsync("/api/v1/library/forks", new { guideId = freeGuideId }); freeFork.EnsureSuccessStatusCode();
        var fork = await Json(freeFork);
        Assert.Equal("Draft", (await Json(await nonBuyerClient.GetAsync($"/api/v1/guides/{fork.GetProperty("id").GetGuid()}"))).GetProperty("lifecycle").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await nonBuyerClient.PostAsJsonAsync("/api/v1/library/forks", new { guideId = freeGuideId })).StatusCode);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await buyerClient.PostAsJsonAsync("/api/v1/library/forks", new { guideId = paidGuideId })).StatusCode);
        await GrantEntitlement(paidGuideId, buyer.Id);
        var paidFork = await buyerClient.PostAsJsonAsync("/api/v1/library/forks", new { guideId = paidGuideId }); paidFork.EnsureSuccessStatusCode();
        var forkJson = await Json(paidFork);
        Assert.Equal(paidGuideId, forkJson.GetProperty("sourceGuideId").GetGuid());
        await WithDb(async db => Assert.True(await db.GuideAuditEntries.AnyAsync(x => x.GuideId == paidGuideId && x.Action == "forked" && x.ActorUserId == buyer.Id)));
    }

    private async Task<Guid> PublishPaidGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Paid nights", subtitle = "Sub", summary = "Worth buying once", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new { concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(), days = new[] { new { title = "Night one", notes = "", nodes = new[] { new { type = "Attraction", name = "Tower", address = "", latitude = 34.0, longitude = 135.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        var published = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = new { priceMinorUnits = 2500, currencyCode = "JPY" } });
        published.EnsureSuccessStatusCode();
        return id;
    }

    private async Task<Guid> PublishFreeGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Free nights", subtitle = "Sub", summary = "Worth reading", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Kyoto" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new { concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(), days = new[] { new { title = "Day", notes = "", nodes = new[] { new { type = "Attraction", name = "Temple", address = "", latitude = 35.0, longitude = 135.7, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        var published = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = (object?)null });
        published.EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<Guid> CreateDraft(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Hidden", subtitle = "S", summary = "S", coverUrl = (string?)null, countryCode = "JP", cities = Array.Empty<string>(), tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        return (await Json(created)).GetProperty("id").GetGuid();
    }

    private async Task GrantEntitlement(Guid guideId, Guid userId) { await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.PurchaseEntitlements.Add(new PurchaseEntitlement { Id = Guid.NewGuid(), GuideId = guideId, UserId = userId, OrderId = Guid.NewGuid(), GrantedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }

    private static async Task<JsonElement> Json(HttpResponseMessage response) { var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }
    private Task<AppUser> CreateUser(string email, bool creator = false) => CreateUser(factory.Services, email, creator);
    private static async Task<AppUser> CreateUser(IServiceProvider services, string email, bool creator = false) { await using var scope = services.CreateAsyncScope(); var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true }; Assert.True((await manager.CreateAsync(user, Password)).Succeeded); if (creator) db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active }); await db.SaveChangesAsync(); return user; }
    private static async Task<string> Login(HttpClient client, string email) { var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }); response.EnsureSuccessStatusCode(); return (await Json(response)).GetProperty("accessToken").GetString()!; }
    private async Task WithDb(Func<AppDbContext, Task> action) { await using var scope = factory.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
}
