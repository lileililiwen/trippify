using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedTripEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "actual_trip_metrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    TripDays = table.Column<int>(type: "integer", nullable: false),
                    TotalCostMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_actual_trip_metrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_actual_trip_metrics_travel_guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_actual_trip_metrics_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trip_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RedactedReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetentionDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trip_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_trip_evidence_travel_guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_trip_evidence_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "verified_guide_badges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    FirstGrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastGrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verified_guide_badges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_verified_guide_badges_travel_guides_GuideId",
                        column: x => x.GuideId,
                        principalTable: "travel_guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "evidence_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evidence_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evidence_reviews_trip_evidence_EvidenceId",
                        column: x => x.EvidenceId,
                        principalTable: "trip_evidence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_evidence_reviews_users_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_actual_trip_metrics_GuideId_CurrencyCode",
                table: "actual_trip_metrics",
                columns: new[] { "GuideId", "CurrencyCode" });

            migrationBuilder.CreateIndex(
                name: "IX_actual_trip_metrics_GuideId_UserId",
                table: "actual_trip_metrics",
                columns: new[] { "GuideId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_actual_trip_metrics_UserId",
                table: "actual_trip_metrics",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_reviews_EvidenceId",
                table: "evidence_reviews",
                column: "EvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_evidence_reviews_ReviewerUserId",
                table: "evidence_reviews",
                column: "ReviewerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_trip_evidence_GuideId_UserId",
                table: "trip_evidence",
                columns: new[] { "GuideId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trip_evidence_RetentionDeadline",
                table: "trip_evidence",
                column: "RetentionDeadline");

            migrationBuilder.CreateIndex(
                name: "IX_trip_evidence_Status",
                table: "trip_evidence",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_trip_evidence_UserId",
                table: "trip_evidence",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_verified_guide_badges_GuideId",
                table: "verified_guide_badges",
                column: "GuideId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "actual_trip_metrics");

            migrationBuilder.DropTable(
                name: "evidence_reviews");

            migrationBuilder.DropTable(
                name: "verified_guide_badges");

            migrationBuilder.DropTable(
                name: "trip_evidence");
        }
    }
}
