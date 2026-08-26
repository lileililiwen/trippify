using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuideCommerceEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guide_discounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PercentOff = table.Column<int>(type: "integer", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_discounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guide_discounts_travel_guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guide_orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DiscountCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    DiscountAmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CheckoutReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefundedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guide_orders_travel_guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_guide_orders_users_BuyerUserId",
                        column: x => x.BuyerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payment_webhook_events",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_webhook_events", x => x.EventId);
                });

            migrationBuilder.CreateTable(
                name: "purchase_entitlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchase_entitlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_purchase_entitlements_travel_guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_purchase_entitlements_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "commerce_ledger_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CommissionRateSnapshot = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_commerce_ledger_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_commerce_ledger_entries_guide_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "guide_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_commerce_ledger_entries_OrderId_Kind",
                table: "commerce_ledger_entries",
                columns: new[] { "OrderId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guide_discounts_GuideId_Code",
                table: "guide_discounts",
                columns: new[] { "GuideId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guide_orders_BuyerUserId_CreatedAt",
                table: "guide_orders",
                columns: new[] { "BuyerUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guide_orders_CheckoutReference",
                table: "guide_orders",
                column: "CheckoutReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guide_orders_GuideId",
                table: "guide_orders",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "IX_purchase_entitlements_GuideId_UserId",
                table: "purchase_entitlements",
                columns: new[] { "GuideId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_purchase_entitlements_UserId",
                table: "purchase_entitlements",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commerce_ledger_entries");

            migrationBuilder.DropTable(
                name: "guide_discounts");

            migrationBuilder.DropTable(
                name: "payment_webhook_events");

            migrationBuilder.DropTable(
                name: "purchase_entitlements");

            migrationBuilder.DropTable(
                name: "guide_orders");
        }
    }
}
