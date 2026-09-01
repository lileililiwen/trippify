using System.Text.RegularExpressions;

namespace Trippify.Api;

public static class RuntimeSecurityValidator
{
    public const string DefaultCorsSection = "Cors:AllowedOrigins";
    public const int MinSecretLength = 24;
    public const string DisallowedSignedUrlSecret = "change-me-to-a-long-random-secret";
    public const string DisallowedPluginSigningSecret = "trippify-dev-shared-hmac-secret";

    private static readonly string[] KnownWeakTokens =
    {
        "secret", "password", "changeme", "redacted", "placeholder", "test-key", "weak"
    };

    private static readonly Regex AllowedOriginPattern = new(
        "^(https?|wss?)://(?:\\[[0-9a-fA-F:]+\\]|[A-Za-z0-9\\-\\.]+)(?::\\d+)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<string> Validate(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (configuration is null) throw new ArgumentNullException(nameof(configuration));
        if (environment is null) throw new ArgumentNullException(nameof(environment));
        var isProduction = environment.IsProduction() || environment.IsStaging();
        var allowedOrigins = LoadAndNormalizeOrigins(configuration, isProduction);
        if (isProduction && allowedOrigins.Count == 0)
            throw new InvalidOperationException("Cors:AllowedOrigins must list at least one exact origin in Staging/Production; empty configuration is rejected at startup.");
        var objectStorage = configuration.GetSection("ObjectStorage");
        var objectStorageSecret = ReadSecret(objectStorage, "SignedUrlSecret")
            ?? configuration["ObjectStorage:SignedUrlSecret"]
            ?? string.Empty;
        if (isProduction && !environment.IsDevelopment())
            EnsureStrongSecret("ObjectStorage:SignedUrlSecret", objectStorageSecret, allowList: new[] { DisallowedSignedUrlSecret });
        var payment = configuration.GetSection("Payment");
        var paymentEnabled = payment.GetValue("Enabled", false);
        var paymentRemote = string.Equals(payment.GetValue<string>("Provider"), "http", StringComparison.OrdinalIgnoreCase);
        if (isProduction && paymentEnabled && paymentRemote)
        {
            EnsureStrongSecret("Payment:ApiKey", payment.GetValue<string>("ApiKey"), allowList: KnownWeakTokens);
            EnsureStrongSecret("Payment:WebhookSecret", payment.GetValue<string>("WebhookSecret"), allowList: KnownWeakTokens);
        }
        var ai = configuration.GetSection("Ai");
        var aiEnabled = ai.GetValue("Enabled", false);
        var aiRemote = string.Equals(ai.GetValue<string>("Provider"), "http", StringComparison.OrdinalIgnoreCase);
        if (isProduction && aiEnabled && aiRemote)
            EnsureStrongSecret("Ai:ApiKey", ai.GetValue<string>("ApiKey"), allowList: KnownWeakTokens);
        var plugins = configuration.GetSection("Plugins");
        var pluginSecret = ReadSecret(plugins, "SigningSecret")
            ?? configuration["Plugins:SigningSecret"]
            ?? string.Empty;
        if (isProduction)
            EnsureStrongSecret("Plugins:SigningSecret", pluginSecret, allowList: new[] { DisallowedPluginSigningSecret });
        return allowedOrigins;
    }

    public static string NormalizeOrigin(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Origin is required.", nameof(value));
        var trimmed = value.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Origin '{value}' is not a valid absolute URL.", nameof(value));
        if (string.IsNullOrEmpty(uri.Host)) throw new ArgumentException($"Origin '{value}' is missing a host.", nameof(value));
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != "ws" && uri.Scheme != "wss")
            throw new ArgumentException($"Origin '{value}' must use http, https, ws, or wss.", nameof(value));
        var defaultPort = (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == "ws") ? 80 : 443;
        var port = uri.IsDefaultPort ? -1 : uri.Port;
        var portSegment = port < 0 || port == defaultPort ? string.Empty : ":" + port;
        return uri.Scheme.ToLowerInvariant() + "://" + uri.Host.ToLowerInvariant() + portSegment;
    }

    private static IReadOnlyList<string> LoadAndNormalizeOrigins(IConfiguration configuration, bool enforceFormat)
    {
        var raw = configuration.GetSection(DefaultCorsSection).Get<string[]>();
        if (raw is null || raw.Length == 0) return Array.Empty<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(raw.Length);
        foreach (var value in raw)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            string normalized;
            try
            {
                normalized = NormalizeOrigin(value);
            }
            catch (ArgumentException ex) when (enforceFormat)
            {
                throw new InvalidOperationException($"Cors:AllowedOrigins contains the invalid exact origin '{value}'. Use scheme://host[:port] without wildcards or trailing slashes.", ex);
            }
            if (enforceFormat && !AllowedOriginPattern.IsMatch(normalized))
                throw new InvalidOperationException($"Cors:AllowedOrigins contains the invalid exact origin '{value}'. Use scheme://host[:port] without wildcards or trailing slashes.");
            if (seen.Add(normalized)) result.Add(normalized);
        }
        return result;
    }

    private static string? ReadSecret(IConfiguration section, string key)
    {
        var value = section.GetValue<string>(key);
        if (!string.IsNullOrWhiteSpace(value)) return value;
        return null;
    }

    private static void EnsureStrongSecret(string setting, string? value, IReadOnlyList<string> allowList)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{setting} must be configured in Staging/Production.");
        var trimmed = value.Trim();
        if (trimmed.Length < MinSecretLength)
            throw new InvalidOperationException($"{setting} must be at least {MinSecretLength} characters in Staging/Production.");
        foreach (var known in allowList)
            if (string.Equals(trimmed, known, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{setting} is set to a known-weak value ('{known}'). Configure a unique, high-entropy secret from your secret store.");
        var lower = trimmed.ToLowerInvariant();
        foreach (var weak in KnownWeakTokens)
            if (lower.Contains(weak, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{setting} contains the weak token '{weak}'. Configure a unique, high-entropy secret from your secret store.");
        if (IsSingleCharacterRun(trimmed))
            throw new InvalidOperationException($"{setting} must contain varied characters; a repeated-character value is rejected.");
    }

    private static bool IsSingleCharacterRun(string value)
    {
        if (value.Length < 4) return false;
        var first = value[0];
        for (var i = 1; i < value.Length; i++)
            if (value[i] != first) return false;
        return true;
    }
}
