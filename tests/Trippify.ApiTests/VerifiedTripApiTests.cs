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

public sealed class VerifiedTripApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";
    private const string AdminRole = "Administrator";

    [Fact]
    public async Task Entitled_buyer_can_submit_evidence_once_with_retention_deadline()
    {
        var creator = await CreateUser("vt-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var body = new string('a', 60);
        var first = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "TripJournal", body, redactedReference = "REF-001" });
        first.EnsureSuccessStatusCode();
        var firstJson = await Json(first);
        Assert.Equal("Pending", firstJson.GetProperty("status").GetString());
        var retention = firstJson.GetProperty("retentionDeadline").GetDateTimeOffset();
        Assert.True(retention > firstJson.GetProperty("submittedAt").GetDateTimeOffset());

        var dup = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "Receipt", body });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        await WithDb(async db => Assert.True(await db.TripEvidence.AsNoTracking().AnyAsync(x => x.GuideId == guideId && x.UserId == buyer.Id)));
    }

    [Fact]
    public async Task Non_purchaser_cannot_submit_evidence_for_paid_guide()
    {
        var creator = await CreateUser("vt-paid-creator@example.com", creator: true);
        var stranger = await CreateUser("vt-paid-stranger@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var paidGuideId = await PublishPaidGuide(creatorClient);

        using var strangerClient = factory.CreateClient();
        strangerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(strangerClient, stranger.Email!));
        var body = new string('x', 60);
        var result = await strangerClient.PostAsJsonAsync($"/api/v1/guides/{paidGuideId}/evidence", new { kind = "Receipt", body });
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
    }

    [Fact]
    public async Task Creator_cannot_review_own_guide_evidence_but_admin_can_approve_and_grant_badge()
    {
        var creator = await CreateUser("vt-owner@example.com", creator: true);
        var buyer = await CreateUser("vt-owner-buyer@example.com");
        var admin = await CreateUser("vt-owner-admin@example.com", admin: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var body = new string('y', 60);
        var submitted = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "BookingConfirmation", body });
        submitted.EnsureSuccessStatusCode();
        var evidenceId = (await Json(submitted)).GetProperty("id").GetGuid();

        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        Assert.Equal(HttpStatusCode.Forbidden, (await creatorClient.PostAsJsonAsync($"/api/v1/admin/evidence/{evidenceId}/review", new { decision = "Approved", reason = "looks right" })).StatusCode);

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        var reviewed = await adminClient.PostAsJsonAsync($"/api/v1/admin/evidence/{evidenceId}/review", new { decision = "Approved", reason = "valid" });
        reviewed.EnsureSuccessStatusCode();
        Assert.Equal("Approved", (await Json(reviewed)).GetProperty("status").GetString());

        var badge = await Json(await adminClient.GetAsync($"/api/v1/guides/{guideId}/evidence/badge"));
        Assert.True(badge.GetProperty("verified").GetBoolean());
        Assert.Equal(1, badge.GetProperty("approvedEvidenceCount").GetInt32());

        var reReview = await adminClient.PostAsJsonAsync($"/api/v1/admin/evidence/{evidenceId}/review", new { decision = "Approved", reason = "again" });
        Assert.Equal(HttpStatusCode.Conflict, reReview.StatusCode);

        await WithDb(async db => Assert.True(await db.EvidenceReviews.AsNoTracking().AnyAsync(x => x.EvidenceId == evidenceId && x.Decision == EvidenceStatus.Approved)));
    }

    [Fact]
    public async Task Evidence_retention_deletion_decrements_badge_and_is_idempotent()
    {
        var creator = await CreateUser("vt-del-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-del-buyer@example.com");
        var admin = await CreateUser("vt-del-admin@example.com", admin: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var body = new string('z', 60);
        var submitted = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "PhotoNote", body });
        submitted.EnsureSuccessStatusCode();
        var evidenceId = (await Json(submitted)).GetProperty("id").GetGuid();

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        (await adminClient.PostAsJsonAsync($"/api/v1/admin/evidence/{evidenceId}/review", new { decision = "Approved", reason = "ok" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.DeleteAsync($"/api/v1/admin/evidence/{evidenceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.DeleteAsync($"/api/v1/admin/evidence/{evidenceId}")).StatusCode);

        var badge = await Json(await adminClient.GetAsync($"/api/v1/guides/{guideId}/evidence/badge"));
        Assert.False(badge.GetProperty("verified").GetBoolean());

        await WithDb(async db => { var row = await db.TripEvidence.AsNoTracking().SingleAsync(x => x.Id == evidenceId); Assert.NotNull(row.DeletedAt); });
    }

    [Fact]
    public async Task Public_badge_is_coarse_and_hides_evidence_bodies()
    {
        var creator = await CreateUser("vt-pub-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-pub-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var body = new string('q', 80);
        (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "Other", body })).EnsureSuccessStatusCode();

        using var anon = factory.CreateClient();
        var response = await anon.GetAsync($"/api/v1/guides/{guideId}/evidence/badge");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("body", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("redactedReference", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Insights_obey_k_anonymity_and_unique_per_user_per_guide()
    {
        var creator = await CreateUser("vt-ins-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-ins-buyer@example.com");
        var stranger = await CreateUser("vt-ins-stranger@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        var submission = new { partySize = 2, tripDays = 5, totalCostMinorUnits = 250000, currencyCode = "JPY" };

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/insights", submission)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/insights", submission)).StatusCode);

        var creatorSelf = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/insights", submission);
        Assert.Equal(HttpStatusCode.BadRequest, creatorSelf.StatusCode);

        using var strangerClient = factory.CreateClient();
        strangerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(strangerClient, stranger.Email!));
        (await strangerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/insights", submission)).EnsureSuccessStatusCode();

        using var anon = factory.CreateClient();
        var low = await Json(await anon.GetAsync($"/api/v1/guides/{guideId}/insights"));
        Assert.False(low.GetProperty("meetsKAnonymity").GetBoolean());
        Assert.Equal(2, low.GetProperty("submissionCount").GetInt32());

        for (var i = 0; i < 4; i++)
        {
            var extra = await CreateUser($"vt-ins-extra{i}@example.com");
            using var extraClient = factory.CreateClient();
            extraClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(extraClient, extra.Email!));
            (await extraClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/insights", submission)).EnsureSuccessStatusCode();
        }

        var high = await Json(await anon.GetAsync($"/api/v1/guides/{guideId}/insights"));
        Assert.True(high.GetProperty("meetsKAnonymity").GetBoolean());
        Assert.Equal("JPY", high.GetProperty("average").GetProperty("currencyCode").GetString());
    }

    [Fact]
    public async Task Validation_rejects_short_bodies_and_invalid_currency()
    {
        var creator = await CreateUser("vt-val-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-val-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        Assert.Equal(HttpStatusCode.BadRequest, (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "Other", body = "short" })).StatusCode);
        var huge = new string('x', 4001);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "Other", body = huge })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/insights", new { partySize = 0, tripDays = 5, totalCostMinorUnits = 250000, currencyCode = "JPY" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/insights", new { partySize = 2, tripDays = 5, totalCostMinorUnits = 250000, currencyCode = "JPYX" })).StatusCode);
    }

    [Fact]
    public async Task Badge_revocation_clears_signal()
    {
        var creator = await CreateUser("vt-rev-creator@example.com", creator: true);
        var buyer = await CreateUser("vt-rev-buyer@example.com");
        var admin = await CreateUser("vt-rev-admin@example.com", admin: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var body = new string('v', 60);
        var submitted = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/evidence", new { kind = "Receipt", body });
        submitted.EnsureSuccessStatusCode();
        var evidenceId = (await Json(submitted)).GetProperty("id").GetGuid();

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));
        (await adminClient.PostAsJsonAsync($"/api/v1/admin/evidence/{evidenceId}/review", new { decision = "Approved", reason = "valid" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.DeleteAsync($"/api/v1/admin/guides/{guideId}/badge")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.DeleteAsync($"/api/v1/admin/guides/{guideId}/badge")).StatusCode);

        var badge = await Json(await adminClient.GetAsync($"/api/v1/guides/{guideId}/evidence/badge"));
        Assert.False(badge.GetProperty("verified").GetBoolean());

        await WithDb(async db => Assert.NotNull((await db.VerifiedGuideBadges.AsNoTracking().SingleAsync(x => x.GuideId == guideId)).RevokedAt));
    }

    private async Task<Guid> PublishFreeGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Verified trip guide", subtitle = "Sub", summary = "Summary", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Osaka" }, tags = Array.Empty<string>(), tripDays = 1 });
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
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Verified paid guide", subtitle = "Sub", summary = "Summary", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new { concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(), days = new[] { new { title = "Day", notes = "", nodes = new[] { new { type = "Attraction", name = "Spot", address = "", latitude = 35.0, longitude = 139.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        (await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = new { priceMinorUnits = 5000, currencyCode = "JPY" } })).EnsureSuccessStatusCode();
        return id;
    }

    private async Task GrantEntitlement(Guid guideId, Guid userId) { await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.PurchaseEntitlements.Add(new PurchaseEntitlement { Id = Guid.NewGuid(), GuideId = guideId, UserId = userId, OrderId = Guid.NewGuid(), GrantedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }

    private static async Task<JsonElement> Json(HttpResponseMessage response) { var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }
    private Task<AppUser> CreateUser(string email, bool creator = false, bool admin = false) => CreateUser(factory.Services, email, creator, admin);
    private static async Task<AppUser> CreateUser(IServiceProvider services, string email, bool creator = false, bool admin = false)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true };
        Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
        if (creator) db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active });
        if (admin)
        {
            if (!await roles.RoleExistsAsync(AdminRole)) Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(AdminRole))).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, AdminRole)).Succeeded);
        }
        await db.SaveChangesAsync();
        return user;
    }
    private static async Task<string> Login(HttpClient client, string email) { var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }); response.EnsureSuccessStatusCode(); return (await Json(response)).GetProperty("accessToken").GetString()!; }
    private async Task WithDb(Func<AppDbContext, Task> action) { await using var scope = factory.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
}
