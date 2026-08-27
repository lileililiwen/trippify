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

public sealed class CommercialRemixesApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";
    private const string AdminRole = "Administrator";

    [Fact]
    public async Task Creator_can_upsert_own_license_policy_and_public_endpoint_returns_commercial_only()
    {
        var creator = await CreateUser("cx-creator@example.com", creator: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var slug = await WithDb(async db => (await db.CreatorProfiles.AsNoTracking().SingleAsync(c => c.UserId == creator.Id)).Slug);
        var resp = await creatorClient.PostAsJsonAsync("/api/v1/me/license-policies", new
        {
            slug,
            displayName = "Default Policy",
            allowCommercial = true,
            requireApproval = true,
            royaltyPercent = 30,
        });
        resp.EnsureSuccessStatusCode();

        using var anon = factory.CreateClient();
        var list = await Json(anon, $"/api/v1/creators/{slug}/license");
        Assert.True(list.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Ancestry_requires_license_policy_and_decides_when_no_approval_required()
    {
        var creator = await CreateUser("cx-ancestor-creator@example.com", creator: true);
        var other = await CreateUser("cx-ancestor-other@example.com", creator: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        using var otherClient = factory.CreateClient();
        otherClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(otherClient, other.Email!));

        var parentId = await PublishFreeGuide(creatorClient);
        var childId = await PublishFreeGuide(otherClient);

        var policyResp = await otherClient.PostAsJsonAsync("/api/v1/me/license-policies", new { slug = "shared", displayName = "Shared", allowCommercial = true, requireApproval = false, royaltyPercent = 25 });
        policyResp.EnsureSuccessStatusCode();
        var policyId = (await Json(policyResp)).GetProperty("id").GetGuid();

        var ancestry = await otherClient.PostAsJsonAsync($"/api/v1/guides/{childId}/remix/ancestry", new { parentGuideId = parentId, licensePolicyId = policyId, attributionJson = "Derived from parent" });
        ancestry.EnsureSuccessStatusCode();
        var detail = await Json(ancestry);
        Assert.Equal("Approved", detail.GetProperty("decision").GetString());

        var orphan = await otherClient.PostAsJsonAsync($"/api/v1/guides/{childId}/remix/ancestry", new { parentGuideId = parentId, licensePolicyId = policyId });
        Assert.Equal(HttpStatusCode.Conflict, orphan.StatusCode);

        var third = await otherClient.PostAsJsonAsync($"/api/v1/guides/{parentId}/remix/ancestry", new { parentGuideId = childId, licensePolicyId = policyId });
        Assert.Equal(HttpStatusCode.Forbidden, third.StatusCode);
    }

    [Fact]
    public async Task Revenue_shares_must_total_amount_and_percent_for_an_order()
    {
        var creator = await CreateUser("cx-rev-creator@example.com", creator: true);
        var buyer = await CreateUser("cx-rev-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var paidGuideId = await PublishPaidGuide(creatorClient);
        Guid orderId = Guid.Empty;
        await WithDb(async db =>
        {
            db.GuideOrders.Add(new GuideOrder
            {
                Id = Guid.NewGuid(),
                GuideId = paidGuideId,
                BuyerUserId = buyer.Id,
                Status = OrderStatus.Paid,
                AmountMinorUnits = 1000,
                CurrencyCode = "JPY",
                CheckoutReference = "ref-x",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            orderId = await db.GuideOrders.AsNoTracking().OrderByDescending(x => x.CreatedAt).Select(x => x.Id).FirstAsync();
        });

        using var adminClient = factory.CreateClient();
        var admin = await CreateUser("cx-rev-admin@example.com", admin: true);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var partial = await adminClient.PostAsJsonAsync("/api/v1/admin/revenue-shares", new
        {
            orderId,
            shares = new[] { new { userId = creator.Id, percent = 50, amountMinorUnits = 500, currencyCode = "JPY" }, new { userId = buyer.Id, percent = 50, amountMinorUnits = 500, currencyCode = "JPY" } },
        });
        partial.EnsureSuccessStatusCode();
        var list = await Json(adminClient, $"/api/v1/admin/revenue-shares?orderId={orderId}");
        Assert.Equal(2, list.GetArrayLength());

        var mismatch = await adminClient.PostAsJsonAsync("/api/v1/admin/revenue-shares", new
        {
            orderId,
            shares = new[] { new { userId = creator.Id, percent = 50, amountMinorUnits = 400, currencyCode = "JPY" }, new { userId = buyer.Id, percent = 50, amountMinorUnits = 500, currencyCode = "JPY" } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
    }

    [Fact]
    public async Task Approval_queue_returns_pending_only_and_decision_is_idempotent()
    {
        var creator = await CreateUser("cx-app-creator@example.com", creator: true);
        var creatorB = await CreateUser("cx-app-other@example.com", creator: true);
        var admin = await CreateUser("cx-app-admin@example.com", admin: true);
        using var creatorA = factory.CreateClient();
        creatorA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorA, creator.Email!));
        using var creatorBC = factory.CreateClient();
        creatorBC.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorBC, creatorB.Email!));
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var parentId = await PublishFreeGuide(creatorA);
        var childId = await PublishFreeGuide(creatorBC);
        var policyResp = await creatorBC.PostAsJsonAsync("/api/v1/me/license-policies", new { slug = "needsApproval", displayName = "Needs", allowCommercial = true, requireApproval = true, royaltyPercent = 25 });
        policyResp.EnsureSuccessStatusCode();
        var policyId = (await Json(policyResp)).GetProperty("id").GetGuid();
        var ancestryResp = await creatorBC.PostAsJsonAsync($"/api/v1/guides/{childId}/remix/ancestry", new { parentGuideId = parentId, licensePolicyId = policyId });
        ancestryResp.EnsureSuccessStatusCode();
        var ancestryId = (await Json(ancestryResp)).GetProperty("id").GetGuid();

        var queue = await Json(adminClient, "/api/v1/admin/remix-approvals/queue");
        Assert.True(queue.GetArrayLength() >= 1);

        var decide = await adminClient.PostAsJsonAsync($"/api/v1/admin/remix-approvals/{ancestryId}/decide", new { decision = "Approved", reason = "ok" });
        decide.EnsureSuccessStatusCode();
        var decideAgain = await adminClient.PostAsJsonAsync($"/api/v1/admin/remix-approvals/{ancestryId}/decide", new { decision = "Rejected", reason = "too late" });
        decideAgain.EnsureSuccessStatusCode();
        var final = await Json(adminClient, $"/api/v1/guides/{childId}/ancestry");
        Assert.Equal("Approved", final.GetProperty("decision").GetString());
    }

    private async Task<T> WithDb<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
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

    private async Task<Guid> PublishFreeGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Cx guide", subtitle = "S", summary = "S", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Osaka" }, tags = Array.Empty<string>(), tripDays = 1 });
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
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Cx paid", subtitle = "S", summary = "S", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new { concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(), days = new[] { new { title = "Day", notes = "", nodes = new[] { new { type = "Attraction", name = "Tower", address = "", latitude = 35.0, longitude = 139.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        (await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = new { priceMinorUnits = 5000, currencyCode = "JPY" } })).EnsureSuccessStatusCode();
        return id;
    }

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

    private static async Task<string> Login(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString()!;
    }
}
