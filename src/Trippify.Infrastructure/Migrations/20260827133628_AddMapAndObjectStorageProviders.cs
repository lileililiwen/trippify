using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMapAndObjectStorageProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeocodeProviderAttribution",
                table: "guide_nodes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeocodeProviderName",
                table: "guide_nodes",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeocodeProviderPlaceId",
                table: "guide_nodes",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GeocodeStatus",
                table: "guide_nodes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResolvedQuery",
                table: "guide_nodes",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "guide_media",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "guide_media",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "SizeBytes",
                table: "guide_media",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "StorageProvider",
                table: "guide_media",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UploadedAt",
                table: "guide_media",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                table: "guide_media",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "evidence_attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ScanFailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LinkedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evidence_attachments_trip_evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "trip_evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evidence_attachments_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "geocode_cache",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QueryHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalQuery = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ProviderAttribution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderPlaceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geocode_cache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guide_media_GuideId_Sha256",
                table: "guide_media",
                columns: new[] { "GuideId", "Sha256" });

            migrationBuilder.CreateIndex(
                name: "IX_evidence_attachments_EvidenceId",
                table: "evidence_attachments",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_attachments_ExpiresAt",
                table: "evidence_attachments",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_attachments_OwnerUserId",
                table: "evidence_attachments",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_attachments_Sha256_OwnerUserId",
                table: "evidence_attachments",
                columns: new[] { "Sha256", "OwnerUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_evidence_attachments_State",
                table: "evidence_attachments",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_geocode_cache_ExpiresAt",
                table: "geocode_cache",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_geocode_cache_QueryHash",
                table: "geocode_cache",
                column: "QueryHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_attachments");

            migrationBuilder.DropTable(
                name: "geocode_cache");

            migrationBuilder.DropIndex(
                name: "IX_guide_media_GuideId_Sha256",
                table: "guide_media");

            migrationBuilder.DropColumn(
                name: "GeocodeProviderAttribution",
                table: "guide_nodes");

            migrationBuilder.DropColumn(
                name: "GeocodeProviderName",
                table: "guide_nodes");

            migrationBuilder.DropColumn(
                name: "GeocodeProviderPlaceId",
                table: "guide_nodes");

            migrationBuilder.DropColumn(
                name: "GeocodeStatus",
                table: "guide_nodes");

            migrationBuilder.DropColumn(
                name: "ResolvedQuery",
                table: "guide_nodes");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "guide_media");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "guide_media");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "guide_media");

            migrationBuilder.DropColumn(
                name: "StorageProvider",
                table: "guide_media");

            migrationBuilder.DropColumn(
                name: "UploadedAt",
                table: "guide_media");

            migrationBuilder.DropColumn(
                name: "Visibility",
                table: "guide_media");
        }
    }
}
