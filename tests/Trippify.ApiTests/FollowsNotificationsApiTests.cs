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

public sealed class FollowsNotificationsApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";

    [Fact]
    public async Task Follow_then_unfollow_is_idempotent_and_self_follow_is_rejected()
    {
        var creator = await CreateUser("fn-creator@example.com", creator: true);
        var follower = await CreateUser("fn-follower@example.com");
        using var followerClient = factory.CreateClient();
        followerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(followerClient, follower.Email!));
        var slug = (await dbSlug(creator.Id));

        var first = await followerClient.PostAsync($"/api/v1/creators/{slug}/follow", null);
        first.EnsureSuccessStatusCode();
        var second = await followerClient.PostAsync($"/api/v1/creators/{slug}/follow", null);
        second.EnsureSuccessStatusCode();

        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        Assert.Equal(HttpStatusCode.BadRequest, (await creatorClient.PostAsync($"/api/v1/creators/{slug}/follow", null)).StatusCode);

        var count = await Json(followerClient, $"/api/v1/creators/{slug}/followers/count");
        Assert.Equal(1, count.GetProperty("followers").GetInt32());

        var status = await Json(followerClient, $"/api/v1/creators/{slug}/follow");
        Assert.True(status.GetProperty("following").GetBoolean());

        var delete = await followerClient.DeleteAsync($"/api/v1/creators/{slug}/follow");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var deleted = await Json(followerClient, $"/api/v1/creators/{slug}/follow");
        Assert.False(deleted.GetProperty("following").GetBoolean());
    }

    [Fact]
    public async Task Anonymous_can_read_follower_count_but_follow_status_requires_auth()
    {
        var creator = await CreateUser("fn-pub-creator@example.com", creator: true);
        var slug = await dbSlug(creator.Id);
        using var anon = factory.CreateClient();
        var json = await Json(anon, $"/api/v1/creators/{slug}/followers/count");
        Assert.Equal(0, json.GetProperty("followers").GetInt32());

        var follower = await CreateUser("fn-pub-follower@example.com");
        using var followerClient = factory.CreateClient();
        followerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(followerClient, follower.Email!));
        var jsonAuth = await Json(followerClient, $"/api/v1/creators/{slug}/follow");
        Assert.False(jsonAuth.GetProperty("following").GetBoolean());
    }

    [Fact]
    public async Task Reviews_on_my_guide_emit_a_notification_when_in_app_preferred()
    {
        var creator = await CreateUser("fn-notif-creator@example.com", creator: true);
        var buyer = await CreateUser("fn-notif-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 5, body = new string('a', 60) })).EnsureSuccessStatusCode();

        var list = await Json(creatorClient, "/api/v1/me/notifications");
        Assert.True(list.GetProperty("unreadCount").GetInt32() >= 1);
        var first = list.GetProperty("items").EnumerateArray().First();
        Assert.Equal("NewReviewOnMyGuide", first.GetProperty("kind").GetString());
        Assert.Equal("New review on your guide", first.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Disabling_in_app_suppresses_review_notification()
    {
        var creator = await CreateUser("fn-off-creator@example.com", creator: true);
        var buyer = await CreateUser("fn-off-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        var prefs = await Json(creatorClient, "/api/v1/me/notification-preferences");
        Assert.True(prefs.GetProperty("newReviewOnMyGuideInApp").GetBoolean());
        var put = await creatorClient.PutAsJsonAsync("/api/v1/me/notification-preferences", new
        {
            emailEnabled = true,
            inAppEnabled = true,
            newGuidePublishedEmail = true,
            newGuidePublishedInApp = true,
            newReviewOnMyGuideEmail = false,
            newReviewOnMyGuideInApp = false,
            newReplyToReviewEmail = true,
            newReplyToReviewInApp = true,
            followerGainedEmail = true,
            followerGainedInApp = true,
            evidenceReviewedEmail = true,
            evidenceReviewedInApp = true,
        });
        put.EnsureSuccessStatusCode();

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 5, body = new string('b', 60) })).EnsureSuccessStatusCode();

        var list = await Json(creatorClient, "/api/v1/me/notifications");
        Assert.Equal(0, list.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Notifications_are_restricted_to_the_recipient()
    {
        var creator = await CreateUser("fn-own-creator@example.com", creator: true);
        var buyer = await CreateUser("fn-own-buyer@example.com");
        var intruder = await CreateUser("fn-own-intruder@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var response = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 5, body = new string('c', 60) });
        response.EnsureSuccessStatusCode();
        var reviewId = (await Json(response)).GetProperty("id").GetGuid();

        using var intruderClient = factory.CreateClient();
        intruderClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(intruderClient, intruder.Email!));
        Assert.Equal(HttpStatusCode.NotFound, (await intruderClient.PostAsync($"/api/v1/me/notifications/{reviewId}/read", null)).StatusCode);
    }

    [Fact]
    public async Task Mark_notification_read_is_idempotent_for_other_users_but_succeeds_for_owner()
    {
        var creator = await CreateUser("fn-read-creator@example.com", creator: true);
        var buyer = await CreateUser("fn-read-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 5, body = new string('d', 60) })).EnsureSuccessStatusCode();

        var list = await Json(creatorClient, "/api/v1/me/notifications");
        var id = list.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var read = await creatorClient.PostAsync($"/api/v1/me/notifications/{id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);
        var readAgain = await creatorClient.PostAsync($"/api/v1/me/notifications/{id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, readAgain.StatusCode);
        var after = await Json(creatorClient, "/api/v1/me/notifications");
        Assert.Equal(0, after.GetProperty("unreadCount").GetInt32());
    }

    private async Task<string> dbSlug(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.CreatorProfiles.AsNoTracking().SingleAsync(x => x.UserId == userId);
        return profile.Slug;
    }

    private async Task<Guid> PublishFreeGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "FN guide", subtitle = "S", summary = "S", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Osaka" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new { concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(), days = new[] { new { title = "Day", notes = "", nodes = new[] { new { type = "Attraction", name = "Spot", address = "", latitude = 34.0, longitude = 135.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        (await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = (object?)null })).EnsureSuccessStatusCode();
        return id;
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
