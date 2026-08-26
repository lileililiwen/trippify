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

public sealed class DiscoveryApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";

    [Fact]
    public async Task Visitor_searches_publishes_previews_and_author_pages()
    {
        var creator = await CreateCreator("publisher@example.com");
        using var owner = factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(owner, creator.Email!));
        var freeSlug = await CreateAndPublish(owner, "Kyoto temples walk", pricing: null);
        var paidSlug = await CreateAndPublish(owner, "Tokyo luxury nights", pricing: (2500, "JPY"));
        await CreateDraft(owner, "Hidden draft guide");

        using var visitor = factory.CreateClient();
        var search = await Json(await visitor.GetAsync("/api/v1/discovery/guides"));
        Assert.Equal(2, search.GetProperty("total").GetInt32());
        Assert.Contains(search.GetProperty("tagFacets").EnumerateArray(), f => f.GetProperty("value").GetString() == "food");
        Assert.DoesNotContain("hidden-draft-guide", search.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("slug").GetString()));

        var paidOnly = await Json(await visitor.GetAsync("/api/v1/discovery/guides?pricing=paid&country=JP&tag=food&q=luxury"));
        Assert.Equal(1, paidOnly.GetProperty("total").GetInt32());
        Assert.Equal(paidSlug, paidOnly.GetProperty("items")[0].GetProperty("slug").GetString());
        Assert.Equal(2500, paidOnly.GetProperty("items")[0].GetProperty("priceMinorUnits").GetInt64());

        var paidDetail = await Json(await visitor.GetAsync($"/api/v1/discovery/guides/{paidSlug}"));
        Assert.Equal("paid", paidDetail.GetProperty("pricing").GetString());
        Assert.Equal(2500, paidDetail.GetProperty("purchase").GetProperty("priceMinorUnits").GetInt64());
        Assert.Equal("JPY", paidDetail.GetProperty("purchase").GetProperty("currencyCode").GetString());
        Assert.Equal($"/guides/{paidSlug}", paidDetail.GetProperty("shareUrl").GetString());
        Assert.Equal("Tower", paidDetail.GetProperty("days")[0].GetProperty("nodes")[0].GetProperty("name").GetString());
        Assert.False(paidDetail.GetProperty("days")[0].GetProperty("nodes")[0].TryGetProperty("latitude", out _));
        Assert.DoesNotContain("Secret lounge access", paidDetail.GetRawText());

        var freeDetail = await Json(await visitor.GetAsync($"/api/v1/discovery/guides/{freeSlug}"));
        Assert.Equal("free", freeDetail.GetProperty("pricing").GetString());
        Assert.False(freeDetail.TryGetProperty("purchase", out _));
        Assert.True(freeDetail.GetProperty("days")[0].GetProperty("nodes")[0].TryGetProperty("latitude", out _));

        var authorSlug = creator.Id.ToString("N");
        var author = await Json(await visitor.GetAsync($"/api/v1/discovery/authors/{authorSlug}"));
        Assert.Equal(authorSlug, author.GetProperty("slug").GetString());
        Assert.Equal(2, author.GetProperty("guides").GetArrayLength());
    }

    [Fact]
    public async Task Publishing_enforces_ownership_concurrency_and_readiness()
    {
        var owner = await CreateCreator("publish-owner@example.com");
        using var ownerClient = factory.CreateClient();
        ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(ownerClient, owner.Email!));
        var id = await CreateDraft(ownerClient, "Unready guide");
        var token = (await Json(await ownerClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();

        var empty = new { concurrencyToken = token, pricing = (object?)null };
        Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", empty)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await ownerClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = Guid.NewGuid(), pricing = (object?)null })).StatusCode);

        var structure = new
        {
            concurrencyToken = token,
            days = new[] { new { title = "Day one", notes = "", nodes = new[] { Node("Attraction", "Tower", 34.0, 135.0), Node("Restaurant", "Izakaya", 34.01, 135.01, "Secret lounge access") } } },
            sections = Array.Empty<object>()
        };
        (await ownerClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        token = (await Json(await ownerClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        var badPricing = new { concurrencyToken = token, pricing = new { priceMinorUnits = -1, currencyCode = "yen" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await ownerClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", badPricing)).StatusCode);

        using var otherClient = factory.CreateClient(); var other = await CreateCreator("publish-other@example.com");
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(otherClient, other.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = (object?)null })).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = (object?)null })).StatusCode);

        var published = await ownerClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = new { priceMinorUnits = 1500, currencyCode = "JPY" } });
        published.EnsureSuccessStatusCode();
        var slug = (await Json(published)).GetProperty("slug").GetString()!;
        using var visitor = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync($"/api/v1/discovery/guides/{slug}")).StatusCode);

        token = (await Json(published)).GetProperty("concurrencyToken").GetGuid();
        (await ownerClient.PostAsJsonAsync($"/api/v1/guides/{id}/unpublish", new { concurrencyToken = token })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/v1/discovery/guides/{slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await visitor.GetAsync("/api/v1/discovery/guides?pageSize=500")).StatusCode);
    }

    private static async Task<string> CreateAndPublish(HttpClient client, string title, (long, string)? pricing)
    {
        var id = await CreateDraft(client, title);
        var json = await Json(await client.GetAsync($"/api/v1/guides/{id}"));
        var token = json.GetProperty("concurrencyToken").GetGuid();
        var structure = new
        {
            concurrencyToken = token,
            days = new[] { new { title = "Day one", notes = "", nodes = new[] { Node("Attraction", "Tower", 34.687, 135.526), Node("Restaurant", "Izakaya", 34.701, 135.495, "Secret lounge access") } } },
            sections = Array.Empty<object>()
        };
        (await client.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        token = (await Json(await client.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        object request = pricing is null ? new { concurrencyToken = token, pricing = (object?)null } : new { concurrencyToken = token, pricing = new { priceMinorUnits = pricing.Value.Item1, currencyCode = pricing.Value.Item2 } };
        var published = await client.PostAsJsonAsync($"/api/v1/guides/{id}/publish", request); published.EnsureSuccessStatusCode();
        return (await Json(published)).GetProperty("slug").GetString()!;
    }

    private static async Task<Guid> CreateDraft(HttpClient client, string title)
    {
        var created = await client.PostAsJsonAsync("/api/v1/guides", new { title, subtitle = "Subtly tested", summary = "A summary worth publishing", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Osaka" }, tags = new[] { "food" }, tripDays = 1 });
        created.EnsureSuccessStatusCode();
        return (await Json(created)).GetProperty("id").GetGuid();
    }

    private static object Node(string type, string name, double latitude, double longitude, string? notes = null) => new { type, name, address = "", latitude, longitude, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = notes ?? "" };
    private static async Task<JsonElement> Json(HttpResponseMessage response) { var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }
    private Task<AppUser> CreateCreator(string email) => CreateCreator(factory.Services, email);
    private static async Task<AppUser> CreateCreator(IServiceProvider services, string email) { await using var scope = services.CreateAsyncScope(); var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true }; Assert.True((await manager.CreateAsync(user, Password)).Succeeded); db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active }); await db.SaveChangesAsync(); return user; }
    private static async Task<string> Login(HttpClient client, string email) { var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }); response.EnsureSuccessStatusCode(); return (await Json(response)).GetProperty("accessToken").GetString()!; }
}
