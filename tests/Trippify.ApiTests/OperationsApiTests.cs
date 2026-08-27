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

public sealed class OperationsApiTests(TrippifyFactory factory) : IClassFixture<TrippifyFactory>
{
    private const string Password = "Strong!Pass123";
    private const string AdminRole = "Administrator";

    [Fact]
    public async Task Creator_dashboard_overview_returns_only_own_revenue()
    {
        var creator = await CreateUser("ops-creator@example.com", creator: true);
        var other = await CreateUser("ops-other@example.com", creator: true);
        var buyer = await CreateUser("ops-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var ownGuideId = await PublishPaidGuide(creatorClient);
        var otherGuideId = await PublishPaidGuide(other, other.Email!);
        await GrantEntitlement(ownGuideId, buyer.Id);
        await CreateOrder(ownGuideId, buyer.Id, 2500);
        await CreateOrder(otherGuideId, buyer.Id, 5000);
        var overview = await Json(await creatorClient.GetAsync("/api/v1/creator/dashboard/overview"));
        Assert.Equal(1, overview.GetProperty("guideCount").GetInt32());
        Assert.Equal(1, overview.GetProperty("activeGuideCount").GetInt32());
        Assert.Equal(1, overview.GetProperty("paidOrderCount").GetInt32());
        var revenue = overview.GetProperty("revenue").EnumerateArray().ToArray();
        Assert.Single(revenue);
        Assert.Equal("JPY", revenue[0].GetProperty("currencyCode").GetString());
        Assert.Equal(2500, revenue[0].GetProperty("grossMinorUnits").GetInt64());
    }

    [Fact]
    public async Task Non_creator_user_is_forbidden_from_dashboard_endpoints()
    {
        var user = await CreateUser("ops-noncreator@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/creator/dashboard/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/creator/dashboard/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/creator/dashboard/reviews")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_is_unauthorized_for_admin_endpoints()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/operations/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/operations/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/operations/creators")).StatusCode);
    }

    [Fact]
    public async Task Ordinary_user_is_forbidden_from_admin_endpoints()
    {
        var user = await CreateUser("ops-ordinary@example.com");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, user.Email!));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/operations/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/operations/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/operations/creators")).StatusCode);
    }

    [Fact]
    public async Task Administrator_can_list_audit_users_and_creators_without_private_profile_fields()
    {
        var creator = await CreateUser("ops-admin-creator@example.com", creator: true);
        var admin = await CreateUser("ops-admin@example.com", admin: true);
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        await PublishPaidGuide(creatorClient);
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(adminClient, admin.Email!));

        var usersJson = await (await adminClient.GetAsync("/api/v1/admin/operations/users")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", usersJson, StringComparison.OrdinalIgnoreCase);

        var creatorsJson = await (await adminClient.GetAsync("/api/v1/admin/operations/creators")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("biography", creatorsJson, StringComparison.OrdinalIgnoreCase);

        var audit = await Json(await adminClient.GetAsync("/api/v1/admin/operations/audit"));
        Assert.True(audit.GetProperty("total").GetInt32() >= 0);
    }

    [Fact]
    public async Task Negative_limit_is_clamped_to_one()
    {
        var admin = await CreateUser("ops-clamp@example.com", admin: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, admin.Email!));
        var audit = await Json(await client.GetAsync("/api/v1/admin/operations/audit?limit=-10"));
        Assert.True(audit.GetProperty("items").GetArrayLength() <= 1);
        var huge = await Json(await client.GetAsync("/api/v1/admin/operations/audit?limit=100000"));
        Assert.True(huge.GetProperty("items").GetArrayLength() <= 200);
    }

    [Fact]
    public async Task Creator_review_summary_reports_open_reports_to_the_creator_only()
    {
        var creator = await CreateUser("ops-rev-creator@example.com", creator: true);
        var buyer = await CreateUser("ops-rev-buyer@example.com");
        var reporter = await CreateUser("ops-rev-reporter@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishFreeGuide(creatorClient);
        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var submitted = await buyerClient.PostAsJsonAsync($"/api/v1/guides/{guideId}/reviews", new { rating = 4, body = new string('a', 60) });
        submitted.EnsureSuccessStatusCode();
        var reviewId = (await Json(submitted)).GetProperty("id").GetGuid();
        using var reporterClient = factory.CreateClient();
        reporterClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(reporterClient, reporter.Email!));
        await reporterClient.PostAsJsonAsync($"/api/v1/reviews/{reviewId}/reports", new { reason = "spam" });

        var summary = await Json(await creatorClient.GetAsync("/api/v1/creator/dashboard/reviews"));
        Assert.Equal(1, summary.GetProperty("visibleCount").GetInt32());
        Assert.Equal(0, summary.GetProperty("flaggedCount").GetInt32());
        Assert.True(summary.GetProperty("reportsOpen").GetInt32() >= 1);
    }

    private async Task<Guid> PublishFreeGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Ops free guide", subtitle = "S", summary = "Writable", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Osaka" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new { concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(), days = new[] { new { title = "Day", notes = "", nodes = new[] { new { type = "Attraction", name = "Spot", address = "", latitude = 34.0, longitude = 135.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        (await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = (object?)null })).EnsureSuccessStatusCode();
        return id;
    }

    private async Task<Guid> PublishPaidGuide(HttpClient creatorClient)
        => await PublishPaidGuide(creatorClient, null);

    private async Task<Guid> PublishPaidGuide(AppUser creator, string creatorEmail)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(client, creatorEmail));
        return await PublishPaidGuide(client);
    }

    private async Task<Guid> PublishPaidGuide(HttpClient client, string? _ = null)
    {
        var created = await client.PostAsJsonAsync("/api/v1/guides", new { title = "Ops paid guide", subtitle = "S", summary = "S", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid();
        var structure = new { concurrencyToken = json.GetProperty("concurrencyToken").GetGuid(), days = new[] { new { title = "Day", notes = "", nodes = new[] { new { type = "Attraction", name = "Tower", address = "", latitude = 35.0, longitude = 139.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await client.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        var token = (await Json(await client.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        (await client.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = new { priceMinorUnits = 5000, currencyCode = "JPY" } })).EnsureSuccessStatusCode();
        return id;
    }

    private async Task GrantEntitlement(Guid guideId, Guid userId) { await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.PurchaseEntitlements.Add(new PurchaseEntitlement { Id = Guid.NewGuid(), GuideId = guideId, UserId = userId, OrderId = Guid.NewGuid(), GrantedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }

    private async Task CreateOrder(Guid guideId, Guid buyerId, long amount)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = new GuideOrder { Id = Guid.NewGuid(), GuideId = guideId, BuyerUserId = buyerId, Status = OrderStatus.Paid, AmountMinorUnits = amount, CurrencyCode = "JPY", CheckoutReference = $"ref-{Guid.NewGuid()}", CreatedAt = DateTimeOffset.UtcNow };
        db.GuideOrders.Add(order);
        await db.SaveChangesAsync();
    }

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
}
