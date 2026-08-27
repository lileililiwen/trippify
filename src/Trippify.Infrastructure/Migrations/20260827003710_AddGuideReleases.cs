using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuideReleases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guide_releases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Changelog = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublisherUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_releases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guide_releases_travel_guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_guide_releases_users_PublisherUserId",
                        column: x => x.PublisherUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guide_releases_GuideId",
                table: "guide_releases",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "IX_guide_releases_GuideId_VersionNumber",
                table: "guide_releases",
                columns: new[] { "GuideId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guide_releases_PublishedAt",
                table: "guide_releases",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_guide_releases_PublisherUserId",
                table: "guide_releases",
                column: "PublisherUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guide_releases");
        }
    }
}
