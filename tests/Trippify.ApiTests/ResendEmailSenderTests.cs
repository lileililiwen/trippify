using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Trippify.Infrastructure;
using Xunit;

namespace Trippify.ApiTests;

public sealed class ResendEmailSenderTests
{
    [Fact]
    public async Task SendAsync_posts_payload_with_bearer_token_and_returns_on_2xx()
    {
        HttpRequestMessage? captured = null;
        string? capturedBody = null;
        var handler = new CapturingHandler((request, body) =>
        {
            captured = request;
            capturedBody = body;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"abc\"}") });
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com") };
        var sender = new ResendEmailSender("test-key", http);

        await sender.SendAsync("user@example.com", "Welcome", "Hello world", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("/emails", captured.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", captured.Headers.Authorization.Parameter);
        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        Assert.Equal("onboarding@trippify.com", doc.RootElement.GetProperty("from").GetString());
        Assert.Equal("user@example.com", doc.RootElement.GetProperty("to")[0].GetString());
        Assert.Equal("Welcome", doc.RootElement.GetProperty("subject").GetString());
        Assert.Equal("Hello world", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendAsync_throws_HttpRequestException_when_resend_returns_non_2xx()
    {
        var handler = new CapturingHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"bad key\"}")
        }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com") };
        var sender = new ResendEmailSender("bad-key", http);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sender.SendAsync("user@example.com", "x", "y", CancellationToken.None));
        Assert.Contains("Unauthorized", ex.Message);
        Assert.Contains("bad key", ex.Message);
    }

    [Fact]
    public async Task LocalEmail_without_api_key_completes_send_without_calling_resend()
    {
        var sender = new NullEmailSender();
        await sender.SendAsync("user@example.com", "x", "y", CancellationToken.None);
    }

    [Fact]
    public async Task LocalEmail_with_api_key_delegates_to_resend()
    {
        HttpRequestMessage? captured = null;
        var handler = new CapturingHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com") };
        var sender = new ResendEmailSender("live-key", http);

        await sender.SendAsync("user@example.com", "x", "y", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization!.Scheme);
        Assert.Equal("live-key", captured.Headers.Authorization.Parameter);
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
}
