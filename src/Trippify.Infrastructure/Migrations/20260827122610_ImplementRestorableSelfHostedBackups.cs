using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImplementRestorableSelfHostedBackups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArtifactPath",
                table: "backup_snapshots",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Encrypted",
                table: "backup_snapshots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Restorable",
                table: "backup_snapshots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RetentionUntil",
                table: "backup_snapshots",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "backup_snapshots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "backup_snapshots",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "backup_snapshots",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArtifactPath",
                table: "backup_snapshots");

            migrationBuilder.DropColumn(
                name: "Encrypted",
                table: "backup_snapshots");

            migrationBuilder.DropColumn(
                name: "Restorable",
                table: "backup_snapshots");

            migrationBuilder.DropColumn(
                name: "RetentionUntil",
                table: "backup_snapshots");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "backup_snapshots");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "backup_snapshots");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "backup_snapshots");
        }
    }
}
