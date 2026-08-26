using Microsoft.EntityFrameworkCore;
using Trippify.Application;
namespace Trippify.Infrastructure;
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    protected override void OnModelCreating(ModelBuilder b) { b.HasPostgresExtension("postgis"); b.Entity<OutboxMessage>(e => { e.ToTable("outbox_messages"); e.HasKey(x => x.Id); e.Property(x => x.Type).HasMaxLength(200); e.HasIndex(x => x.OccurredAt); }); }
}
public sealed class OutboxMessage { public Guid Id { get; init; } public DateTimeOffset OccurredAt { get; init; } public required string Type { get; init; } public required string Payload { get; init; } public DateTimeOffset? ProcessedAt { get; set; } }
public sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
public sealed class LocalProviders : IObjectStorage, IEmailSender, IMapProvider, IPaymentGateway, IAiAssistant, IBackgroundJobQueue
{
    public Task<Uri> PutAsync(string key, Stream content, CancellationToken cancellationToken) => Task.FromResult(new Uri("file:///tmp/trippify/" + Uri.EscapeDataString(key)));
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<string?> GeocodeAsync(string address, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    public Task<string> CreateCheckoutAsync(long minorUnits, string currency, CancellationToken cancellationToken) => throw new NotSupportedException("Payments are disabled in local mode.");
    public Task<string> AssistAsync(string input, CancellationToken cancellationToken) => Task.FromResult(input);
    public ValueTask EnqueueAsync(string jobName, string payload, CancellationToken cancellationToken) => ValueTask.CompletedTask;
}
