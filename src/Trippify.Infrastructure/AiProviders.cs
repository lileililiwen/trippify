using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Trippify.Application;

namespace Trippify.Infrastructure;

public sealed class AiProviderOptions
{
    public string Provider { get; set; } = "local";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
    public string SchemaVersion { get; set; } = "v1";
    public int TimeoutMilliseconds { get; set; } = 15000;
    public int MaxAttempts { get; set; } = 3;
    public int MaxInputChars { get; set; } = 20000;
    public int MaxOutputChars { get; set; } = 8000;
    public bool Enabled { get; set; } = true;
    public string FailureWebhookUrl { get; set; } = string.Empty;

    public static AiProviderOptions Bind(IConfiguration configuration)
    {
        var options = new AiProviderOptions();
        configuration.GetSection("Ai").Bind(options);
        if (string.IsNullOrWhiteSpace(options.Provider)) options.Provider = "local";
        if (string.IsNullOrWhiteSpace(options.SchemaVersion)) options.SchemaVersion = "v1";
        if (options.TimeoutMilliseconds <= 0) options.TimeoutMilliseconds = 15000;
        if (options.MaxAttempts <= 0) options.MaxAttempts = 3;
        if (options.MaxInputChars <= 0) options.MaxInputChars = 20000;
        if (options.MaxOutputChars <= 0) options.MaxOutputChars = 8000;
        return options;
    }

    public void Validate()
    {
        if (Provider.Equals("local", StringComparison.OrdinalIgnoreCase)) return;
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new ProviderConfigurationException("Ai:Endpoint must be configured for the production AI provider.");
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new ProviderConfigurationException("Ai:ApiKey must be configured for the production AI provider.");
        if (string.IsNullOrWhiteSpace(Model))
            throw new ProviderConfigurationException("Ai:Model must be configured for the production AI provider.");
    }
}

public sealed class HttpAiAssistant : IAiAssistant
{
    public const string SchemaVersion = "v1";
    private readonly AiProviderOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<HttpAiAssistant>? _logger;

    public HttpAiAssistant(AiProviderOptions options, HttpClient http, ILogger<HttpAiAssistant>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            throw new ProviderConfigurationException("Ai:Endpoint must be configured for the production AI provider.");
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ProviderConfigurationException("Ai:ApiKey must be configured for the production AI provider.");
        _options = options;
        _http = http;
        _logger = logger;
        _http.BaseAddress ??= new Uri(options.Endpoint);
        _http.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);
    }

    public string ProviderName => "http";

    public AiAssistResult Disabled() => new(AiAssistStatus.Disabled, null, null, null, ProviderName, _options.Model, _options.SchemaVersion, 0, false, "provider-disabled");

    public async Task<AiAssistResult> AssistAsync(AiAssistRequest request, CancellationToken cancellation)
    {
        if (!_options.Enabled) return Disabled();
        if (request.SourceText.Length > _options.MaxInputChars)
            return new AiAssistResult(AiAssistStatus.InvalidOutput, null, null, null, ProviderName, _options.Model, _options.SchemaVersion, 0, false, "input-too-long");

        var attempts = 0;
        Exception? lastError = null;
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(_options.TimeoutMilliseconds);
        while (attempts < _options.MaxAttempts && DateTimeOffset.UtcNow < deadline)
        {
            attempts++;
            cancellation.ThrowIfCancellationRequested();
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds));
                var payload = new HttpRequestMessage(HttpMethod.Post, "/v1/ai/assist")
                {
                    Content = new StringContent(JsonSerializer.Serialize(BuildRequestBody(request)), Encoding.UTF8, "application/json"),
                };
                payload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                using var response = await _http.SendAsync(payload, HttpCompletionOption.ResponseContentRead, timeoutCts.Token);
                if (response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    _logger?.LogWarning("AI provider returned transient status {Status}.", (int)response.StatusCode);
                    lastError = new IOException($"AI provider returned transient status {(int)response.StatusCode}.");
                    await Task.Delay(Backoff(attempts), timeoutCts.Token);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("AI provider rejected request with status {Status}.", (int)response.StatusCode);
                    return new AiAssistResult(AiAssistStatus.InvalidOutput, null, null, null, ProviderName, _options.Model, _options.SchemaVersion, attempts, false, "provider-rejected");
                }
                await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);
                var parsed = TryParse(document.RootElement, request);
                if (parsed is null)
                {
                    return new AiAssistResult(AiAssistStatus.InvalidOutput, null, null, null, ProviderName, _options.Model, _options.SchemaVersion, attempts, false, "schema-mismatch");
                }
                return parsed with { AttemptCount = attempts };
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                lastError = new TimeoutException("AI provider timed out.");
            }
            catch (HttpRequestException error)
            {
                _logger?.LogWarning(error, "AI provider call failed.");
                lastError = error;
            }
            catch (JsonException error)
            {
                _logger?.LogWarning(error, "AI provider returned malformed JSON.");
                return new AiAssistResult(AiAssistStatus.InvalidOutput, null, null, null, ProviderName, _options.Model, _options.SchemaVersion, attempts, false, "malformed-json");
            }
            catch (IOException error)
            {
                _logger?.LogWarning(error, "AI provider I/O failure.");
                lastError = error;
            }
            await Task.Delay(Backoff(attempts), cancellation);
        }
        var status = lastError is TimeoutException ? AiAssistStatus.Timeout : AiAssistStatus.ProviderUnavailable;
        var code = lastError is TimeoutException ? "timeout" : "provider-unavailable";
        var retryable = attempts >= _options.MaxAttempts;
        return new AiAssistResult(status, null, null, null, ProviderName, _options.Model, _options.SchemaVersion, attempts, retryable, code);
    }

    private object BuildRequestBody(AiAssistRequest request) => new
    {
        schemaVersion = _options.SchemaVersion,
        kind = request.Kind.ToString().ToLowerInvariant(),
        sourceText = request.SourceText,
        sourceLocale = request.SourceLocale,
        targetLocale = request.TargetLocale,
        operationId = request.OperationId,
        maxOutputChars = request.MaxOutputChars,
        limits = new { maxInputChars = request.MaxInputChars, maxOutputChars = request.MaxOutputChars },
    };

    private static AiAssistResult? TryParse(JsonElement element, AiAssistRequest request)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty("schemaVersion", out var versionProperty) || versionProperty.GetString() != SchemaVersion) return null;
        var title = element.TryGetProperty("title", out var titleProperty) ? titleProperty.GetString() : null;
        if (request.Kind == AiAssistKind.Draft)
        {
            if (!element.TryGetProperty("nodes", out var nodesProperty) || nodesProperty.ValueKind != JsonValueKind.Array) return null;
            var nodes = new List<string>();
            foreach (var node in nodesProperty.EnumerateArray())
            {
                if (node.ValueKind != JsonValueKind.String) return null;
                var text = node.GetString();
                if (string.IsNullOrWhiteSpace(text)) return null;
                nodes.Add(text.Trim());
                if (nodes.Count >= 32) break;
            }
            if (nodes.Count == 0) return null;
            if (string.IsNullOrWhiteSpace(title)) title = nodes[0];
            return new AiAssistResult(AiAssistStatus.Completed, title!, nodes, null, "http", string.Empty, SchemaVersion, 0, false, string.Empty);
        }
        if (request.Kind == AiAssistKind.Translate)
        {
            if (!element.TryGetProperty("body", out var bodyProperty) || bodyProperty.ValueKind != JsonValueKind.String) return null;
            var body = bodyProperty.GetString();
            if (string.IsNullOrWhiteSpace(body)) return null;
            return new AiAssistResult(AiAssistStatus.Completed, null, null, body.Trim(), "http", string.Empty, SchemaVersion, 0, false, string.Empty);
        }
        return null;
    }

    private static TimeSpan Backoff(int attempt) => TimeSpan.FromMilliseconds(Math.Min(2000, 200 * Math.Pow(2, Math.Clamp(attempt, 1, 8))));
}

public sealed class SafeInputScrubber
{
    private static readonly Regex EmailPattern = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"\+?\d[\d\s\-]{6,}\d", RegexOptions.Compiled);

    public static string Scrub(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var scrubbed = EmailPattern.Replace(input, "[redacted-email]");
        scrubbed = PhonePattern.Replace(scrubbed, "[redacted-phone]");
        return scrubbed;
    }
}
