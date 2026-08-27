namespace Trippify.Application;

public enum ObjectStorageVisibility { Private, Public }

public interface IObjectStorage
{
    Task<Uri> PutAsync(string key, Stream content, CancellationToken cancellation);
    Task DeleteAsync(string key, CancellationToken cancellation);
    Task<Uri> CreateSignedReadAsync(string key, TimeSpan lifetime, CancellationToken cancellation);
    Task<bool> ExistsAsync(string key, CancellationToken cancellation);
}

public interface IEmailSender { Task SendAsync(string recipient, string subject, string body, CancellationToken cancellation); }

public enum GeocodeStatus { Resolved, Unresolved }

public sealed record GeocodeResult(
    GeocodeStatus Status,
    double? Latitude,
    double? Longitude,
    string ProviderName,
    string ProviderAttribution,
    string? ProviderPlaceId)
{
    public static GeocodeResult Resolved(double latitude, double longitude, string provider, string attribution, string? placeId) =>
        new(GeocodeStatus.Resolved, latitude, longitude, provider, attribution, placeId);
    public static GeocodeResult Unresolved(string provider, string attribution) =>
        new(GeocodeStatus.Unresolved, null, null, provider, attribution, null);
}

public interface IMapProvider
{
    string ProviderName { get; }
    Task<GeocodeResult> GeocodeAsync(string address, CancellationToken cancellationToken);
}

public interface IPaymentGateway { Task<string> CreateCheckoutAsync(long minorUnits, string currency, CancellationToken cancellation); }
public interface IAiAssistant { Task<string> AssistAsync(string input, CancellationToken cancellation); }
public interface IBackgroundJobQueue
{
    ValueTask EnqueueAsync(string jobName, string payload, CancellationToken cancellation, string? idempotencyKey = null, DateTimeOffset? availableAt = null);
}
public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class ModuleMarker;
