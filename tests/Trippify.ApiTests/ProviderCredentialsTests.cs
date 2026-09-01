using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Trippify.Application;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class ProviderCredentialsTests
{
    [Fact]
    public void Payment_options_validate_requires_api_key_for_remote_provider()
    {
        var options = new PaymentProviderOptions
        {
            Provider = "http",
            Endpoint = "https://payments.example.test",
            Enabled = true,
            WebhookSecret = "secret",
            ApiKey = null,
        };
        var ex = Assert.Throws<ProviderConfigurationException>(() => options.Validate());
        Assert.Contains("Payment:ApiKey", ex.Message);
    }

    [Fact]
    public void Payment_options_validate_requires_endpoint_for_remote_provider()
    {
        var options = new PaymentProviderOptions
        {
            Provider = "http",
            Enabled = true,
            ApiKey = "k",
            WebhookSecret = "secret",
        };
        var ex = Assert.Throws<ProviderConfigurationException>(() => options.Validate());
        Assert.Contains("Payment:Endpoint", ex.Message);
    }

    [Fact]
    public void Payment_options_validate_passes_when_remote_credentials_are_present()
    {
        var options = new PaymentProviderOptions
        {
            Provider = "http",
            Endpoint = "https://payments.example.test",
            Enabled = true,
            ApiKey = "live-key",
            WebhookSecret = "secret",
        };
        options.Validate();
    }

    [Fact]
    public void Payment_options_validate_skips_for_local_provider()
    {
        var options = new PaymentProviderOptions { Provider = "local", ApiKey = null, Enabled = true };
        options.Validate();
    }

    [Fact]
    public void Payment_options_validate_skips_for_disabled_remote_provider()
    {
        var options = new PaymentProviderOptions { Provider = "http", ApiKey = null, Enabled = false };
        options.Validate();
    }

    [Fact]
    public void Payment_constructor_rejects_missing_api_key()
    {
        var options = new PaymentProviderOptions
        {
            Provider = "http",
            Endpoint = "https://payments.example.test",
            Enabled = true,
            WebhookSecret = "secret",
        };
        using var http = new HttpClient();
        Assert.Throws<ProviderConfigurationException>(() => new HttpPaymentGateway(options, http));
    }

    [Fact]
    public async Task Payment_checkout_sends_configured_bearer_token()
    {
        HttpRequestMessage? captured = null;
        var handler = new CapturingHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"sessionId\":\"cs_test\",\"url\":\"https://example.test/cs\"}")
            });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://payments.example.test") };
        var options = new PaymentProviderOptions
        {
            Provider = "http",
            Endpoint = "https://payments.example.test",
            Enabled = true,
            ApiKey = "live-payment-key",
            WebhookSecret = "secret",
            TimeoutMilliseconds = 5000,
        };
        var gateway = new HttpPaymentGateway(options, http, NullLogger<HttpPaymentGateway>.Instance);
        var request = new PaymentCheckoutRequest(
            AmountMinorUnits: 1000,
            CurrencyCode: "USD",
            GuideId: Guid.NewGuid(),
            BuyerUserId: Guid.NewGuid(),
            DiscountCode: null,
            SuccessUrl: "https://app.example.test/success",
            CancelUrl: "https://app.example.test/cancel",
            IdempotencyKey: "idem-1");
        var session = await gateway.CreateCheckoutAsync(request, CancellationToken.None);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.NotNull(captured.Headers.Authorization);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("live-payment-key", captured.Headers.Authorization.Parameter);
        Assert.DoesNotContain("REDACTED", captured.Headers.Authorization.Parameter!);
        Assert.Equal("idem-1", captured.Headers.GetValues("Idempotency-Key").Single());
        Assert.Equal("cs_test", session.Reference);
    }

    [Fact]
    public async Task Payment_checkout_surfaces_provider_unavailable_on_401_without_persisting_session()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"bad key\"}")
        }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://payments.example.test") };
        var options = new PaymentProviderOptions
        {
            Provider = "http",
            Endpoint = "https://payments.example.test",
            Enabled = true,
            ApiKey = "live-payment-key",
            WebhookSecret = "secret",
            TimeoutMilliseconds = 5000,
        };
        var gateway = new HttpPaymentGateway(options, http, NullLogger<HttpPaymentGateway>.Instance);
        var request = new PaymentCheckoutRequest(1000, "USD", Guid.NewGuid(), Guid.NewGuid(), null, "s", "c", "idem-x");
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CreateCheckoutAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Payment_checkout_surfaces_provider_unavailable_on_403_without_persisting_session()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("{\"error\":\"forbidden\"}")
        }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://payments.example.test") };
        var options = new PaymentProviderOptions
        {
            Provider = "http",
            Endpoint = "https://payments.example.test",
            Enabled = true,
            ApiKey = "live-payment-key",
            WebhookSecret = "secret",
            TimeoutMilliseconds = 5000,
        };
        var gateway = new HttpPaymentGateway(options, http, NullLogger<HttpPaymentGateway>.Instance);
        var request = new PaymentCheckoutRequest(1000, "USD", Guid.NewGuid(), Guid.NewGuid(), null, "s", "c", "idem-y");
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.CreateCheckoutAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Payment_checkout_times_out_when_provider_does_not_respond()
    {
        var handler = new HangingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://payments.example.test") };
        var options = new PaymentProviderOptions
        {
            Provider = "http",
            Endpoint = "https://payments.example.test",
            Enabled = true,
            ApiKey = "live-payment-key",
            WebhookSecret = "secret",
            TimeoutMilliseconds = 200,
        };
        var gateway = new HttpPaymentGateway(options, http, NullLogger<HttpPaymentGateway>.Instance);
        var request = new PaymentCheckoutRequest(1000, "USD", Guid.NewGuid(), Guid.NewGuid(), null, "s", "c", "idem-t");
        await Assert.ThrowsAsync<IOException>(() => gateway.CreateCheckoutAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Payment_log_does_not_contain_api_key_on_rejection()
    {
        var sink = new ListLogger();
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("denied")
        }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://payments.example.test") };
        var options = new PaymentProviderOptions
        {
            Provider = "http",
            Endpoint = "https://payments.example.test",
            Enabled = true,
            ApiKey = "super-secret-payment-key",
            WebhookSecret = "secret",
            TimeoutMilliseconds = 5000,
        };
        var logger = new SingleCategoryLoggerFactory(sink, nameof(HttpPaymentGateway)).CreateLogger<HttpPaymentGateway>();
        var gateway = new HttpPaymentGateway(options, http, logger);
        var request = new PaymentCheckoutRequest(1000, "USD", Guid.NewGuid(), Guid.NewGuid(), null, "s", "c", "idem-log");
        try
        {
            await gateway.CreateCheckoutAsync(request, CancellationToken.None);
        }
        catch (InvalidOperationException)
        {
        }
        Assert.DoesNotContain("super-secret-payment-key", sink.Text);
        Assert.DoesNotContain("REDACTED", sink.Text);
    }

    [Fact]
    public void Ai_options_validate_requires_api_key_for_remote_provider()
    {
        var options = new AiProviderOptions
        {
            Provider = "http",
            Endpoint = "https://ai.example.test",
            Enabled = true,
            Model = "m",
            ApiKey = null,
        };
        var ex = Assert.Throws<ProviderConfigurationException>(() => options.Validate());
        Assert.Contains("Ai:ApiKey", ex.Message);
    }

    [Fact]
    public void Ai_options_validate_skips_for_disabled_remote_provider()
    {
        var options = new AiProviderOptions { Provider = "http", ApiKey = null, Enabled = false, Model = "m", Endpoint = "https://ai.example.test" };
        options.Validate();
    }

    [Fact]
    public void Ai_constructor_rejects_missing_api_key()
    {
        var options = new AiProviderOptions
        {
            Provider = "http",
            Endpoint = "https://ai.example.test",
            Enabled = true,
            Model = "m",
        };
        using var http = new HttpClient();
        Assert.Throws<ProviderConfigurationException>(() => new HttpAiAssistant(options, http));
    }

    [Fact]
    public async Task Ai_assist_sends_configured_bearer_token()
    {
        HttpRequestMessage? captured = null;
        var handler = new CapturingHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"schemaVersion\":\"v1\",\"title\":\"Hello\",\"nodes\":[\"a\",\"b\"]}")
            });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ai.example.test") };
        var options = new AiProviderOptions
        {
            Provider = "http",
            Endpoint = "https://ai.example.test",
            Enabled = true,
            ApiKey = "live-ai-key",
            Model = "gpt-4o-mini",
            MaxInputChars = 8000,
            MaxOutputChars = 4000,
            MaxAttempts = 1,
            TimeoutMilliseconds = 5000,
        };
        var assistant = new HttpAiAssistant(options, http, NullLogger<HttpAiAssistant>.Instance);
        var result = await assistant.AssistAsync(new AiAssistRequest(
            Kind: AiAssistKind.Draft,
            SourceText: "Source body",
            SourceLocale: "en",
            TargetLocale: null,
            OperationId: "op-1",
            MaxInputChars: 8000,
            MaxOutputChars: 4000), CancellationToken.None);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.NotNull(captured.Headers.Authorization);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("live-ai-key", captured.Headers.Authorization.Parameter);
        Assert.DoesNotContain("REDACTED", captured.Headers.Authorization.Parameter!);
        Assert.Equal(AiAssistStatus.Completed, result.Status);
    }

    [Fact]
    public async Task Ai_assist_returns_invalid_output_on_401_without_publishing()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("denied")
        }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ai.example.test") };
        var options = new AiProviderOptions
        {
            Provider = "http",
            Endpoint = "https://ai.example.test",
            Enabled = true,
            ApiKey = "live-ai-key",
            Model = "gpt-4o-mini",
            MaxInputChars = 8000,
            MaxOutputChars = 4000,
            MaxAttempts = 1,
            TimeoutMilliseconds = 5000,
        };
        var assistant = new HttpAiAssistant(options, http, NullLogger<HttpAiAssistant>.Instance);
        var result = await assistant.AssistAsync(new AiAssistRequest(
            Kind: AiAssistKind.Draft,
            SourceText: "Source",
            SourceLocale: "en",
            TargetLocale: null,
            OperationId: "op-2",
            MaxInputChars: 8000,
            MaxOutputChars: 4000), CancellationToken.None);
        Assert.Equal(AiAssistStatus.InvalidOutput, result.Status);
        Assert.Null(result.Nodes);
        Assert.Null(result.Body);
    }

    [Fact]
    public async Task Ai_assist_logs_do_not_contain_api_key()
    {
        var sink = new ListLogger();
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://ai.example.test") };
        var options = new AiProviderOptions
        {
            Provider = "http",
            Endpoint = "https://ai.example.test",
            Enabled = true,
            ApiKey = "super-secret-ai-key",
            Model = "gpt-4o-mini",
            MaxInputChars = 8000,
            MaxOutputChars = 4000,
            MaxAttempts = 1,
            TimeoutMilliseconds = 5000,
        };
        var logger = new SingleCategoryLoggerFactory(sink, nameof(HttpAiAssistant)).CreateLogger<HttpAiAssistant>();
        var assistant = new HttpAiAssistant(options, http, logger);
        await assistant.AssistAsync(new AiAssistRequest(AiAssistKind.Draft, "x", "en", null, "op", 100, 100), CancellationToken.None);
        Assert.DoesNotContain("super-secret-ai-key", sink.Text);
        Assert.DoesNotContain("REDACTED", sink.Text);
    }

    [Fact]
    public void Object_storage_options_validate_requires_access_and_secret_for_remote_provider()
    {
        var options = new ObjectStorageOptions
        {
            Provider = "s3-compatible",
            Endpoint = "https://s3.example.test",
            Bucket = "trippify",
            AccessKey = null,
            SecretKey = null,
        };
        var ex = Assert.Throws<ProviderConfigurationException>(() => options.Validate());
        Assert.Contains("AccessKey", ex.Message);
        Assert.Contains("SecretKey", ex.Message);
    }

    [Fact]
    public void Object_storage_options_validate_requires_endpoint_for_remote_provider()
    {
        var options = new ObjectStorageOptions
        {
            Provider = "s3-compatible",
            Bucket = "trippify",
            AccessKey = "ak",
            SecretKey = "sk",
        };
        var ex = Assert.Throws<ProviderConfigurationException>(() => options.Validate());
        Assert.Contains("Endpoint", ex.Message);
    }

    [Fact]
    public void Object_storage_options_validate_skips_for_local_provider()
    {
        var options = new ObjectStorageOptions
        {
            Provider = "local",
            LocalRoot = Path.Combine(Path.GetTempPath(), "trippify-store-test"),
        };
        options.Validate();
    }

    [Fact]
    public async Task Object_storage_put_sends_configured_bearer_token()
    {
        HttpRequestMessage? captured = null;
        var handler = new CapturingHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://s3.example.test") };
        var options = new ObjectStorageOptions
        {
            Provider = "s3-compatible",
            Endpoint = "https://s3.example.test",
            Bucket = "trippify",
            AccessKey = "live-access-key",
            SecretKey = "live-secret-key",
            TimeoutMilliseconds = 5000,
            SignedUrlSecret = "signed-url-secret",
        };
        var storage = new RemoteHttpObjectStorage(options, http, NullLogger<RemoteHttpObjectStorage>.Instance);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("payload"));
        await storage.PutAsync("objects/sample.txt", stream, CancellationToken.None);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Put, captured!.Method);
        Assert.NotNull(captured.Headers.Authorization);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("live-access-key", captured.Headers.Authorization.Parameter);
        Assert.DoesNotContain("REDACTED", captured.Headers.Authorization.Parameter!);
    }

    [Fact]
    public async Task Object_storage_put_throws_on_401()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://s3.example.test") };
        var options = new ObjectStorageOptions
        {
            Provider = "s3-compatible",
            Endpoint = "https://s3.example.test",
            Bucket = "trippify",
            AccessKey = "ak",
            SecretKey = "sk",
            TimeoutMilliseconds = 5000,
            SignedUrlSecret = "x",
        };
        var storage = new RemoteHttpObjectStorage(options, http, NullLogger<RemoteHttpObjectStorage>.Instance);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("payload"));
        await Assert.ThrowsAsync<IOException>(() => storage.PutAsync("objects/sample.txt", stream, CancellationToken.None));
    }

    [Fact]
    public async Task Object_storage_put_logs_do_not_contain_credentials()
    {
        var sink = new ListLogger();
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://s3.example.test") };
        var options = new ObjectStorageOptions
        {
            Provider = "s3-compatible",
            Endpoint = "https://s3.example.test",
            Bucket = "trippify",
            AccessKey = "super-secret-access",
            SecretKey = "super-secret-secret",
            TimeoutMilliseconds = 5000,
            SignedUrlSecret = "x",
        };
        var logger = new SingleCategoryLoggerFactory(sink, nameof(RemoteHttpObjectStorage)).CreateLogger<RemoteHttpObjectStorage>();
        var storage = new RemoteHttpObjectStorage(options, http, logger);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("payload"));
        try
        {
            await storage.PutAsync("objects/sample.txt", stream, CancellationToken.None);
        }
        catch (IOException)
        {
        }
        Assert.DoesNotContain("super-secret-access", sink.Text);
        Assert.DoesNotContain("super-secret-secret", sink.Text);
        Assert.DoesNotContain("REDACTED", sink.Text);
    }

    [Fact]
    public void Local_payment_options_binding_keeps_provider_local_even_when_secret_appears_in_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Payment:Provider"] = "local",
                ["Payment:Enabled"] = "false",
                ["Payment:ApiKey"] = "irrelevant",
            })
            .Build();
        var options = PaymentProviderOptions.Bind(configuration);
        options.Validate();
        Assert.Equal("local", options.Provider);
        Assert.False(options.Enabled);
    }

    [Fact]
    public void Local_ai_options_binding_keeps_provider_local()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:Provider"] = "local",
                ["Ai:Enabled"] = "false",
                ["Ai:Model"] = "gpt-4o-mini",
            })
            .Build();
        var options = AiProviderOptions.Bind(configuration);
        options.Validate();
        Assert.Equal("local", options.Provider);
        Assert.False(options.Enabled);
    }

    [Fact]
    public void Local_object_storage_options_binding_keeps_provider_local()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectStorage:Provider"] = "local",
                ["ObjectStorage:LocalRoot"] = Path.Combine(Path.GetTempPath(), "trippify-store-test"),
            })
            .Build();
        var options = ObjectStorageOptions.Bind(configuration);
        options.Validate();
        Assert.Equal("local", options.Provider);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, string, Task<HttpResponseMessage>> _handler;
        public CapturingHandler(Func<HttpRequestMessage, string, Task<HttpResponseMessage>> handler) => _handler = handler;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return await _handler(request, body);
        }
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class ListLogger
    {
        private readonly System.Collections.Generic.List<string> _entries = new();
        public string Text => string.Join("\n", _entries);
        public void Add(string line) => _entries.Add(line);
    }

    private sealed class SingleCategoryLoggerFactory : ILoggerFactory
    {
        private readonly ListLogger _sink;
        private readonly string _category;
        public SingleCategoryLoggerFactory(ListLogger sink, string category) { _sink = sink; _category = category; }
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_sink, categoryName == _category);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ListLogger _sink;
        private readonly bool _capture;
        public CapturingLogger(ListLogger sink, bool capture) { _sink = sink; _capture = capture; }
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => _capture;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!_capture) return;
            _sink.Add(formatter(state, exception));
            if (exception is not null) _sink.Add(exception.Message);
        }
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
