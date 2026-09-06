using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trippify.Application;

namespace Trippify.Infrastructure;

public sealed class PaymentProviderOptions
{
    public string Provider { get; set; } = "local";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? WebhookSecret { get; set; }
    public string WebhookHeader { get; set; } = "X-Provider-Signature";
    public string SignatureScheme { get; set; } = "v1";
    public int TimeoutMilliseconds { get; set; } = 10000;
    public bool Enabled { get; set; } = true;
    public string PublicCheckoutBaseUrl { get; set; } = string.Empty;

    public static PaymentProviderOptions Bind(IConfiguration configuration)
    {
        var options = new PaymentProviderOptions();
        configuration.GetSection("Payment").Bind(options);
        if (string.IsNullOrWhiteSpace(options.Provider)) options.Provider = "local";
        if (options.TimeoutMilliseconds <= 0) options.TimeoutMilliseconds = 10000;
        if (string.IsNullOrWhiteSpace(options.WebhookHeader)) options.WebhookHeader = "X-Provider-Signature";
        if (string.IsNullOrWhiteSpace(options.SignatureScheme)) options.SignatureScheme = "v1";
        if (string.IsNullOrWhiteSpace(options.WebhookSecret))
            options.WebhookSecret = configuration["Payment:WebhookSecret"] ?? configuration["Commerce:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(options.PublicCheckoutBaseUrl))
            options.PublicCheckoutBaseUrl = configuration["Payment:PublicCheckoutBaseUrl"] ?? string.Empty;
        return options;
    }

    public void Validate(string? environment = null)
    {
        if (Provider.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, "Staging", StringComparison.OrdinalIgnoreCase))
                throw new ProviderConfigurationException("Payment:Provider must not be 'local' in Staging or Production.");
            return;
        }
        if (!Enabled)
        {
            if (string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, "Staging", StringComparison.OrdinalIgnoreCase))
                throw new ProviderConfigurationException("Payment:Enabled must not be false in Staging or Production.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Endpoint))
            throw new ProviderConfigurationException("Payment:Endpoint must be configured for the production payment provider.");
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new ProviderConfigurationException("Payment:ApiKey must be configured for the production payment provider.");
        if (string.IsNullOrWhiteSpace(WebhookSecret))
            throw new ProviderConfigurationException("Payment:WebhookSecret must be configured for the production payment provider.");
    }
}

public sealed class HttpPaymentGateway : IPaymentGateway, IPaymentWebhookVerifier
{
    public string ProviderName => "http";
    private readonly PaymentProviderOptions _options;
    private readonly HttpClient _http;
    private readonly ILogger<HttpPaymentGateway>? _logger;

    public HttpPaymentGateway(PaymentProviderOptions options, HttpClient http, ILogger<HttpPaymentGateway>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
            throw new ProviderConfigurationException("Payment:Endpoint must be configured for the production payment provider.");
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ProviderConfigurationException("Payment:ApiKey must be configured for the production payment provider.");
        _options = options;
        _http = http;
        _logger = logger;
        _http.BaseAddress ??= new Uri(options.Endpoint);
        _http.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);
    }

    public async Task<CheckoutSession> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellation)
    {
        if (!_options.Enabled)
            throw new NotSupportedException("Payment provider is disabled by configuration.");
        var body = new
        {
            amountMinorUnits = request.AmountMinorUnits,
            currency = request.CurrencyCode,
            guideId = request.GuideId,
            buyerUserId = request.BuyerUserId,
            discountCode = request.DiscountCode,
            successUrl = request.SuccessUrl,
            cancelUrl = request.CancelUrl,
            idempotencyKey = request.IdempotencyKey,
        };
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_options.TimeoutMilliseconds));
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Headers.Add("Idempotency-Key", request.IdempotencyKey);
        try
        {
            using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, timeoutCts.Token);
            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout || response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new IOException($"Payment provider returned transient status {(int)response.StatusCode}.");
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Payment provider rejected checkout with status {Status}.", (int)response.StatusCode);
                throw new InvalidOperationException($"Payment provider rejected checkout with status {(int)response.StatusCode}.");
            }
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);
            var root = document.RootElement;
            if (!root.TryGetProperty("sessionId", out var sessionProp) || sessionProp.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("Payment provider response missing sessionId.");
            if (!root.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String)
                throw new InvalidOperationException("Payment provider response missing url.");
            return new CheckoutSession(urlProp.GetString()!, sessionProp.GetString()!, request.AmountMinorUnits, request.CurrencyCode);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        {
            throw new IOException("Payment provider timed out before responding.");
        }
    }

    public bool VerifySignature(ReadOnlySpan<byte> rawBody, IDictionary<string, string> headers, string expectedSecret)
    {
        if (string.IsNullOrWhiteSpace(expectedSecret)) return false;
        if (!headers.TryGetValue(_options.WebhookHeader, out var provided) && !headers.TryGetValue(_options.WebhookHeader.ToLowerInvariant(), out provided))
            return false;
        if (string.IsNullOrWhiteSpace(provided)) return false;
        if (!provided.StartsWith($"{_options.SignatureScheme}=", StringComparison.OrdinalIgnoreCase)) return false;
        var expected = Sign(expectedSecret, rawBody);
        var providedValue = provided[$"{_options.SignatureScheme}=".Length..];
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(providedValue);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    public static string Sign(string secret, ReadOnlySpan<byte> body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(body.ToArray());
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
public sealed class NoopPaymentWebhookVerifier : IPaymentWebhookVerifier
{
    public string ProviderName => string.Empty;
    public bool VerifySignature(ReadOnlySpan<byte> rawBody, IDictionary<string, string> headers, string expectedSecret) => false;
}
