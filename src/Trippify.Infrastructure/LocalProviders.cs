using Trippify.Application;

namespace Trippify.Infrastructure;

public sealed class NullEmailSender : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellation) => Task.CompletedTask;
}

public sealed class LocalPaymentGateway : IPaymentGateway
{
    public Task<string> CreateCheckoutAsync(long minorUnits, string currency, CancellationToken cancellation)
        => throw new NotSupportedException("Payments are disabled in local mode.");
}

public sealed class LocalAiAssistant : IAiAssistant
{
    public Task<string> AssistAsync(string input, CancellationToken cancellation) => Task.FromResult(input);
}

