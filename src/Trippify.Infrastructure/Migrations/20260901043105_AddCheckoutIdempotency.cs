using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckoutIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "checkout_idempotency_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DiscountCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkout_idempotency_keys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_checkout_idempotency_keys_guide_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "guide_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_checkout_idempotency_keys_travel_guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_checkout_idempotency_keys_users_BuyerUserId",
                        column: x => x.BuyerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_checkout_idempotency_keys_BuyerUserId_Scope_Key",
                table: "checkout_idempotency_keys",
                columns: new[] { "BuyerUserId", "Scope", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_checkout_idempotency_keys_GuideId",
                table: "checkout_idempotency_keys",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "IX_checkout_idempotency_keys_OrderId",
                table: "checkout_idempotency_keys",
                column: "OrderId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "checkout_idempotency_keys");
        }
    }
}
