using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialRemixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "license_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AllowCommercial = table.Column<bool>(type: "boolean", nullable: false),
                    RequireApproval = table.Column<bool>(type: "boolean", nullable: false),
                    RoyaltyPercent = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_license_policies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_license_policies_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "revenue_shares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Percent = table.Column<int>(type: "integer", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revenue_shares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_revenue_shares_guide_orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "guide_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_revenue_shares_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "remix_ancestries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildGuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentGuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    LicensePolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttributionJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_remix_ancestries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_remix_ancestries_license_policies_LicensePolicyId",
                        column: x => x.LicensePolicyId,
                        principalTable: "license_policies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_remix_ancestries_travel_guides_ChildGuideId",
                        column: x => x.ChildGuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_remix_ancestries_travel_guides_ParentGuideId",
                        column: x => x.ParentGuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "remix_approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RemixAncestryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApproverUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_remix_approvals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_remix_approvals_remix_ancestries_RemixAncestryId",
                        column: x => x.RemixAncestryId,
                        principalTable: "remix_ancestries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_remix_approvals_users_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_license_policies_OwnerUserId_Slug",
                table: "license_policies",
                columns: new[] { "OwnerUserId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_remix_ancestries_ChildGuideId",
                table: "remix_ancestries",
                column: "ChildGuideId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_remix_ancestries_LicensePolicyId",
                table: "remix_ancestries",
                column: "LicensePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_remix_ancestries_ParentGuideId",
                table: "remix_ancestries",
                column: "ParentGuideId");

            migrationBuilder.CreateIndex(
                name: "IX_remix_approvals_ApproverUserId",
                table: "remix_approvals",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_remix_approvals_RemixAncestryId",
                table: "remix_approvals",
                column: "RemixAncestryId");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_shares_OrderId",
                table: "revenue_shares",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_shares_UserId",
                table: "revenue_shares",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "remix_approvals");

            migrationBuilder.DropTable(
                name: "revenue_shares");

            migrationBuilder.DropTable(
                name: "remix_ancestries");

            migrationBuilder.DropTable(
                name: "license_policies");
        }
    }
}
