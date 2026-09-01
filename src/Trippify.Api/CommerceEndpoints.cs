using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class CommerceEndpoints
{
    private const string CheckoutScope = "checkout";
    private static readonly Meter CommerceMeter = new("Trippify.Commerce");
    private static readonly Counter<long> CommerceCommands = CommerceMeter.CreateCounter<long>("trippify.commerce.commands");
    public static void MapCommerce(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/commerce").RequireAuthorization();
        group.MapPost("/checkout", Checkout);
        group.MapGet("/orders", MyOrders);
        group.MapGet("/entitlements", MyEntitlements);
        group.MapPost("/guides/{guideId:guid}/discounts", CreateDiscount);
        app.MapPost("/api/v1/commerce/webhook", Webhook);
    }

    private static async Task<IResult> Checkout(HttpRequest httpRequest, CheckoutRequest request, ClaimsPrincipal principal, AppDbContext db, IPaymentGateway gateway, IConfiguration configuration, IClock clock)
    {
        var buyer = IdentityEndpoints.CurrentUserId(principal);
        var guide = await db.TravelGuides.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.GuideId);
        if (guide is null || guide.Lifecycle != GuideLifecycle.Paid) return Results.NotFound();
        if (guide.OwnerUserId == buyer) return Results.ValidationProblem(new Dictionary<string, string[]> { ["guide"] = ["Creators cannot buy their own guide."] });
        if (guide.PriceMinorUnits is not > 0 || string.IsNullOrWhiteSpace(guide.CurrencyCode)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["guide"] = ["This guide is not purchasable."] });
        long discountAmount = 0; string? appliedCode = null;
        if (!string.IsNullOrWhiteSpace(request.DiscountCode))
        {
            var code = request.DiscountCode.Trim();
            var discount = await db.GuideDiscounts.AsNoTracking().SingleOrDefaultAsync(x => x.GuideId == guide.Id && x.Active && x.Code.ToUpper() == code.ToUpper());
            if (discount is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["discountCode"] = ["This discount code is not valid for the guide."] });
            discountAmount = checked(guide.PriceMinorUnits.Value * discount.PercentOff / 100);
            appliedCode = discount.Code;
        }
        var payable = guide.PriceMinorUnits.Value - discountAmount;
        var now = clock.UtcNow;
        var headerKey = NormalizeIdempotencyKey(httpRequest.Headers["Idempotency-Key"].ToString());
        if (httpRequest.Headers.ContainsKey("Idempotency-Key") && headerKey is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["idempotencyKey"] = ["Idempotency-Key must be 8 to 200 characters of letters, digits, '-', '_', or '.'."] });
        var idempotencyKey = headerKey ?? $"guide:{guide.Id}:buyer:{buyer}:{appliedCode?.ToUpperInvariant() ?? "-"}";
        var existing = await db.CheckoutIdempotencyKeys.AsNoTracking()
            .Where(x => x.BuyerUserId == buyer && x.Scope == CheckoutScope && x.Key == idempotencyKey)
            .Select(x => new { x.GuideId, x.AmountMinorUnits, x.CurrencyCode, x.DiscountCode, x.OrderId })
            .SingleOrDefaultAsync();
        if (existing is not null)
        {
            var payloadConflict = existing.GuideId != guide.Id
                || existing.AmountMinorUnits != payable
                || !string.Equals(existing.CurrencyCode, guide.CurrencyCode, StringComparison.Ordinal)
                || !string.Equals(existing.DiscountCode ?? string.Empty, appliedCode ?? string.Empty, StringComparison.Ordinal);
            if (payloadConflict)
                return Results.Conflict(new { error = "idempotency-key-reuse", message = "This idempotency key was already used for a different checkout payload." });
            var prior = await db.GuideOrders.AsNoTracking().SingleAsync(x => x.Id == existing.OrderId);
            return Results.Accepted($"/api/v1/commerce/orders/{prior.Id}", new CheckoutResponse(prior.Id, prior.CheckoutReference, string.Empty, prior.AmountMinorUnits, prior.CurrencyCode, prior.ProviderName));
        }
        string reference; string checkoutUrl; string providerName;
        try
        {
            var session = await gateway.CreateCheckoutAsync(new PaymentCheckoutRequest(payable, guide.CurrencyCode!, guide.Id, buyer, appliedCode, "/api/v1/commerce/return/success", "/api/v1/commerce/return/cancel", idempotencyKey), default);
            reference = session.Reference;
            checkoutUrl = session.Url;
            providerName = gateway.ProviderName;
        }
        catch (Exception error) when (error is NotSupportedException or IOException or InvalidOperationException) { return Results.Problem("Payments are unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable); }
        var order = new GuideOrder { Id = Guid.NewGuid(), GuideId = guide.Id, BuyerUserId = buyer, AmountMinorUnits = payable, CurrencyCode = guide.CurrencyCode!, DiscountCode = appliedCode, DiscountAmountMinorUnits = discountAmount, CheckoutReference = reference, ProviderReference = reference, ProviderName = providerName, CreatedAt = now };
        var keyRecord = new CheckoutIdempotencyKey { Id = Guid.NewGuid(), BuyerUserId = buyer, Scope = CheckoutScope, Key = idempotencyKey, GuideId = guide.Id, AmountMinorUnits = payable, CurrencyCode = guide.CurrencyCode!, DiscountCode = appliedCode, OrderId = order.Id, ProviderName = providerName, CreatedAt = now };
        db.GuideOrders.Add(order);
        db.CheckoutIdempotencyKeys.Add(keyRecord);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            var winner = await db.CheckoutIdempotencyKeys.AsNoTracking()
                .Where(x => x.BuyerUserId == buyer && x.Scope == CheckoutScope && x.Key == idempotencyKey)
                .SingleAsync();
            var prior = await db.GuideOrders.AsNoTracking().SingleAsync(x => x.Id == winner.OrderId);
            return Results.Accepted($"/api/v1/commerce/orders/{prior.Id}", new CheckoutResponse(prior.Id, prior.CheckoutReference, string.Empty, prior.AmountMinorUnits, prior.CurrencyCode, prior.ProviderName));
        }
        CommerceCommands.Add(1, new KeyValuePair<string, object?>("operation", "checkout"));
        return Results.Accepted($"/api/v1/commerce/orders/{order.Id}", new CheckoutResponse(order.Id, reference, checkoutUrl, payable, order.CurrencyCode, providerName));
    }

    private static string? NormalizeIdempotencyKey(string raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        if (trimmed.Length is < 8 or > 200) return null;
        foreach (var ch in trimmed)
        {
            if (!(char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')) return null;
        }
        return trimmed;
    }

    private static bool IsUniqueViolation(Exception? error)
    {
        while (error is not null)
        {
            if (error is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation) return true;
            if (error is ArgumentException argEx && argEx.Message.StartsWith("An item with the same key has already been added", StringComparison.Ordinal))
            {
                return true;
            }
            var message = error.Message;
            if (!string.IsNullOrEmpty(message)
                && (message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("cannot be added because another instance with the same key", StringComparison.OrdinalIgnoreCase)
                    || message.Contains("already being tracked", StringComparison.OrdinalIgnoreCase)
                    || message.StartsWith("An item with the same key has already been added", StringComparison.Ordinal)))
                return true;
            error = error.InnerException;
        }
        return false;
    }

    private static async Task<IResult> MyOrders(ClaimsPrincipal principal, AppDbContext db)
    {
        var buyer = IdentityEndpoints.CurrentUserId(principal);
        var orders = await db.GuideOrders.AsNoTracking().Where(x => x.BuyerUserId == buyer).OrderByDescending(x => x.CreatedAt).Select(x => new OrderResponse(x.Id, x.GuideId, x.Status.ToString(), x.AmountMinorUnits, x.CurrencyCode, x.DiscountCode, x.DiscountAmountMinorUnits, x.CreatedAt)).ToListAsync();
        return Results.Ok(orders);
    }

    private static async Task<IResult> MyEntitlements(ClaimsPrincipal principal, AppDbContext db)
    {
        var buyer = IdentityEndpoints.CurrentUserId(principal);
        var entitlements = await db.PurchaseEntitlements.AsNoTracking().Where(x => x.UserId == buyer && x.RevokedAt == null).Join(db.TravelGuides.AsNoTracking(), e => e.GuideId, g => g.Id, (e, g) => new EntitlementResponse(e.Id, g.Id, g.Slug, g.Title, e.GrantedAt)).ToListAsync();
        return Results.Ok(entitlements);
    }

    private static async Task<IResult> CreateDiscount(Guid guideId, DiscountRequest request, ClaimsPrincipal principal, AppDbContext db, IClock clock)
    {
        var owner = IdentityEndpoints.CurrentUserId(principal);
        if (!await db.TravelGuides.AnyAsync(x => x.Id == guideId && x.OwnerUserId == owner)) return Results.NotFound();
        var errors = new Dictionary<string, string[]>();
        var code = request.Code.Trim();
        if (code.Length is < 3 or > 60) errors["code"] = ["Codes must contain 3 to 60 characters."];
        if (request.PercentOff is < 1 or > 100) errors["percentOff"] = ["Percent off must be between 1 and 100."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (await db.GuideDiscounts.AnyAsync(x => x.GuideId == guideId && x.Code.ToUpper() == code.ToUpper())) return Results.ValidationProblem(new Dictionary<string, string[]> { ["code"] = ["This code already exists for the guide."] });
        var discount = new GuideDiscount { Id = Guid.NewGuid(), GuideId = guideId, Code = code, PercentOff = request.PercentOff, CreatedAt = clock.UtcNow };
        db.GuideDiscounts.Add(discount); await db.SaveChangesAsync();
        CommerceCommands.Add(1, new KeyValuePair<string, object?>("operation", "discount-created"));
        return Results.Ok(new { discount.Id, discount.Code, discount.PercentOff });
    }

    private static async Task<IResult> Webhook(HttpRequest httpRequest, AppDbContext db, IConfiguration configuration, IClock clock, IServiceProvider services)
    {
        httpRequest.EnableBuffering();
        using var ms = new MemoryStream();
        await httpRequest.Body.CopyToAsync(ms);
        var rawBody = ms.ToArray();
        httpRequest.Body.Position = 0;
        WebhookRequest? request;
        try
        {
            using var document = await JsonDocument.ParseAsync(httpRequest.Body);
            request = ReadWebhook(document.RootElement);
        }
        catch (JsonException) { return Results.BadRequest(); }
        if (request is null) return Results.BadRequest();
        var verifier = services.GetService<IPaymentWebhookVerifier>();
        var sharedSecret = configuration["Commerce:WebhookSecret"];
        var headerSnapshot = httpRequest.Headers.ToDictionary(h => h.Key, h => h.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var signed = false;
        if (verifier is not null && !string.IsNullOrWhiteSpace(verifier.ProviderName))
        {
            signed = verifier.VerifySignature(rawBody, headerSnapshot, configuration[$"Payment:WebhookSecret"] ?? sharedSecret ?? string.Empty);
            if (!signed) return Results.Unauthorized();
        }
        else
        {
            if (string.IsNullOrEmpty(sharedSecret)) return Results.Unauthorized();
            var provided = httpRequest.Headers["X-Webhook-Secret"].ToString();
            if (!FixedTimeEquals(sharedSecret, provided)) return Results.Unauthorized();
        }
        if (await db.PaymentWebhookEvents.AsNoTracking().AnyAsync(x => x.EventId == request.EventId)) return Results.Ok(new { replayed = true });
        var order = await db.GuideOrders.SingleOrDefaultAsync(x => x.CheckoutReference == request.CheckoutReference);
        if (order is null) return Results.NotFound();
        var now = clock.UtcNow;
        switch (request.Type)
        {
            case "payment.paid" when order.Status == OrderStatus.Pending:
                order.Status = OrderStatus.Paid; order.ConfirmedAt = now;
                db.PurchaseEntitlements.Add(new PurchaseEntitlement { Id = Guid.NewGuid(), GuideId = order.GuideId, UserId = order.BuyerUserId, OrderId = order.Id, GrantedAt = now });
                AppendSaleLedger(db, order, configuration, now);
                break;
            case "payment.refunded" when order.Status == OrderStatus.Paid:
                order.Status = OrderStatus.Refunded; order.RefundedAt = now;
                var entitlement = await db.PurchaseEntitlements.SingleOrDefaultAsync(x => x.OrderId == order.Id);
                if (entitlement is not null) entitlement.RevokedAt = now;
                db.CommerceLedgerEntries.Add(new CommerceLedgerEntry { Id = Guid.NewGuid(), OrderId = order.Id, Kind = LedgerKind.Refund, AmountMinorUnits = order.AmountMinorUnits, CurrencyCode = order.CurrencyCode, CreatedAt = now });
                break;
        }
        db.PaymentWebhookEvents.Add(new PaymentWebhookEvent { EventId = request.EventId, Type = request.Type, ProcessedAt = now });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            return Results.Ok(new { replayed = true });
        }
        CommerceCommands.Add(1, new KeyValuePair<string, object?>("operation", "webhook:" + request.Type));
        return Results.Ok(new { orderId = order.Id, status = order.Status.ToString(), signed = signed });
    }

    private static WebhookRequest? ReadWebhook(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty("eventId", out var eventId) || eventId.ValueKind != JsonValueKind.String) return null;
        if (!element.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) return null;
        if (!element.TryGetProperty("checkoutReference", out var reference) || reference.ValueKind != JsonValueKind.String) return null;
        return new WebhookRequest(eventId.GetString()!, type.GetString()!, reference.GetString()!);
    }

    private static void AppendSaleLedger(AppDbContext db, GuideOrder order, IConfiguration configuration, DateTimeOffset now)
    {
        var rate = configuration.GetValue<decimal?>("Commerce:CommissionRate") ?? 0.10m;
        var commission = (long)Math.Round(order.AmountMinorUnits * rate, MidpointRounding.ToEven);
        db.CommerceLedgerEntries.Add(new CommerceLedgerEntry { Id = Guid.NewGuid(), OrderId = order.Id, Kind = LedgerKind.Gross, AmountMinorUnits = order.AmountMinorUnits, CurrencyCode = order.CurrencyCode, CreatedAt = now });
        db.CommerceLedgerEntries.Add(new CommerceLedgerEntry { Id = Guid.NewGuid(), OrderId = order.Id, Kind = LedgerKind.Commission, AmountMinorUnits = commission, CurrencyCode = order.CurrencyCode, CommissionRateSnapshot = rate, CreatedAt = now });
        db.CommerceLedgerEntries.Add(new CommerceLedgerEntry { Id = Guid.NewGuid(), OrderId = order.Id, Kind = LedgerKind.CreatorNet, AmountMinorUnits = order.AmountMinorUnits - commission, CurrencyCode = order.CurrencyCode, CreatedAt = now });
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        var left = Encoding.UTF8.GetBytes(expected); var right = Encoding.UTF8.GetBytes(provided);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }
}

public sealed record CheckoutRequest(Guid GuideId, string? DiscountCode = null);
public sealed record CheckoutResponse(Guid OrderId, string CheckoutReference, string CheckoutUrl, long AmountMinorUnits, string CurrencyCode, string ProviderName);
public sealed record OrderResponse(Guid Id, Guid GuideId, string Status, long AmountMinorUnits, string CurrencyCode, string? DiscountCode, long DiscountAmountMinorUnits, DateTimeOffset CreatedAt);
public sealed record EntitlementResponse(Guid Id, Guid GuideId, string Slug, string Title, DateTimeOffset GrantedAt);
public sealed record DiscountRequest(string Code, int PercentOff);
public sealed record WebhookRequest(string EventId, string Type, string CheckoutReference);