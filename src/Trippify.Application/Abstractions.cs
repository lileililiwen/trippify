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

public sealed record CheckoutSession(string Url, string Reference, long AmountMinorUnits, string CurrencyCode);

public interface IPaymentGateway
{
    string ProviderName { get; }
    Task<CheckoutSession> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellation);
}

public sealed record PaymentCheckoutRequest(long AmountMinorUnits, string CurrencyCode, Guid GuideId, Guid BuyerUserId, string? DiscountCode, string SuccessUrl, string CancelUrl, string IdempotencyKey);

public interface IPaymentWebhookVerifier
{
    string ProviderName { get; }
    bool VerifySignature(ReadOnlySpan<byte> rawBody, IDictionary<string, string> headers, string expectedSecret);
}

public enum AiAssistKind { Draft, Translate }
public enum AiAssistStatus { Completed, InvalidOutput, ProviderUnavailable, Timeout, Disabled }

public sealed record AiAssistRequest(
    AiAssistKind Kind,
    string SourceText,
    string? SourceLocale,
    string? TargetLocale,
    string OperationId,
    int MaxInputChars,
    int MaxOutputChars);

public sealed record AiAssistResult(
    AiAssistStatus Status,
    string? Title,
    IReadOnlyList<string>? Nodes,
    string? Body,
    string ProviderName,
    string ModelName,
    string SchemaVersion,
    int AttemptCount,
    bool Retryable,
    string FailureCode);

public interface IAiAssistant
{
    string ProviderName { get; }
    AiAssistResult Disabled();
    Task<AiAssistResult> AssistAsync(AiAssistRequest request, CancellationToken cancellation);
}

public interface IBackgroundJobQueue
{
    ValueTask EnqueueAsync(string jobName, string payload, CancellationToken cancellation, string? idempotencyKey = null, DateTimeOffset? availableAt = null);
}
public interface IClock { DateTimeOffset UtcNow { get; } }
public sealed class ModuleMarker;
