using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
namespace Trippify.Infrastructure.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("202608260001_Initial")]
public sealed class Initial : Migration
{
    protected override void Up(MigrationBuilder m) { m.Sql("CREATE EXTENSION IF NOT EXISTS postgis;"); m.CreateTable("outbox_messages", t => new { Id = t.Column<Guid>(type: "uuid", nullable: false), OccurredAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false), Type = t.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false), Payload = t.Column<string>(type: "text", nullable: false), ProcessedAt = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true) }, constraints: t => t.PrimaryKey("PK_outbox_messages", x => x.Id)); m.CreateIndex("IX_outbox_messages_OccurredAt", "outbox_messages", "OccurredAt"); }
    protected override void Down(MigrationBuilder m) => m.DropTable("outbox_messages");
}
