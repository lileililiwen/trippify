using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trippify.Application;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class CommerceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Commerce:WebhookSecret", "test-webhook-secret");
        builder.ConfigureServices(services =>
        {
            var databaseName = "trippify-commerce-tests-" + Guid.NewGuid();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway, FakeGateway>();
        });
    }
}

public sealed class FakeGateway : IPaymentGateway
{
    public Task<string> CreateCheckoutAsync(long minorUnits, string currency, CancellationToken cancellationToken) => Task.FromResult("cs_test_" + Guid.NewGuid().ToString("N"));
}

public sealed class CommerceApiTests(CommerceFactory factory) : IClassFixture<CommerceFactory>
{
    private const string Password = "Strong!Pass123";
    private const string Secret = "test-webhook-secret";

    [Fact]
    public async Task Double_webhook_yields_one_paid_order_one_entitlement_and_unlocks_content()
    {
        var creator = await CreateUser("commerce-creator@example.com", creator: true);
        var buyer = await CreateUser("commerce-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);
        var discount = await creatorClient.PostAsJsonAsync($"/api/v1/commerce/guides/{guideId}/discounts", new { code = "LAUNCH25", percentOff = 25 }); discount.EnsureSuccessStatusCode();

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var checkout = await buyerClient.PostAsJsonAsync("/api/v1/commerce/checkout", new { guideId, discountCode = "launch25" }); checkout.EnsureSuccessStatusCode();
        var checkoutJson = await Json(checkout);
        Assert.Equal(1875, checkoutJson.GetProperty("amountMinorUnits").GetInt64());
        var reference = checkoutJson.GetProperty("checkoutReference").GetString()!;

        var slug = await SlugOf(guideId);
        var preview = await Json(await buyerClient.GetAsync($"/api/v1/discovery/guides/{slug}"));
        Assert.False(preview.GetProperty("unlocked").GetBoolean());
        Assert.False(preview.GetProperty("days")[0].GetProperty("nodes")[0].TryGetProperty("latitude", out _));

        var webhookBody = new { eventId = "evt_1", type = "payment.paid", checkoutReference = reference };
        var first = await PostWebhook(buyerClient, webhookBody); first.EnsureSuccessStatusCode();
        var second = await PostWebhook(buyerClient, webhookBody); second.EnsureSuccessStatusCode();
        Assert.True((await Json(second)).GetProperty("replayed").GetBoolean());

        var orders = await Json(await buyerClient.GetAsync("/api/v1/commerce/orders"));
        Assert.Equal(1, orders.GetArrayLength());
        Assert.Equal("Paid", orders[0].GetProperty("status").GetString());
        var entitlements = await Json(await buyerClient.GetAsync("/api/v1/commerce/entitlements"));
        Assert.Equal(1, entitlements.GetArrayLength());

        await WithDb(async db =>
        {
            Assert.Equal(1, await db.PurchaseEntitlements.CountAsync(x => x.GuideId == guideId));
            var ledger = await db.CommerceLedgerEntries.Where(x => x.OrderId == orders[0].GetProperty("id").GetGuid()).ToListAsync();
            Assert.Equal(3, ledger.Count);
            Assert.Contains(ledger, x => x.Kind == LedgerKind.Commission && x.CommissionRateSnapshot == 0.10m && x.AmountMinorUnits == 188);
            Assert.Contains(ledger, x => x.Kind == LedgerKind.CreatorNet && x.AmountMinorUnits == 1687);
        });

        var unlocked = await Json(await buyerClient.GetAsync($"/api/v1/discovery/guides/{slug}"));
        Assert.True(unlocked.GetProperty("unlocked").GetBoolean());
        Assert.True(unlocked.GetProperty("days")[0].GetProperty("nodes")[0].TryGetProperty("latitude", out _));
        Assert.False(unlocked.TryGetProperty("purchase", out _));
    }

    [Fact]
    public async Task Refund_revokes_access_and_anonymous_and_owner_paths_stay_protected()
    {
        var creator = await CreateUser("refund-creator@example.com", creator: true);
        var buyer = await CreateUser("refund-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/commerce/checkout", new { guideId })).StatusCode);
        var ownPurchase = await creatorClient.PostAsJsonAsync("/api/v1/commerce/checkout", new { guideId });
        Assert.Equal(HttpStatusCode.BadRequest, ownPurchase.StatusCode);
        var checkout = await buyerClient.PostAsJsonAsync("/api/v1/commerce/checkout", new { guideId }); checkout.EnsureSuccessStatusCode();
        var reference = (await Json(checkout)).GetProperty("checkoutReference").GetString()!;
        (await PostWebhook(buyerClient, new { eventId = "evt_r0", type = "payment.paid", checkoutReference = reference })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWebhook(buyerClient, new { eventId = "evt_r1", type = "payment.refunded", checkoutReference = reference }, secret: "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostWebhook(buyerClient, new { eventId = "evt_r2", type = "payment.refunded", checkoutReference = "cs_unknown" })).StatusCode);
        var refund = await PostWebhook(buyerClient, new { eventId = "evt_r3", type = "payment.refunded", checkoutReference = reference }); refund.EnsureSuccessStatusCode();
        var refundReplay = await PostWebhook(buyerClient, new { eventId = "evt_r4", type = "payment.refunded", checkoutReference = reference }); refundReplay.EnsureSuccessStatusCode();

        await WithDb(async db =>
        {
            var order = await db.GuideOrders.SingleAsync(o => o.CheckoutReference == reference);
            Assert.Equal(OrderStatus.Refunded, order.Status);
            Assert.True(await db.PurchaseEntitlements.Where(x => x.OrderId == order.Id).AllAsync(x => x.RevokedAt != null));
            Assert.Contains(await db.CommerceLedgerEntries.Where(x => x.OrderId == order.Id).Select(x => x.Kind).ToListAsync(), kind => kind == LedgerKind.Refund);
        });
        var entitlements = await Json(await buyerClient.GetAsync("/api/v1/commerce/entitlements"));
        Assert.Equal(0, entitlements.GetArrayLength());
    }

    [Fact]
    public async Task Gateway_failure_returns_unavailable_without_persisting_orders()
    {
        using var local = new TrippifyFactory();
        var creator = await CreateUser(local.Services, "gateway-creator@example.com", creator: true);
        var buyer = await CreateUser(local.Services, "gateway-buyer@example.com");
        using var creatorClient = local.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);
        using var buyerClient = local.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var response = await buyerClient.PostAsJsonAsync("/api/v1/commerce/checkout", new { guideId });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var scope = local.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().GuideOrders.AnyAsync());
    }

    private static async Task<HttpResponseMessage> PostWebhook(HttpClient client, object body, string secret = Secret)
    {
        client.DefaultRequestHeaders.Remove("X-Webhook-Secret");
        client.DefaultRequestHeaders.Add("X-Webhook-Secret", secret);
        return await client.PostAsJsonAsync("/api/v1/commerce/webhook", body);
    }

    private async Task<Guid> PublishPaidGuide(HttpClient creatorClient)
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title = "Paid nights", subtitle = "Sub", summary = "Worth buying once", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid(); var token = json.GetProperty("concurrencyToken").GetGuid();
        var structure = new { concurrencyToken = token, days = new[] { new { title = "Night one", notes = "", nodes = new[] { new { type = "Attraction", name = "Tower", address = "", latitude = 34.0, longitude = 135.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        var published = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = new { priceMinorUnits = 2500, currencyCode = "JPY" } });
        published.EnsureSuccessStatusCode();
        return id;
    }

    private async Task<string> SlugOf(Guid guideId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().TravelGuides.SingleAsync(x => x.Id == guideId)).Slug;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) { var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }
    private Task<AppUser> CreateUser(string email, bool creator = false) => CreateUser(factory.Services, email, creator);
    private static async Task<AppUser> CreateUser(IServiceProvider services, string email, bool creator = false) { await using var scope = services.CreateAsyncScope(); var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true }; Assert.True((await manager.CreateAsync(user, Password)).Succeeded); if (creator) db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active }); await db.SaveChangesAsync(); return user; }
    private static async Task<string> Login(HttpClient client, string email) { var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }); response.EnsureSuccessStatusCode(); return (await Json(response)).GetProperty("accessToken").GetString()!; }
    private async Task WithDb(Func<AppDbContext, Task> action) { await using var scope = factory.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
}

