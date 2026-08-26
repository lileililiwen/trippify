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

public sealed class ReviewApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";

    [Fact]
    public async Task Verified_purchaser_can_submit_and_edit_review()
    {
        var creator = await CreateUser("review-creator@example.com", creator: true);
        var buyer = await CreateUser("review-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        await GrantEntitlement(guideId, buyer.Id);
        var submit = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 4, body = "Great guide!" });
        submit.EnsureSuccessStatusCode();
        var json = await Json(submit);
        var reviewId = json.GetProperty("id").GetGuid();
        Assert.Equal(4, json.GetProperty("rating").GetInt32());
        Assert.Equal("Great guide!", json.GetProperty("body").GetString());

        var edit = await buyerClient.PutAsJsonAsync($"/api/v1/reviews/{reviewId}", new { rating = 5, body = "Actually perfect!" });
        edit.EnsureSuccessStatusCode();
        var edited = await Json(edit);
        Assert.Equal(5, edited.GetProperty("rating").GetInt32());
        Assert.Equal("Actually perfect!", edited.GetProperty("body").GetString());

        var duplicate = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 3, body = "Duplicate" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Non_purchaser_cannot_submit_review_for_paid_guide()
    {
        var creator = await CreateUser("np-creator@example.com", creator: true);
        var stranger = await CreateUser("np-stranger@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);

        using var strangerClient = factory.CreateClient();
        strangerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(strangerClient, stranger.Email!));
        var result = await strangerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 1, body = "Should fail" });
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task Creator_cannot_review_own_guide()
    {
        var creator = await CreateUser("self-review@example.com", creator: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);
        var result = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 5, body = "Self praise" });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task Author_can_reply_and_reporter_can_report()
    {
        var creator = await CreateUser("reply-creator@example.com", creator: true);
        var buyer = await CreateUser("reply-buyer@example.com");
        var reporter = await CreateUser("reply-reporter@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var submit = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 3, body = "It was okay" });
        submit.EnsureSuccessStatusCode();
        var reviewId = (await Json(submit)).GetProperty("id").GetGuid();

        var reply = await creatorClient.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/reply", new { body = "Thanks for the feedback!" });
        reply.EnsureSuccessStatusCode();
        var replyJson = await Json(reply);
        Assert.Equal("Thanks for the feedback!", replyJson.GetProperty("body").GetString());

        var duplicateReply = await creatorClient.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/reply", new { body = "Second reply" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateReply.StatusCode);

        using var reporterClient = factory.CreateClient();
        reporterClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(reporterClient, reporter.Email!));
        var report = await reporterClient.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/reports", new { reason = "Spam content" });
        report.EnsureSuccessStatusCode();

        var selfReport = await buyerClient.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/reports", new { reason = "Self report" });
        Assert.Equal(HttpStatusCode.BadRequest, selfReport.StatusCode);

        var duplicateReport = await reporterClient.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/reports", new { reason = "Double" });
        Assert.Equal(HttpStatusCode.Conflict, duplicateReport.StatusCode);
    }

    [Fact]
    public async Task Anonymous_user_can_list_visible_reviews()
    {
        var creator = await CreateUser("list-creator@example.com", creator: true);
        var buyer = await CreateUser("list-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 5, body = "Excellent!" })).EnsureSuccessStatusCode();

        using var anonymous = factory.CreateClient();
        var reviews = await anonymous.GetAsync($"/api/v1/guides/{guideId}/reviews");
        reviews.EnsureSuccessStatusCode();
        var list = await Json(reviews);
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal(5, list[0].GetProperty("rating").GetInt32());
    }

    [Fact]
    public async Task Owner_can_delete_and_non_owner_cannot()
    {
        var creator = await CreateUser("del-creator@example.com", creator: true);
        var buyer = await CreateUser("del-buyer@example.com");
        var other = await CreateUser("del-other@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var submit = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 2, body = "Not great" });
        submit.EnsureSuccessStatusCode();
        var reviewId = (await Json(submit)).GetProperty("id").GetGuid();

        using var otherClient = factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(otherClient, other.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.DeleteAsync($"/api/v1/reviews/{reviewId}")).StatusCode);

        var deleted = await buyerClient.DeleteAsync($"/api/v1/reviews/{reviewId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task Feedback_can_be_submitted_once_per_user_per_guide()
    {
        var creator = await CreateUser("fb-creator@example.com", creator: true);
        var user1 = await CreateUser("fb-user1@example.com");
        var user2 = await CreateUser("fb-user2@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var user1Client = factory.CreateClient();
        user1Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(user1Client, user1.Email!));
        (await user1Client.PostAsJsonAsync($"/api/v1/guides/{guideId}/feedback", new { body = "Update the hotel section" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await user1Client.PostAsJsonAsync($"/api/v1/guides/{guideId}/feedback", new { body = "Another feedback" })).StatusCode);

        using var user2Client = factory.CreateClient();
        user2Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(user2Client, user2.Email!));
        (await user2Client.PostAsJsonAsync($"/api/v1/guides/{guideId}/feedback", new { body = "Different user feedback" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Validation_rejects_invalid_ratings_and_bodies()
    {
        var creator = await CreateUser("val-creator@example.com", creator: true);
        var buyer = await CreateUser("val-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        Assert.Equal(HttpStatusCode.BadRequest, (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 0, body = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 6, body = "" })).StatusCode);

        var badBody = new string('x', 4001);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 3, body = badBody })).StatusCode);
    }

    private async Task<Guid> PublishFreeGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Free review guide", subtitle = "Sub", summary = "Writable", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Osaka" }, tags = Array.Empty<string>(), tripDays = 1 });
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
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Paid review guide", subtitle = "Sub", summary = "Writable", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new { concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(), days = new[] { new { title = "Day", notes = "", nodes = new[] { new { type = "Attraction", name = "Tower", address = "", latitude = 35.0, longitude = 139.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        (await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = new { priceMinorUnits = 5000, currencyCode = "JPY" } })).EnsureSuccessStatusCode();
        return id;
    }

    private async Task GrantEntitlement(Guid guideId, Guid userId) { await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.PurchaseEntitlements.Add(new PurchaseEntitlement { Id = Guid.NewGuid(), GuideId = guideId, UserId = userId, OrderId = Guid.NewGuid(), GrantedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }

    private static async Task<JsonElement> Json(HttpResponseMessage response) { var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }
    private Task<AppUser> CreateUser(string email, bool creator = false) => CreateUser(factory.Services, email, creator);
    private static async Task<AppUser> CreateUser(IServiceProvider services, string email, bool creator = false) { await using var scope = services.CreateAsyncScope(); var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true }; Assert.True((await manager.CreateAsync(user, Password)).Succeeded); if (creator) db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active }); await db.SaveChangesAsync(); return user; }
    private static async Task<string> Login(HttpClient client, string email) { var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }); response.EnsureSuccessStatusCode(); return (await Json(response)).GetProperty("accessToken").GetString()!; }
}
