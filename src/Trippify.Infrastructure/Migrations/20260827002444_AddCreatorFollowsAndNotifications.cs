using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trippify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorFollowsAndNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "creator_follows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_follows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_creator_follows_users_CreatorUserId",
                        column: x => x.CreatorUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_creator_follows_users_FollowerUserId",
                        column: x => x.FollowerUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NewGuidePublishedEmail = table.Column<bool>(type: "boolean", nullable: false),
                    NewGuidePublishedInApp = table.Column<bool>(type: "boolean", nullable: false),
                    NewReviewOnMyGuideEmail = table.Column<bool>(type: "boolean", nullable: false),
                    NewReviewOnMyGuideInApp = table.Column<bool>(type: "boolean", nullable: false),
                    NewReplyToReviewEmail = table.Column<bool>(type: "boolean", nullable: false),
                    NewReplyToReviewInApp = table.Column<bool>(type: "boolean", nullable: false),
                    FollowerGainedEmail = table.Column<bool>(type: "boolean", nullable: false),
                    FollowerGainedInApp = table.Column<bool>(type: "boolean", nullable: false),
                    EvidenceReviewedEmail = table.Column<bool>(type: "boolean", nullable: false),
                    EvidenceReviewedInApp = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_notification_preferences_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    TargetSlug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TargetGuideId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_creator_follows_CreatorUserId_FollowerUserId",
                table: "creator_follows",
                columns: new[] { "CreatorUserId", "FollowerUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creator_follows_FollowerUserId",
                table: "creator_follows",
                column: "FollowerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_CreatedAt",
                table: "notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_notifications_UserId_ReadAt",
                table: "notifications",
                columns: new[] { "UserId", "ReadAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creator_follows");

            migrationBuilder.DropTable(
                name: "notification_preferences");

            migrationBuilder.DropTable(
                name: "notifications");
        }
    }
}
