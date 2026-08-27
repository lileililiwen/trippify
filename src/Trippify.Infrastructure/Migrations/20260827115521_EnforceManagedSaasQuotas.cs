using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceManagedSaasQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsCustomLimit",
                table: "quota_usages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Reserved",
                table: "quota_usages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "quota_history_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metric = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quota_history_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quota_history_entries_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quota_history_entries_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quota_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quota_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_quota_reservations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quota_history_entries_ActorUserId",
                table: "quota_history_entries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_quota_history_entries_TenantId_Metric_OccurredAt",
                table: "quota_history_entries",
                columns: new[] { "TenantId", "Metric", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_quota_reservations_TenantId_Metric_PeriodStart",
                table: "quota_reservations",
                columns: new[] { "TenantId", "Metric", "PeriodStart" });

            migrationBuilder.Sql("""
                INSERT INTO quota_usages
                    ("Id", "TenantId", "Metric", "Used", "Reserved", "Limit", "IsCustomLimit", "PeriodStart", "PeriodEnd")
                SELECT gen_random_uuid(), t."Id", metrics."Metric", 0, 0,
                    CASE s."Plan"
                        WHEN 'Enterprise' THEN 1000000
                        WHEN 'Pro' THEN CASE metrics."Metric"
                            WHEN 'Guides' THEN 100 WHEN 'AiImports' THEN 250
                            WHEN 'MediaMegabytes' THEN 10000 ELSE 2500 END
                        ELSE CASE metrics."Metric"
                            WHEN 'Guides' THEN 3 WHEN 'AiImports' THEN 5
                            WHEN 'MediaMegabytes' THEN 100 ELSE 25 END
                    END,
                    FALSE,
                    date_trunc('month', CURRENT_TIMESTAMP),
                    date_trunc('month', CURRENT_TIMESTAMP) + interval '1 month'
                FROM tenants t
                JOIN subscriptions s ON s."TenantId" = t."Id"
                CROSS JOIN (VALUES ('Guides'), ('AiImports'), ('MediaMegabytes'), ('BackgroundJobs')) metrics("Metric")
                ON CONFLICT ("TenantId", "Metric", "PeriodStart") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "quota_history_entries");

            migrationBuilder.DropTable(
                name: "quota_reservations");

            migrationBuilder.DropColumn(
                name: "IsCustomLimit",
                table: "quota_usages");

            migrationBuilder.DropColumn(
                name: "Reserved",
                table: "quota_usages");
        }
    }
}
