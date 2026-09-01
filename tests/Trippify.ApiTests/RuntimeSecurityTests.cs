using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class StrictCorsFactory : WebApplicationFactory<Program>
{
    public static readonly string ObjectStorageRoot = Path.Combine(Path.GetTempPath(), "trippify-strict-cors-" + Guid.NewGuid().ToString("N"));
    public static readonly string SignedUrlSecret = "strict-cors-secret-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
    public static readonly string PluginSigningSecret = "strict-cors-plugin-" + Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Cors:AllowedOrigins:0", "https://app.example");
        builder.UseSetting("BackgroundJobs:WorkersEnabled", "false");
        builder.UseSetting("ObjectStorage:LocalRoot", ObjectStorageRoot);
        builder.UseSetting("ObjectStorage:PublicBaseUrl", "local://trippify-tests/");
        builder.UseSetting("ObjectStorage:SignedUrlSecret", SignedUrlSecret);
        builder.UseSetting("Plugins:SigningSecret", PluginSigningSecret);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DemoSeed:Enabled"] = "false",
                ["ObjectStorage:Provider"] = "local",
                ["Map:Provider"] = "local",
            });
        });
        builder.ConfigureServices(services =>
        {
            var databaseName = "trippify-strict-cors-" + Guid.NewGuid();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        });
    }
}

public sealed class RuntimeSecurityValidatorTests
{
    private const string StrongObjectStorageSecret = "strong-object-storage-hmac-key-1234567890";
    private const string StrongPaymentApiKey = "live-payment-key-1234567890";
    private const string StrongPaymentWebhookSecret = "strong-webhook-hmac-key-1234567890";
    private const string StrongAiApiKey = "live-ai-key-with-32-random-chars-aaaa";
    private const string StrongPluginSigningSecret = "strong-plugin-hmac-key-1234567890";
    private const string AllowedOrigin = "https://app.example";

    private static Dictionary<string, string?> StrongProductionBase(IDictionary<string, string?>? overrides = null)
    {
        var dict = new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = AllowedOrigin,
            ["ObjectStorage:Provider"] = "s3-compatible",
            ["ObjectStorage:Endpoint"] = "https://s3.example.test",
            ["ObjectStorage:Bucket"] = "trippify",
            ["ObjectStorage:AccessKey"] = "ak",
            ["ObjectStorage:SecretKey"] = "sk",
            ["ObjectStorage:SignedUrlSecret"] = StrongObjectStorageSecret,
            ["Payment:Provider"] = "http",
            ["Payment:Enabled"] = "true",
            ["Payment:Endpoint"] = "https://payments.example.test",
            ["Payment:ApiKey"] = StrongPaymentApiKey,
            ["Payment:WebhookSecret"] = StrongPaymentWebhookSecret,
            ["Ai:Provider"] = "http",
            ["Ai:Enabled"] = "true",
            ["Ai:Endpoint"] = "https://ai.example.test",
            ["Ai:Model"] = "gpt-4o-mini",
            ["Ai:ApiKey"] = StrongAiApiKey,
            ["Plugins:SigningSecret"] = StrongPluginSigningSecret,
        };
        if (overrides is not null)
            foreach (var (k, v) in overrides) dict[k] = v;
        return dict;
    }

    [Fact]
    public void Validate_rejects_weak_object_storage_signed_url_secret_in_production()
    {
        var dict = StrongProductionBase();
        dict["ObjectStorage:SignedUrlSecret"] = "change-me-to-a-long-random-secret";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var env = new TestWebHostEnvironment("Production");
        var ex = Assert.Throws<InvalidOperationException>(() => Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env));
        Assert.Contains("ObjectStorage:SignedUrlSecret", ex.Message);
    }

    [Fact]
    public void Validate_rejects_short_object_storage_signed_url_secret_in_production()
    {
        var dict = StrongProductionBase();
        dict["ObjectStorage:SignedUrlSecret"] = "short";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var env = new TestWebHostEnvironment("Production");
        var ex = Assert.Throws<InvalidOperationException>(() => Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env));
        Assert.Contains("ObjectStorage:SignedUrlSecret", ex.Message);
    }

    [Fact]
    public void Validate_rejects_weak_payment_webhook_secret_in_production()
    {
        var dict = StrongProductionBase();
        dict["Payment:WebhookSecret"] = "secret";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var env = new TestWebHostEnvironment("Production");
        var ex = Assert.Throws<InvalidOperationException>(() => Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env));
        Assert.Contains("Payment:WebhookSecret", ex.Message);
    }

    [Fact]
    public void Validate_rejects_short_payment_webhook_secret_in_production()
    {
        var dict = StrongProductionBase();
        dict["Payment:WebhookSecret"] = "aabbccddeeffgghhiijjkk";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var env = new TestWebHostEnvironment("Production");
        var ex = Assert.Throws<InvalidOperationException>(() => Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env));
        Assert.Contains("Payment:WebhookSecret", ex.Message);
    }

    [Fact]
    public void Validate_rejects_weak_payment_api_key_in_production()
    {
        var dict = StrongProductionBase();
        dict["Payment:ApiKey"] = "weak";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var env = new TestWebHostEnvironment("Production");
        var ex = Assert.Throws<InvalidOperationException>(() => Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env));
        Assert.Contains("Payment:ApiKey", ex.Message);
    }

    [Fact]
    public void Validate_rejects_weak_ai_api_key_in_production()
    {
        var dict = StrongProductionBase();
        dict["Ai:ApiKey"] = "weak";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var env = new TestWebHostEnvironment("Production");
        var ex = Assert.Throws<InvalidOperationException>(() => Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env));
        Assert.Contains("Ai:ApiKey", ex.Message);
    }

    [Fact]
    public void Validate_rejects_weak_plugin_signing_secret_in_production()
    {
        var dict = StrongProductionBase();
        dict["Plugins:SigningSecret"] = "trippify-dev-shared-hmac-secret";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var env = new TestWebHostEnvironment("Production");
        var ex = Assert.Throws<InvalidOperationException>(() => Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env));
        Assert.Contains("Plugins:SigningSecret", ex.Message);
    }

    [Fact]
    public void Validate_rejects_short_plugin_signing_secret_in_production()
    {
        var dict = StrongProductionBase();
        dict["Plugins:SigningSecret"] = "short";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var env = new TestWebHostEnvironment("Production");
        var ex = Assert.Throws<InvalidOperationException>(() => Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env));
        Assert.Contains("Plugins:SigningSecret", ex.Message);
    }

    [Fact]
    public void Validate_rejects_empty_cors_allowed_origins_in_production()
    {
        var configuration = new ConfigurationBuilder().Build();
        var env = new TestWebHostEnvironment("Production");
        var ex = Assert.Throws<InvalidOperationException>(() => Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env));
        Assert.Contains("Cors:AllowedOrigins", ex.Message);
    }

    [Fact]
    public void Validate_rejects_invalid_origin_format_in_production()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "not-a-valid-origin",
            })
            .Build();
        var env = new TestWebHostEnvironment("Production");
        var ex = Assert.Throws<InvalidOperationException>(() => Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env));
        Assert.Contains("Cors:AllowedOrigins", ex.Message);
    }

    [Fact]
    public void Validate_normalizes_allowed_origins_and_accepts_strong_config_in_production()
    {
        var dict = StrongProductionBase();
        dict["Cors:AllowedOrigins:0"] = "HTTPS://App.Example/";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var env = new TestWebHostEnvironment("Production");
        var origins = Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env);
        Assert.Single(origins);
        Assert.Equal(AllowedOrigin, origins[0]);
    }

    [Fact]
    public void Validate_allows_empty_cors_in_development()
    {
        var configuration = new ConfigurationBuilder().Build();
        var env = new TestWebHostEnvironment("Development");
        var origins = Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env);
        Assert.Empty(origins);
    }

    [Fact]
    public void Validate_allows_known_dev_secrets_in_development()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "local",
                ["ObjectStorage:SignedUrlSecret"] = "change-me-to-a-long-random-secret",
                ["Plugins:SigningSecret"] = "trippify-dev-shared-hmac-secret",
            })
            .Build();
        var env = new TestWebHostEnvironment("Development");
        var origins = Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env);
        Assert.Empty(origins);
    }

    [Fact]
    public void Validate_rejects_default_port_in_cors_when_explicit_port_matches_scheme_default()
    {
        var dict = StrongProductionBase();
        dict["Cors:AllowedOrigins:0"] = "https://app.example:443";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var env = new TestWebHostEnvironment("Production");
        var origins = Trippify.Api.RuntimeSecurityValidator.Validate(configuration, env);
        Assert.Single(origins);
        Assert.Equal(AllowedOrigin, origins[0]);
    }

    [Fact]
    public void Normalize_origin_strips_trailing_slash_and_lowercases_scheme_and_host()
    {
        Assert.Equal("https://app.example", Trippify.Api.RuntimeSecurityValidator.NormalizeOrigin("HTTPS://App.Example/"));
        Assert.Equal("http://localhost:3000", Trippify.Api.RuntimeSecurityValidator.NormalizeOrigin("http://localhost:3000/"));
        Assert.Equal("https://app.example", Trippify.Api.RuntimeSecurityValidator.NormalizeOrigin("https://app.example:443"));
        Assert.Equal("http://app.example", Trippify.Api.RuntimeSecurityValidator.NormalizeOrigin("http://app.example:80"));
        Assert.Equal("https://app.example:8443", Trippify.Api.RuntimeSecurityValidator.NormalizeOrigin("HTTPS://App.Example:8443"));
        Assert.Throws<ArgumentException>(() => Trippify.Api.RuntimeSecurityValidator.NormalizeOrigin("not-a-url"));
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment(string environmentName) { EnvironmentName = environmentName; }
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Trippify.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

public sealed class CorsPolicyTests(StrictCorsFactory factory) : IClassFixture<StrictCorsFactory>
{
    [Fact]
    public async Task Preflight_from_configured_origin_returns_allow_origin_and_credentials()
    {
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/system");
        request.Headers.Add("Origin", "https://app.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        var response = await client.SendAsync(request);
        var headersText = string.Join("\n", response.Headers.Select(h => $"{h.Key}: {string.Join(',', h.Value)}"));
        var bodyText = response.Content is null ? "<null>" : await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"status={(int)response.StatusCode} headers={headersText} body={bodyText}");
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"), $"headers={headersText}");
        Assert.Equal("https://app.example", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.True(response.Headers.Contains("Access-Control-Allow-Credentials"), $"headers={headersText}");
        Assert.Equal("true", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Credentials")));
    }

    [Fact]
    public async Task Preflight_from_unconfigured_origin_does_not_receive_allow_origin()
    {
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/system");
        request.Headers.Add("Origin", "https://attacker.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        var response = await client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Actual_request_from_unconfigured_origin_does_not_receive_allow_origin()
    {
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system");
        request.Headers.Add("Origin", "https://attacker.example");
        var response = await client.SendAsync(request);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Credentialed_request_from_configured_origin_succeeds()
    {
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system");
        request.Headers.Add("Origin", "https://app.example");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "no-token-needed");
        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.Equal("https://app.example", string.Join(',', response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Health_endpoints_do_not_leak_secrets_in_response_headers()
    {
        using var client = factory.CreateClient();
        var live = await client.GetAsync("/health/live");
        Assert.True(live.IsSuccessStatusCode);
        var liveBody = await live.Content.ReadAsStringAsync();
        Assert.DoesNotContain(StrictCorsFactory.SignedUrlSecret, liveBody);
        Assert.DoesNotContain(StrictCorsFactory.PluginSigningSecret, liveBody);
        Assert.DoesNotContain("change-me-to-a-long-random-secret", liveBody, StringComparison.OrdinalIgnoreCase);
    }
}
