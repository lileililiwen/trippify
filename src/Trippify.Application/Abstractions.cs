namespace Trippify.Application;
public interface IObjectStorage
{
    Task<Uri> PutAsync(string key, Stream content, CancellationToken cancellation);
    Task DeleteAsync(string key, CancellationToken cancellation);
    Task<Uri> CreateSignedReadAsync(string key, TimeSpan lifetime, CancellationToken cancellation);
}
public interface IEmailSender { Task SendAsync(string recipient, string subject, string body, CancellationToken cancellation); }
public interface IMapProvider { Task<string?> GeocodeAsync(string address, CancellationToken cancellationToken); }
public interface IPaymentGateway { Task<string> CreateCheckoutAsync(long minorUnits, string currency, CancellationToken cancellationToken); }
public interface IAiAssistant { Task<string> AssistAsync(string input, CancellationToken cancellationToken); }
public interface IBackgroundJobQueue
{
    ValueTask EnqueueAsync(string jobName, string payload, CancellationToken cancellationToken, string? idempotencyKey = null, DateTimeOffset? availableAt = null);
}
public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class ModuleMarker;
