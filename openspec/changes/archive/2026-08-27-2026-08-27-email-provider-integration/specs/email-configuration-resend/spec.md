# Email Configuration Specification

## ADDED Requirements

### Requirement: RESEND_API_KEY configuration
The system SHALL read the `RESEND_API_KEY` configuration value at startup, and SHALL construct a Resend-backed `IEmailSender` when the value is present and non-empty.

#### Scenario: Key configured
- **WHEN** the application starts with `RESEND_API_KEY` set to a non-empty string
- **THEN** the resolved `IEmailSender` issues HTTP requests to `https://api.resend.com/emails` authenticated with the key

#### Scenario: Key absent
- **WHEN** the application starts with `RESEND_API_KEY` unset or empty
- **THEN** the resolved `IEmailSender` completes every send call without issuing an HTTP request

### Requirement: Pluggable provider replacement
The system SHALL resolve `IEmailSender` through the dependency injection container so that production deployments may replace the Resend implementation with another provider (SendGrid, AWS SES, SMTP) without modifying the registration or forgot-password endpoints.

#### Scenario: Custom provider registration
- **WHEN** a deployment registers a different `IEmailSender` implementation in `Program.cs`
- **THEN** the identity endpoints dispatch all email through that implementation
