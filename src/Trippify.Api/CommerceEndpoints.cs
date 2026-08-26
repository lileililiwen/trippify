using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using Trippify.Application;
using Trippify.Infrastructure;

namespace Trippify.Api;

public static class CommerceEndpoints
{
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

    private static async Task<IResult> Checkout(CheckoutRequest request, ClaimsPrincipal principal, AppDbContext db, IPaymentGateway gateway, IConfiguration configuration, IClock clock)
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
        string reference;
        try { reference = await gateway.CreateCheckoutAsync(payable, guide.CurrencyCode!, default); }
        catch (Exception error) when (error is NotSupportedException or IOException or InvalidOperationException) { return Results.Problem("Payments are unavailable.", statusCode: StatusCodes.Status503ServiceUnavailable); }
        var now = clock.UtcNow;
        var order = new GuideOrder { Id = Guid.NewGuid(), GuideId = guide.Id, BuyerUserId = buyer, AmountMinorUnits = payable, CurrencyCode = guide.CurrencyCode!, DiscountCode = appliedCode, DiscountAmountMinorUnits = discountAmount, CheckoutReference = reference, CreatedAt = now };
        db.GuideOrders.Add(order); await db.SaveChangesAsync();
        CommerceCommands.Add(1, new KeyValuePair<string, object?>("operation", "checkout"));
        return Results.Accepted($"/api/v1/commerce/orders/{order.Id}", new { orderId = order.Id, checkoutReference = reference, amountMinorUnits = payable, currencyCode = order.CurrencyCode });
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

    private static async Task<IResult> Webhook(WebhookRequest request, HttpRequest httpRequest, AppDbContext db, IConfiguration configuration, IClock clock)
    {
        var expected = configuration["Commerce:WebhookSecret"];
        var provided = httpRequest.Headers["X-Webhook-Secret"].ToString();
        if (string.IsNullOrEmpty(expected) || !FixedTimeEquals(expected, provided)) return Results.Unauthorized();
        if (await db.PaymentWebhookEvents.AnyAsync(x => x.EventId == request.EventId)) return Results.Ok(new { replayed = true });
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
        await db.SaveChangesAsync();
        CommerceCommands.Add(1, new KeyValuePair<string, object?>("operation", "webhook:" + request.Type));
        return Results.Ok(new { orderId = order.Id, status = order.Status.ToString() });
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
public sealed record OrderResponse(Guid Id, Guid GuideId, string Status, long AmountMinorUnits, string CurrencyCode, string? DiscountCode, long DiscountAmountMinorUnits, DateTimeOffset CreatedAt);
public sealed record EntitlementResponse(Guid Id, Guid GuideId, string Slug, string Title, DateTimeOffset GrantedAt);
public sealed record DiscountRequest(string Code, int PercentOff);
public sealed record WebhookRequest(string EventId, string Type, string CheckoutReference);
