using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationPluginSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "plugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Manifest = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Signature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plugin_audit_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugin_audit_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plugin_audit_entries_plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_plugin_audit_entries_users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "plugin_installations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Lifecycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InstalledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EnabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DisabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UninstalledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugin_installations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plugin_installations_plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_plugin_installations_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plugin_permission_grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstallationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugin_permission_grants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plugin_permission_grants_plugin_installations_InstallationId",
                        column: x => x.InstallationId,
                        principalTable: "plugin_installations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_plugin_audit_entries_ActorUserId",
                table: "plugin_audit_entries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_plugin_audit_entries_OccurredAt",
                table: "plugin_audit_entries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_plugin_audit_entries_PluginId",
                table: "plugin_audit_entries",
                column: "PluginId");

            migrationBuilder.CreateIndex(
                name: "IX_plugin_installations_PluginId_UserId",
                table: "plugin_installations",
                columns: new[] { "PluginId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plugin_installations_UserId",
                table: "plugin_installations",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_plugin_permission_grants_InstallationId",
                table: "plugin_permission_grants",
                column: "InstallationId");

            migrationBuilder.CreateIndex(
                name: "IX_plugins_Slug_Version",
                table: "plugins",
                columns: new[] { "Slug", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "plugin_audit_entries");

            migrationBuilder.DropTable(
                name: "plugin_permission_grants");

            migrationBuilder.DropTable(
                name: "plugin_installations");

            migrationBuilder.DropTable(
                name: "plugins");
        }
    }
}
