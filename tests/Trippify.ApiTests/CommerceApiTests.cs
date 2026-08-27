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

public class CommerceFactory : WebApplicationFactory<Program>
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
    public string ProviderName => "fake";
    public Task<CheckoutSession> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new CheckoutSession($"https://example.test/checkout/{Guid.NewGuid():N}", "cs_test_" + Guid.NewGuid().ToString("N"), request.AmountMinorUnits, request.CurrencyCode));
}

public sealed class FakeVerifier : IPaymentWebhookVerifier
{
    public string ProviderName => "fake";
    public bool VerifySignature(ReadOnlySpan<byte> rawBody, IDictionary<string, string> headers, string expectedSecret)
    {
        if (!headers.TryGetValue("X-Fake-Signature", out var provided)) return false;
        var expected = HttpPaymentGateway.Sign(expectedSecret, rawBody);
        var match = string.Equals(expected, provided, StringComparison.OrdinalIgnoreCase);
        return match;
    }
}

public sealed class SignedCommerceFactory : CommerceFactory
{
    public string Secret { get; } = "test-webhook-secret";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPaymentWebhookVerifier>();
            services.AddSingleton<IPaymentWebhookVerifier>(new FakeVerifier());
        });
    }
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

    [Fact]
    public async Task Checkout_returns_url_and_provider_reference_for_redirect()
    {
        var creator = await CreateUser("pay-url-creator@example.com", creator: true);
        var buyer = await CreateUser("pay-url-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);

        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var response = await buyerClient.PostAsJsonAsync("/api/v1/commerce/checkout", new { guideId });
        response.EnsureSuccessStatusCode();
        var json = await Json(response);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("checkoutUrl").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("checkoutReference").GetString()));
        Assert.Equal("fake", json.GetProperty("providerName").GetString());

        await WithDb(async db =>
        {
            var order = await db.GuideOrders.SingleAsync(o => o.CheckoutReference == json.GetProperty("checkoutReference").GetString());
            Assert.False(string.IsNullOrEmpty(order.ProviderReference));
            Assert.Equal("fake", order.ProviderName);
        });
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

public sealed class SignedCommerceApiTests : IClassFixture<SignedCommerceFactory>
{
    private const string Password = "Strong!Pass123";
    private const string Secret = "test-webhook-secret";
    private readonly SignedCommerceFactory _factory;

    public SignedCommerceApiTests(SignedCommerceFactory factory) { _factory = factory; }

    [Fact]
    public async Task Signed_webhook_pays_order_then_replay_is_idempotent()
    {
        var creator = await CreateUser("signed-creator@example.com", creator: true);
        var buyer = await CreateUser("signed-buyer@example.com");
        using var creatorClient = _factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);

        using var buyerClient = _factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var checkout = await buyerClient.PostAsJsonAsync("/api/v1/commerce/checkout", new { guideId });
        checkout.EnsureSuccessStatusCode();
        var reference = (await Json(checkout)).GetProperty("checkoutReference").GetString()!;

        var first = await PostSignedWebhook(buyerClient, "{\"eventId\":\"evt_s1\",\"type\":\"payment.paid\",\"checkoutReference\":\"" + reference + "\"}");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var replay = await PostSignedWebhook(buyerClient, "{\"eventId\":\"evt_s1\",\"type\":\"payment.paid\",\"checkoutReference\":\"" + reference + "\"}");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True((await Json(replay)).GetProperty("replayed").GetBoolean());

        await WithDb(async db =>
        {
            Assert.Single(await db.GuideOrders.AsNoTracking().Where(o => o.CheckoutReference == reference && o.Status == OrderStatus.Paid).ToListAsync());
            var order = await db.GuideOrders.SingleAsync(o => o.CheckoutReference == reference);
            Assert.Equal(3, await db.CommerceLedgerEntries.CountAsync(x => x.OrderId == order.Id));
        });
    }

    [Fact]
    public async Task Signed_webhook_with_tampered_body_rejects_without_recording()
    {
        var creator = await CreateUser("signed-tamper-creator@example.com", creator: true);
        var buyer = await CreateUser("signed-tamper-buyer@example.com");
        using var creatorClient = _factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);

        using var buyerClient = _factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var checkout = await buyerClient.PostAsJsonAsync("/api/v1/commerce/checkout", new { guideId });
        checkout.EnsureSuccessStatusCode();
        var reference = (await Json(checkout)).GetProperty("checkoutReference").GetString()!;

        var original = "{\"eventId\":\"evt_t1\",\"type\":\"payment.paid\",\"checkoutReference\":\"" + reference + "\"}";
        var tampered = "{\"eventId\":\"evt_t1\",\"type\":\"payment.refunded\",\"checkoutReference\":\"" + reference + "\"}";
        var signature = HttpPaymentGateway.Sign(Secret, System.Text.Encoding.UTF8.GetBytes(original));
        buyerClient.DefaultRequestHeaders.Remove("X-Fake-Signature");
        buyerClient.DefaultRequestHeaders.Add("X-Fake-Signature", signature);
        var tamperedResponse = await buyerClient.PostAsync("/api/v1/commerce/webhook", new StringContent(tampered, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, tamperedResponse.StatusCode);

        await WithDb(async db =>
        {
            var order = await db.GuideOrders.SingleAsync(o => o.CheckoutReference == reference);
            Assert.Equal(OrderStatus.Pending, order.Status);
        });
    }

    [Fact]
    public async Task Signed_webhook_without_signature_rejects()
    {
        var creator = await CreateUser("signed-missing-creator@example.com", creator: true);
        var buyer = await CreateUser("signed-missing-buyer@example.com");
        using var creatorClient = _factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);

        using var buyerClient = _factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));
        var checkout = await buyerClient.PostAsJsonAsync("/api/v1/commerce/checkout", new { guideId });
        checkout.EnsureSuccessStatusCode();
        var reference = (await Json(checkout)).GetProperty("checkoutReference").GetString()!;

        buyerClient.DefaultRequestHeaders.Remove("X-Fake-Signature");
        var response = await buyerClient.PostAsync("/api/v1/commerce/webhook", new StringContent("{\"eventId\":\"evt_n1\",\"type\":\"payment.paid\",\"checkoutReference\":\"" + reference + "\"}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HttpPaymentGateway_returns_session_and_url()
    {
        var options = new PaymentProviderOptions { Provider = "http", Endpoint = "https://payments.example.invalid", ApiKey = "test-key", WebhookSecret = "webhook-secret", Enabled = true, TimeoutMilliseconds = 5000 };
        using var handler = new CheckoutHandler("{\"sessionId\":\"cs_test_123\",\"url\":\"https://payments.example.invalid/cs_test_123\"}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri(options.Endpoint!), Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds) };
        var gateway = new HttpPaymentGateway(options, http);
        var request2 = new PaymentCheckoutRequest(2500, "JPY", Guid.NewGuid(), Guid.NewGuid(), null, "/success", "/cancel", Guid.NewGuid().ToString("N"));
        var session = await gateway.CreateCheckoutAsync(request2, default);
        Assert.Equal("cs_test_123", session.Reference);
        Assert.Equal("https://payments.example.invalid/cs_test_123", session.Url);
        Assert.Equal(1, handler.Requests);
    }

    [Fact]
    public async Task HttpPaymentGateway_translates_5xx_to_transient_io_exception()
    {
        var options = new PaymentProviderOptions { Provider = "http", Endpoint = "https://payments.example.invalid", ApiKey = "test-key", WebhookSecret = "webhook-secret", Enabled = true, TimeoutMilliseconds = 5000 };
        using var handler = new CheckoutHandler("", HttpStatusCode.ServiceUnavailable);
        using var http = new HttpClient(handler) { BaseAddress = new Uri(options.Endpoint!), Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds) };
        var gateway = new HttpPaymentGateway(options, http);
        var request2 = new PaymentCheckoutRequest(2500, "JPY", Guid.NewGuid(), Guid.NewGuid(), null, "/success", "/cancel", Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAsync<IOException>(() => gateway.CreateCheckoutAsync(request2, default));
    }

    private static async Task<HttpResponseMessage> PostSignedWebhook(HttpClient client, string rawBody)
    {
        var signature = HttpPaymentGateway.Sign(Secret, System.Text.Encoding.UTF8.GetBytes(rawBody));
        client.DefaultRequestHeaders.Remove("X-Fake-Signature");
        client.DefaultRequestHeaders.Add("X-Fake-Signature", signature);
        return await client.PostAsync("/api/v1/commerce/webhook", new StringContent(rawBody, System.Text.Encoding.UTF8, "application/json"));
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

    private static async Task<JsonElement> Json(HttpResponseMessage response) { var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }
    private Task<AppUser> CreateUser(string email, bool creator = false) => CreateUser(_factory.Services, email, creator);
    private static async Task<AppUser> CreateUser(IServiceProvider services, string email, bool creator = false) { await using var scope = services.CreateAsyncScope(); var manager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true }; Assert.True((await manager.CreateAsync(user, Password)).Succeeded); if (creator) db.CreatorProfiles.Add(new CreatorProfile { UserId = user.Id, Slug = user.Id.ToString("N"), Status = CreatorStatus.Active }); await db.SaveChangesAsync(); return user; }
    private static async Task<string> Login(HttpClient client, string email) { var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = Password }); response.EnsureSuccessStatusCode(); return (await Json(response)).GetProperty("accessToken").GetString()!; }
    private async Task WithDb(Func<AppDbContext, Task> action) { await using var scope = _factory.Services.CreateAsyncScope(); await action(scope.ServiceProvider.GetRequiredService<AppDbContext>()); }
}

internal sealed class CheckoutHandler : HttpMessageHandler
{
    private readonly string _body;
    private readonly HttpStatusCode _status;
    public int Requests { get; private set; }
    public CheckoutHandler(string body, HttpStatusCode status = HttpStatusCode.OK) { _body = body; _status = status; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests++;
        return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
    }
}

