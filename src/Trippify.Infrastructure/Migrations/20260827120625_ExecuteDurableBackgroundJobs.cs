using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExecuteDurableBackgroundJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceJobId",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "background_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadVersion = table.Column<int>(type: "integer", nullable: false),
                    Payload = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseOwner = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_background_jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_SourceJobId",
                table: "notifications",
                column: "SourceJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_IdempotencyKey",
                table: "background_jobs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_background_jobs_Status_AvailableAt_LeaseExpiresAt",
                table: "background_jobs",
                columns: new[] { "Status", "AvailableAt", "LeaseExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_jobs");

            migrationBuilder.DropIndex(
                name: "IX_notifications_SourceJobId",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "SourceJobId",
                table: "notifications");
        }
    }
}
