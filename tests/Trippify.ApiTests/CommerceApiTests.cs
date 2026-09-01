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
    private readonly object _gate = new();
    private readonly Dictionary<string, CheckoutSession> _byKey = new(StringComparer.Ordinal);
    private int _calls;
    public int CallCount { get { lock (_gate) return _calls; } }
    public Task<CheckoutSession> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _calls++;
            if (_byKey.TryGetValue(request.IdempotencyKey, out var existing)) return Task.FromResult(existing);
        }
        var reference = "cs_test_" + Guid.NewGuid().ToString("N");
        var session = new CheckoutSession($"https://example.test/checkout/{reference}", reference, request.AmountMinorUnits, request.CurrencyCode);
        lock (_gate) _byKey[request.IdempotencyKey] = session;
        return Task.FromResult(session);
    }
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

    [Fact]
    public async Task Same_idempotency_key_retry_returns_original_order_without_calling_provider_again()
    {
        var creator = await CreateUser("idem-creator@example.com", creator: true);
        var buyer = await CreateUser("idem-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);
        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));

        var key = "buyer-checkout-001-aaaaaa";
        var first = await SendCheckout(buyerClient, guideId, key); first.EnsureSuccessStatusCode();
        var firstJson = await Json(first);
        var firstOrderId = firstJson.GetProperty("orderId").GetGuid();
        var firstReference = firstJson.GetProperty("checkoutReference").GetString()!;

        var second = await SendCheckout(buyerClient, guideId, key); second.EnsureSuccessStatusCode();
        var secondJson = await Json(second);
        Assert.Equal(firstOrderId, secondJson.GetProperty("orderId").GetGuid());
        Assert.Equal(firstReference, secondJson.GetProperty("checkoutReference").GetString());

        await WithDb(async db =>
        {
            Assert.Equal(1, await db.GuideOrders.CountAsync(o => o.GuideId == guideId && o.BuyerUserId == buyer.Id));
            Assert.Equal(1, await db.CheckoutIdempotencyKeys.CountAsync(k => k.BuyerUserId == buyer.Id && k.Scope == "checkout" && k.Key == key));
        });
    }

    [Fact]
    public async Task Conflicting_idempotency_key_reuse_returns_409_and_does_not_create_second_order()
    {
        var creator = await CreateUser("idem-conflict-creator@example.com", creator: true);
        var buyer = await CreateUser("idem-conflict-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);
        var otherGuide = await PublishPaidGuide(creatorClient, "Other island", 1800, "JPY");
        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));

        var key = "buyer-checkout-conflict-aaaaaa";
        var first = await SendCheckout(buyerClient, guideId, key); first.EnsureSuccessStatusCode();
        var conflict = await SendCheckout(buyerClient, otherGuide, key);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        await WithDb(async db =>
        {
            Assert.Equal(1, await db.GuideOrders.CountAsync(o => o.BuyerUserId == buyer.Id));
        });
    }

    [Fact]
    public async Task Invalid_idempotency_key_format_returns_400()
    {
        var creator = await CreateUser("idem-bad-creator@example.com", creator: true);
        var buyer = await CreateUser("idem-bad-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);
        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commerce/checkout") { Content = JsonContent.Create(new { guideId }) };
        request.Headers.Add("Idempotency-Key", "short");
        var response = await buyerClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await WithDb(async db => Assert.Equal(0, await db.GuideOrders.CountAsync(o => o.BuyerUserId == buyer.Id)));
    }

    [Fact]
    public async Task Concurrent_checkout_requests_with_same_key_create_one_order()
    {
        var creator = await CreateUser("idem-conc-creator@example.com", creator: true);
        var buyer = await CreateUser("idem-conc-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);
        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));

        var key = "buyer-checkout-concurrent-aaaa";
        async Task<HttpResponseMessage> Send() => await SendCheckout(buyerClient, guideId, key);
        var first = await Send(); first.EnsureSuccessStatusCode();
        var firstOrderId = (await Json(first)).GetProperty("orderId").GetGuid();
        var firstReference = (await Json(first)).GetProperty("checkoutReference").GetString()!;

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Send()));
        Assert.All(responses, r => r.EnsureSuccessStatusCode());
        foreach (var r in responses)
        {
            var v = await Json(r);
            Assert.Equal(firstOrderId, v.GetProperty("orderId").GetGuid());
            Assert.Equal(firstReference, v.GetProperty("checkoutReference").GetString());
        }

        await WithDb(async db =>
        {
            Assert.Equal(1, await db.GuideOrders.CountAsync(o => o.BuyerUserId == buyer.Id && o.GuideId == guideId));
            Assert.Equal(1, await db.CheckoutIdempotencyKeys.CountAsync(k => k.BuyerUserId == buyer.Id && k.Key == key));
        });
    }

    [Fact]
    public async Task Duplicate_paid_webhook_yields_single_entitlement_and_three_ledger_rows()
    {
        var creator = await CreateUser("replay-creator@example.com", creator: true);
        var buyer = await CreateUser("replay-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);
        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));

        var checkout = await buyerClient.PostAsJsonAsync("/api/v1/commerce/checkout", new { guideId });
        checkout.EnsureSuccessStatusCode();
        var reference = (await Json(checkout)).GetProperty("checkoutReference").GetString()!;
        var webhookBody = new { eventId = "evt_replay_1", type = "payment.paid", checkoutReference = reference };

        var first = await PostWebhook(buyerClient, webhookBody); first.EnsureSuccessStatusCode();
        var second = await PostWebhook(buyerClient, webhookBody); second.EnsureSuccessStatusCode();
        var third = await PostWebhook(buyerClient, webhookBody); third.EnsureSuccessStatusCode();
        Assert.False((await Json(first)).TryGetProperty("replayed", out _));
        Assert.True((await Json(second)).GetProperty("replayed").GetBoolean());
        Assert.True((await Json(third)).GetProperty("replayed").GetBoolean());

        await WithDb(async db =>
        {
            var order = await db.GuideOrders.SingleAsync(o => o.CheckoutReference == reference);
            Assert.Equal(OrderStatus.Paid, order.Status);
            Assert.Equal(1, await db.PurchaseEntitlements.CountAsync(e => e.OrderId == order.Id));
            Assert.Equal(3, await db.CommerceLedgerEntries.CountAsync(l => l.OrderId == order.Id));
            Assert.Equal(1, await db.PaymentWebhookEvents.CountAsync(e => e.EventId == "evt_replay_1"));
        });
    }

    [Fact]
    public async Task Idempotency_keys_are_scoped_to_buyer()
    {
        var creator = await CreateUser("idem-scope-creator@example.com", creator: true);
        var buyerA = await CreateUser("idem-scope-buyerA@example.com");
        var buyerB = await CreateUser("idem-scope-buyerB@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);
        using var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(clientA, buyerA.Email!));
        using var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(clientB, buyerB.Email!));

        var key = "shared-key-scope-1234";
        var aResponse = await SendCheckout(clientA, guideId, key); aResponse.EnsureSuccessStatusCode();
        var bResponse = await SendCheckout(clientB, guideId, key); bResponse.EnsureSuccessStatusCode();
        Assert.NotEqual((await Json(aResponse)).GetProperty("orderId").GetGuid(), (await Json(bResponse)).GetProperty("orderId").GetGuid());

        await WithDb(async db =>
        {
            Assert.Equal(2, await db.GuideOrders.CountAsync(o => o.GuideId == guideId));
            Assert.Equal(2, await db.CheckoutIdempotencyKeys.CountAsync(k => k.Key == key));
        });
    }

    [Fact]
    public async Task Same_idempotency_key_retry_after_provider_timeout_still_returns_original_order()
    {
        var creator = await CreateUser("idem-timeout-creator@example.com", creator: true);
        var buyer = await CreateUser("idem-timeout-buyer@example.com");
        using var creatorClient = factory.CreateClient();
        creatorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(creatorClient, creator.Email!));
        var guideId = await PublishPaidGuide(creatorClient);
        using var buyerClient = factory.CreateClient();
        buyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(buyerClient, buyer.Email!));

        var key = "buyer-checkout-timeout-001";
        var first = await SendCheckout(buyerClient, guideId, key); first.EnsureSuccessStatusCode();
        var firstJson = await Json(first);
        var firstOrderId = firstJson.GetProperty("orderId").GetGuid();

        var retry = await SendCheckout(buyerClient, guideId, key); retry.EnsureSuccessStatusCode();
        var retryJson = await Json(retry);
        Assert.Equal(firstOrderId, retryJson.GetProperty("orderId").GetGuid());
        Assert.Equal(firstJson.GetProperty("checkoutReference").GetString(), retryJson.GetProperty("checkoutReference").GetString());

        await WithDb(async db =>
        {
            var orders = await db.GuideOrders.Where(o => o.BuyerUserId == buyer.Id).ToListAsync();
            Assert.Single(orders);
        });
    }

    private static async Task<HttpResponseMessage> SendCheckout(HttpClient client, Guid guideId, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/commerce/checkout") { Content = JsonContent.Create(new { guideId }) };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostWebhook(HttpClient client, object body, string secret = Secret)
    {
        client.DefaultRequestHeaders.Remove("X-Webhook-Secret");
        client.DefaultRequestHeaders.Add("X-Webhook-Secret", secret);
        return await client.PostAsJsonAsync("/api/v1/commerce/webhook", body);
    }

    private async Task<Guid> PublishPaidGuide(HttpClient creatorClient, string title = "Paid nights", long priceMinorUnits = 2500, string currencyCode = "JPY")
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title, subtitle = "Sub", summary = "Worth buying once", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid(); var token = json.GetProperty("concurrencyToken").GetGuid();
        var structure = new { concurrencyToken = token, days = new[] { new { title = "Day one", notes = "", nodes = new[] { new { type = "Attraction", name = "Tower", address = "", latitude = 34.0, longitude = 135.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        var published = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = new { priceMinorUnits, currencyCode } });
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

    private async Task<Guid> PublishPaidGuide(HttpClient creatorClient, string title = "Paid nights", long priceMinorUnits = 2500, string currencyCode = "JPY")
    {
        var created = await creatorClient.PostAsJsonAsync("/api/v1/guides", new { title, subtitle = "Sub", summary = "Worth buying once", coverUrl = (string?)null, countryCode = "JP", cities = new[] { "Tokyo" }, tags = Array.Empty<string>(), tripDays = 1 });
        created.EnsureSuccessStatusCode();
        var json = await Json(created); var id = json.GetProperty("id").GetGuid(); var token = json.GetProperty("concurrencyToken").GetGuid();
        var structure = new { concurrencyToken = token, days = new[] { new { title = "Day one", notes = "", nodes = new[] { new { type = "Attraction", name = "Tower", address = "", latitude = 34.0, longitude = 135.0, arrivalTime = (string?)null, departureTime = (string?)null, stayMinutes = 60, ticketInformation = (string?)null, reservationInformation = (string?)null, openingHours = (string?)null, notes = "" } } } }, sections = Array.Empty<object>() };
        (await creatorClient.PutAsJsonAsync($"/api/v1/guides/{id}/structure", structure)).EnsureSuccessStatusCode();
        token = (await Json(await creatorClient.GetAsync($"/api/v1/guides/{id}"))).GetProperty("concurrencyToken").GetGuid();
        var published = await creatorClient.PostAsJsonAsync($"/api/v1/guides/{id}/publish", new { concurrencyToken = token, pricing = new { priceMinorUnits, currencyCode } });
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

