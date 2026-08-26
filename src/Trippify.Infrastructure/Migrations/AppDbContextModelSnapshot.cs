using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
namespace Trippify.Infrastructure.Migrations;
[DbContext(typeof(AppDbContext))]
public sealed class AppDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder b) { b.HasAnnotation("ProductVersion", "8.0.11"); b.HasPostgresExtension("postgis"); b.Entity("Trippify.Infrastructure.OutboxMessage", e => { e.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uuid"); e.Property<DateTimeOffset>("OccurredAt").HasColumnType("timestamp with time zone"); e.Property<string>("Payload").IsRequired().HasColumnType("text"); e.Property<DateTimeOffset?>("ProcessedAt").HasColumnType("timestamp with time zone"); e.Property<string>("Type").IsRequired().HasMaxLength(200).HasColumnType("character varying(200)"); e.HasKey("Id"); e.HasIndex("OccurredAt"); e.ToTable("outbox_messages"); }); }
}
