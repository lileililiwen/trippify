# Email Provider Integration

Replace the stub `LocalProviders.SendAsync` with a real email delivery service.

The current `LocalProviders` implementation returns `Task.CompletedTask` and never
delivers email. Development and staging environments need real email delivery so
that the email-confirmation flow actually works end-to-end.

**Resend** is used as the default provider because it requires zero configuration
in local development (uses a fallback replay URL), has a generous free tier, and
exposes a simple REST API.

Production deployments may swap the Resend provider for SendGrid, AWS SES, or any
other SMTP/REST email service by implementing `IEmailSender` without touching
the rest of the application.
