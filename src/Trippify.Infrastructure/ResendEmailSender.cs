using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Trippify.Application;

namespace Trippify.Infrastructure;

// Resend email sender implementation
public sealed class ResendEmailSender : IEmailSender
{
    private readonly HttpClient _httpClient;
    private const string FromEmail = "onboarding@trippify.com";

    public ResendEmailSender(string apiKey) : this(apiKey, new HttpClient()) { }

    public ResendEmailSender(string apiKey, HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var payload = new
        {
            from = FromEmail,
            to = new[] { recipient },
            subject = subject,
            text = body
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            "https://api.resend.com/emails",
            content,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Failed to send email via Resend: {response.StatusCode} - {errorContent}"
            );
        }
    }
}