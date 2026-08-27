using Trippify.Application;

namespace Trippify.Infrastructure;

public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellation) => Task.CompletedTask;
}

public sealed class LocalPaymentGateway : IPaymentGateway
{
    public string ProviderName => "local";
    public Task<CheckoutSession> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellation)
        => throw new NotSupportedException("Payments are disabled in local mode.");
}

public sealed class LocalAiAssistant : IAiAssistant
{
    public string ProviderName => "local";
    public AiAssistResult Disabled() => new(AiAssistStatus.Disabled, null, null, null, "local", "local", "v1", 0, false, "provider-disabled");
    public Task<AiAssistResult> AssistAsync(AiAssistRequest request, CancellationToken cancellation)
        => Task.FromResult(Disabled());
}
