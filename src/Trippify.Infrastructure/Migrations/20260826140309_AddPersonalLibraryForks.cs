using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalLibraryForks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ForkedAt",
                table: "travel_guides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceGuideId",
                table: "travel_guides",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "guide_favorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_favorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guide_favorites_travel_guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_guide_favorites_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceGuideId = table.Column<Guid>(type: "uuid", nullable: true),
                    ForkedGuideId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_trips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_trips_travel_guides_ForkedGuideId",
                        column: x => x.ForkedGuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_trips_travel_guides_SourceGuideId",
                        column: x => x.SourceGuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_trips_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_travel_guides_SourceGuideId",
                table: "travel_guides",
                column: "SourceGuideId");

            migrationBuilder.CreateIndex(
                name: "IX_guide_favorites_GuideId",
                table: "guide_favorites",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "IX_guide_favorites_UserId_GuideId",
                table: "guide_favorites",
                columns: new[] { "UserId", "GuideId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_trips_ForkedGuideId",
                table: "user_trips",
                column: "ForkedGuideId");

            migrationBuilder.CreateIndex(
                name: "IX_user_trips_SourceGuideId",
                table: "user_trips",
                column: "SourceGuideId");

            migrationBuilder.CreateIndex(
                name: "IX_user_trips_UserId_UpdatedAt",
                table: "user_trips",
                columns: new[] { "UserId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guide_favorites");

            migrationBuilder.DropTable(
                name: "user_trips");

            migrationBuilder.DropIndex(
                name: "IX_travel_guides_SourceGuideId",
                table: "travel_guides");

            migrationBuilder.DropColumn(
                name: "ForkedAt",
                table: "travel_guides");

            migrationBuilder.DropColumn(
                name: "SourceGuideId",
                table: "travel_guides");
        }
    }
}
