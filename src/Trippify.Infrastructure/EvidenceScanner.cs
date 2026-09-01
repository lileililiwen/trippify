using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trippify.Application;

namespace Trippify.Infrastructure;

public sealed class EvidenceScannerOptions
{
    public string Provider { get; set; } = "local";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public int TimeoutMilliseconds { get; set; } = 5000;
    public bool Enabled { get; set; } = true;
    public string FailureWebhookUrl { get; set; } = string.Empty;

    public static EvidenceScannerOptions Bind(IConfiguration configuration)
    {
        var options = new EvidenceScannerOptions();
        configuration.GetSection("EvidenceScanner").Bind(options);
        if (string.IsNullOrWhiteSpace(options.Provider)) options.Provider = "local";
        if (options.TimeoutMilliseconds <= 0) options.TimeoutMilliseconds = 5000;
        return options;
    }

    public void Validate()
    {
        if (Provider.Equals("local", StringComparison.OrdinalIgnoreCase)) return;
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new ProviderConfigurationException("EvidenceScanner:Endpoint must be configured for the production malware scanner.");
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new ProviderConfigurationException("EvidenceScanner:ApiKey must be configured for the production malware scanner.");
    }

    public void EnsureEnvironmentPolicy(string environmentName)
    {
        if (Provider.Equals("local", StringComparison.OrdinalIgnoreCase) && string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
            throw new ProviderConfigurationException("EvidenceScanner:Provider=local is not permitted in production. Configure a real malware scanner provider.");
    }
}

public sealed class EvidenceScannerUnavailableException : IOException
{
    public EvidenceScannerUnavailableException(string message) : base(message) { }
    public EvidenceScannerUnavailableException(string message, Exception inner) : base(message, inner) { }
}

public sealed class LocalEvidenceScanner : IEvidenceScanner
{
    private readonly EvidenceScannerOptions _options;
    private readonly ILogger<LocalEvidenceScanner>? _logger;

    public LocalEvidenceScanner(EvidenceScannerOptions options, ILogger<LocalEvidenceScanner>? logger = null)
    {
        if (!options.Provider.Equals("local", StringComparison.OrdinalIgnoreCase))
            throw new ProviderConfigurationException("LocalEvidenceScanner requires EvidenceScanner:Provider=local.");
        _options = options;
        _logger = logger;
    }

    public string ProviderName => "local-noop";

    public Task<EvidenceScanResult> ScanAsync(EvidenceScanRequest request, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!_options.Enabled)
        {
            _logger?.LogWarning("Local evidence scanner is disabled; treating attachment {Attachment} as unavailable.", request.StorageKey);
            throw new EvidenceScannerUnavailableException("Local evidence scanner is disabled.");
        }
        if (string.IsNullOrEmpty(request.StorageKey) || request.SizeBytes <= 0)
        {
            return Task.FromResult(new EvidenceScanResult(EvidenceScanOutcome.Invalid, ProviderName, "invalid-request"));
        }
        return Task.FromResult(new EvidenceScanResult(EvidenceScanOutcome.Clean, ProviderName, string.Empty));
    }
}

public sealed class HttpEvidenceScanner : IEvidenceScanner
{
    public const string SchemaVersion = "v1";
    private readonly EvidenceScannerOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<HttpEvidenceScanner>? _logger;

    public HttpEvidenceScanner(EvidenceScannerOptions options, HttpClient http, ILogger<HttpEvidenceScanner>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            throw new ProviderConfigurationException("EvidenceScanner:Endpoint must be configured for the remote evidence scanner.");
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ProviderConfigurationException("EvidenceScanner:ApiKey must be configured for the remote evidence scanner.");
        _options = options;
        _http = http;
        _logger = logger;
        _http.BaseAddress ??= new Uri(options.Endpoint);
        _http.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);
    }

    public string ProviderName => "http";

    public async Task<EvidenceScanResult> ScanAsync(EvidenceScanRequest request, CancellationToken cancellation)
    {
        if (!_options.Enabled) throw new EvidenceScannerUnavailableException("Remote evidence scanner is disabled.");
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds));
        var payload = new HttpRequestMessage(HttpMethod.Post, "/v1/scan")
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                storageKey = request.StorageKey,
                contentType = request.ContentType,
                sha256 = request.Sha256,
                sizeBytes = request.SizeBytes,
                schemaVersion = SchemaVersion,
            }), Encoding.UTF8, "application/json"),
        };
        payload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        try
        {
            using var response = await _http.SendAsync(payload, HttpCompletionOption.ResponseContentRead, timeoutCts.Token);
            if (response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            {
                _logger?.LogWarning("Remote evidence scanner returned retryable status {Status}.", (int)response.StatusCode);
                throw new EvidenceScannerUnavailableException($"Remote evidence scanner returned status {(int)response.StatusCode}.");
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Remote evidence scanner rejected the request: {Status}.", (int)response.StatusCode);
                return new EvidenceScanResult(EvidenceScanOutcome.Invalid, ProviderName, $"http-{(int)response.StatusCode}");
            }
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);
            if (!document.RootElement.TryGetProperty("outcome", out var outcomeElement))
                return new EvidenceScanResult(EvidenceScanOutcome.Invalid, ProviderName, "missing-outcome");
            if (!Enum.TryParse<EvidenceScanOutcome>(outcomeElement.GetString(), true, out var outcome))
                return new EvidenceScanResult(EvidenceScanOutcome.Invalid, ProviderName, "unrecognised-outcome");
            var code = document.RootElement.TryGetProperty("failureCode", out var codeElement) ? codeElement.GetString() ?? string.Empty : string.Empty;
            if (outcome == EvidenceScanOutcome.Clean) code = string.Empty;
            return new EvidenceScanResult(outcome, ProviderName, Truncate(code, 100));
        }
        catch (JsonException error)
        {
            _logger?.LogWarning(error, "Remote evidence scanner returned malformed JSON.");
            throw new EvidenceScannerUnavailableException("Remote evidence scanner returned malformed JSON.", error);
        }
        catch (HttpRequestException error)
        {
            _logger?.LogWarning(error, "Remote evidence scanner transport failure.");
            throw new EvidenceScannerUnavailableException("Remote evidence scanner transport failed.", error);
        }
        catch (TaskCanceledException error) when (!cancellation.IsCancellationRequested)
        {
            _logger?.LogWarning(error, "Remote evidence scanner timed out.");
            throw new EvidenceScannerUnavailableException("Remote evidence scanner timed out.", error);
        }
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
        return value[..max];
    }
}

public static class EvidenceScannerRegistration
{
    public static IEvidenceScanner BuildEvidenceScanner(IServiceProvider services, EvidenceScannerOptions options)
    {
        if (options.Provider.Equals("local", StringComparison.OrdinalIgnoreCase))
            return ActivatorUtilities.CreateInstance<LocalEvidenceScanner>(services, options);
        return ActivatorUtilities.CreateInstance<HttpEvidenceScanner>(services, options);
    }
}
