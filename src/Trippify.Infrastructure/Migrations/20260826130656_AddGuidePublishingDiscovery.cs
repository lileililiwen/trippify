using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuidePublishingDiscovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "travel_guides",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PriceMinorUnits",
                table: "travel_guides",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "travel_guides",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "travel_guides",
                type: "character varying(180)",
                maxLength: 180,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_travel_guides_Slug",
                table: "travel_guides",
                column: "Slug",
                unique: true,
                filter: "\"DeletedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_travel_guides_Slug",
                table: "travel_guides");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "travel_guides");

            migrationBuilder.DropColumn(
                name: "PriceMinorUnits",
                table: "travel_guides");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "travel_guides");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "travel_guides");
        }
    }
}
