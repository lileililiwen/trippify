using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Trippify.Infrastructure;

public enum OrderStatus { Pending, Paid, Refunded, Failed }
public enum LedgerKind { Gross, Commission, CreatorNet, Refund }

public sealed class GuideOrder
{
    public Guid Id { get; init; }
    public Guid GuideId { get; init; }
    public Guid BuyerUserId { get; init; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public long AmountMinorUnits { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string? DiscountCode { get; set; }
    public long DiscountAmountMinorUnits { get; set; }
    public required string CheckoutReference { get; init; }
    public string ProviderReference { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
}

public sealed class PurchaseEntitlement
{
    public Guid Id { get; init; }
    public Guid GuideId { get; init; }
    public Guid UserId { get; init; }
    public Guid OrderId { get; init; }
    public DateTimeOffset GrantedAt { get; init; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class CommerceLedgerEntry
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }
    public LedgerKind Kind { get; init; }
    public long AmountMinorUnits { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal? CommissionRateSnapshot { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class GuideDiscount
{
    public Guid Id { get; init; }
    public Guid GuideId { get; init; }
    public required string Code { get; set; }
    public int PercentOff { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class PaymentWebhookEvent
{
    public required string EventId { get; init; }
    public required string Type { get; init; }
    public DateTimeOffset ProcessedAt { get; init; }
}

public sealed class CheckoutIdempotencyKey
{
    public Guid Id { get; init; }
    public Guid BuyerUserId { get; init; }
    public required string Scope { get; init; }
    public required string Key { get; init; }
    public Guid GuideId { get; init; }
    public long AmountMinorUnits { get; init; }
    public required string CurrencyCode { get; init; }
    public string? DiscountCode { get; init; }
    public Guid OrderId { get; init; }
    public required string ProviderName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class GuideOrderConfiguration : IEntityTypeConfiguration<GuideOrder>
{
    public void Configure(EntityTypeBuilder<GuideOrder> e)
    {
        e.ToTable("guide_orders"); e.HasKey(x => x.Id);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.CurrencyCode).HasMaxLength(3);
        e.Property(x => x.DiscountCode).HasMaxLength(60);
        e.Property(x => x.CheckoutReference).HasMaxLength(200);
        e.Property(x => x.ProviderReference).HasMaxLength(200);
        e.Property(x => x.ProviderName).HasMaxLength(40);
        e.HasIndex(x => x.CheckoutReference).IsUnique();
        e.HasIndex(x => new { x.BuyerUserId, x.CreatedAt });
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.BuyerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PurchaseEntitlementConfiguration : IEntityTypeConfiguration<PurchaseEntitlement>
{
    public void Configure(EntityTypeBuilder<PurchaseEntitlement> e)
    {
        e.ToTable("purchase_entitlements"); e.HasKey(x => x.Id);
        e.HasIndex(x => new { x.GuideId, x.UserId }).IsUnique();
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CommerceLedgerEntryConfiguration : IEntityTypeConfiguration<CommerceLedgerEntry>
{
    public void Configure(EntityTypeBuilder<CommerceLedgerEntry> e)
    {
        e.ToTable("commerce_ledger_entries"); e.HasKey(x => x.Id);
        e.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.CurrencyCode).HasMaxLength(3);
        e.HasIndex(x => new { x.OrderId, x.Kind }).IsUnique();
        e.HasOne<GuideOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GuideDiscountConfiguration : IEntityTypeConfiguration<GuideDiscount>
{
    public void Configure(EntityTypeBuilder<GuideDiscount> e)
    {
        e.ToTable("guide_discounts"); e.HasKey(x => x.Id);
        e.Property(x => x.Code).HasMaxLength(60);
        e.HasIndex(x => new { x.GuideId, x.Code }).IsUnique();
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PaymentWebhookEventConfiguration : IEntityTypeConfiguration<PaymentWebhookEvent>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookEvent> e)
    {
        e.ToTable("payment_webhook_events"); e.HasKey(x => x.EventId);
        e.Property(x => x.EventId).HasMaxLength(200);
        e.Property(x => x.Type).HasMaxLength(50);
    }
}

public sealed class CheckoutIdempotencyKeyConfiguration : IEntityTypeConfiguration<CheckoutIdempotencyKey>
{
    public void Configure(EntityTypeBuilder<CheckoutIdempotencyKey> e)
    {
        e.ToTable("checkout_idempotency_keys"); e.HasKey(x => x.Id);
        e.Property(x => x.Scope).HasMaxLength(40);
        e.Property(x => x.Key).HasMaxLength(200);
        e.Property(x => x.CurrencyCode).HasMaxLength(3);
        e.Property(x => x.DiscountCode).HasMaxLength(60);
        e.Property(x => x.ProviderName).HasMaxLength(40);
        e.HasIndex(x => new { x.BuyerUserId, x.Scope, x.Key }).IsUnique();
        e.HasIndex(x => x.OrderId).IsUnique();
        e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.BuyerUserId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<TravelGuide>().WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<GuideOrder>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
    }
}
