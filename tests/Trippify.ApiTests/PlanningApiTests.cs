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

public sealed class PlanningApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";

    [Fact]
    public async Task Selecting_a_guide_day_returns_ordered_markers_and_route_segments()
    {
        using var client = factory.CreateClient();
        var creator = await CreateCreator("planner@example.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, creator.Email!));
        var guideId = await CreateGuideWithDays(client);
        var token = await CurrentToken(client, guideId);

        var route = new
        {
            concurrencyToken = token,
            segments = new[]
            {
                new { mode = "Train", label = "JR line", originName = "Osaka Castle", destinationName = "Nishiki Market", durationMinutes = 55, costPerPersonMinorUnits = 820, currencyCode = "jpy" },
                new { mode = "Walk", label = "Kawaramachi stroll", originName = "Nishiki Market", destinationName = "Gion", durationMinutes = 15, costPerPersonMinorUnits = 0, currencyCode = "JPY" }
            }
        };
        var replaced = await client.PutAsJsonAsync($"/api/v1/guides/{guideId}/days/1/route", route); replaced.EnsureSuccessStatusCode();
        var saved = await Json(replaced);
        Assert.Equal(2, saved.GetProperty("segments").GetArrayLength());
        Assert.Equal("Train", saved.GetProperty("segments")[0].GetProperty("mode").GetString());
        Assert.Equal("JPY", saved.GetProperty("segments")[0].GetProperty("currencyCode").GetString());

        var dayRoute = await Json(await client.GetAsync($"/api/v1/guides/{guideId}/days/1/route"));
        Assert.Equal(3, dayRoute.GetProperty("markers").GetArrayLength());
        Assert.Equal("Osaka Castle", dayRoute.GetProperty("markers")[0].GetProperty("name").GetString());
        Assert.Equal(34.687, dayRoute.GetProperty("markers")[0].GetProperty("latitude").GetDouble(), 3);
        Assert.Equal("Nishiki Market", dayRoute.GetProperty("markers")[1].GetProperty("name").GetString());
        Assert.Equal("Gion Corner", dayRoute.GetProperty("markers")[2].GetProperty("name").GetString());
        Assert.Equal(15, dayRoute.GetProperty("segments")[1].GetProperty("durationMinutes").GetInt32());

        var budget = new
        {
            concurrencyToken = (await Json(await client.GetAsync($"/api/v1/guides/{guideId}"))).GetProperty("concurrencyToken").GetGuid(),
            entries = new[]
            {
                new { category = "Transport", amountPerPersonMinorUnits = 5000, currencyCode = "JPY" },
                new { category = "Food", amountPerPersonMinorUnits = 3000, currencyCode = "JPY" }
            }
        };
        var budgetResponse = await client.PutAsJsonAsync($"/api/v1/guides/{guideId}/budget", budget); budgetResponse.EnsureSuccessStatusCode();
        var savedBudget = await Json(budgetResponse);
        var party = await Json(await client.GetAsync($"/api/v1/guides/{guideId}/budget?partySize=4"));
        Assert.Equal(4, party.GetProperty("partySize").GetInt32());
        Assert.Equal(20000, party.GetProperty("lines")[0].GetProperty("partyTotalMinorUnits").GetInt64());
        Assert.Equal(12000, party.GetProperty("lines")[1].GetProperty("partyTotalMinorUnits").GetInt64());
        await WithDb(async db => Assert.Contains(await db.GuideAuditEntries.Where(x => x.GuideId == guideId).Select(x => x.Action).ToListAsync(), action => action == "route-replaced"));
        await WithDb(async db => Assert.Contains(await db.GuideAuditEntries.Where(x => x.GuideId == guideId).Select(x => x.Action).ToListAsync(), action => action == "budget-replaced"));
    }

    [Fact]
    public async Task Planning_writes_enforce_owner_concurrency_and_validation()
    {
        using var ownerClient = factory.CreateClient(); var owner = await CreateCreator("route-owner@example.com");
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(ownerClient, owner.Email!));
        var id = await CreateGuideWithDays(ownerClient);
        var token = await CurrentToken(ownerClient, id);

        var stale = new { concurrencyToken = Guid.NewGuid(), segments = Array.Empty<object>() };
        Assert.Equal(HttpStatusCode.Conflict, (await ownerClient.PutAsJsonAsync($"/api/v1/guides/{id}/days/0/route", stale)).StatusCode);
        var invalid = new { concurrencyToken = token, segments = new[] { new { mode = "Teleportation", label = "", originName = "", destinationName = "", durationMinutes = 9999, costPerPersonMinorUnits = -5, currencyCode = "yen" } } };
        Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.PutAsJsonAsync($"/api/v1/guides/{id}/days/0/route", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.GetAsync($"/api/v1/guides/{id}/days/9/route")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.GetAsync($"/api/v1/guides/{id}/budget?partySize=99")).StatusCode);

        using var otherClient = factory.CreateClient(); var other = await CreateCreator("route-other@example.com");
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(otherClient, other.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/v1/guides/{id}/days/0/route")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.PutAsJsonAsync($"/api/v1/guides/{id}/budget", new { concurrencyToken = token, entries = Array.Empty<object>() })).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/guides/{id}/days/0/route")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/guides/{id}/budget")).StatusCode);
    }

    private static async Task<Guid> CreateGuideWithDays(HttpClient client)
    {
        var created = await client.PostAsJsonAsync("/api/v1/guides", new { title = "Planning route", subtitle = "S", summary = "Summary", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Osaka" }, tags = Array.Empty<string>(), tripDays = 2 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new
        {
            concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(),
            days = new[]
            {
                new { title = "Osaka", notes = "", nodes = new[] { Node("Attraction", "Osaka Castle", 34.687, 135.526) } },
                new { title = "Kyoto", notes = "", nodes = new[] { Node("Attraction", "Osaka Castle", 34.687, 135.526), Node("Restaurant", "Nishiki Market", 35.005, 135.765), Node("Cafe", "Gion Corner", 35.003, 135.778) } }
            },
            sections = Array.Empty<object>()
        };
        (await client.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        return id;
    }

    private static object Node(string type, string name, double latitude, double longitude) => new { type, name, address = "", latitude, longitude, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" };
    private static async Task<Guid> CurrentToken(HttpClient client, Guid guideId) => (await Json(await client.GetAsync($"/api/v1/guides/{guideId}"))).GetProperty("concurrencyToken").GetGuid();
    private static async Task<JsonElement> Json(HttpResponseMessage response) { var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }
    private Task<AppUser> CreateCreator(string email) => CreateCreator(factory.Services, email);
    private static async Task<AppUser> CreateCreator(IServiceProvider services, string email) { await using var scope = services.CreateAsyncScope(); var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true }; Assert.True((await manager.CreateAsync(user, Password)).Succeeded); db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active }); await db.SaveChangesAsync(); return user; }
    private static async Task<string> Login(HttpClient client, string email) { var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }); response.EnsureSuccessStatusCode(); return (await Json(response)).GetProperty("accessToken").GetString()!; }
    private async Task WithDb(Func<AppDbContext, Task> action) { await using var scope = factory.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
}
