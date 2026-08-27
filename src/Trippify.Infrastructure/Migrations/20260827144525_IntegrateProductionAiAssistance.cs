using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateProductionAiAssistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "translations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "translations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SchemaVersion",
                table: "translations",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "import_jobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "import_jobs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "import_jobs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "import_jobs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SchemaVersion",
                table: "import_jobs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "import_drafts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OutputSchemaVersion",
                table: "import_drafts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "import_drafts",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SchemaVersion",
                table: "import_drafts",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "translations");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "translations");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "translations");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "import_jobs");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "import_jobs");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "import_jobs");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "import_jobs");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "import_jobs");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "import_drafts");

            migrationBuilder.DropColumn(
                name: "OutputSchemaVersion",
                table: "import_drafts");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "import_drafts");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "import_drafts");
        }
    }
}
