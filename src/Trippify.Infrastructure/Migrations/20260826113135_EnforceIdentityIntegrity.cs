using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceIdentityIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_creator_profiles_users_UserId",
                table: "creator_profiles",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_creator_profiles_users_UserId",
                table: "creator_profiles");

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "NormalizedEmail");
        }
    }
}
