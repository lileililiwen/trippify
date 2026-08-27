# Email Provider Integration

## Why
The `LocalProviders` email sender is a stub that returns `Task.CompletedTask`.
It never delivers email, so the email-confirmation flow is broken in all
environments. Users register but never receive the confirmation link, and
password-reset emails never arrive.

## What Changes
- Replace `LocalProviders.SendAsync` with a Resend-backed implementation
- Add `RESEND_API_KEY` to configuration
- Keep the stub implementation available for tests and offline work
- Update the `IdentityEmailSender` adapter to handle Resend responses

## Capabilities
### New Capabilities
- `email-delivery-resend`: real email delivery via Resend API
- `email-configuration-resend`: `RESEND_API_KEY` environment variable

### Modified Capabilities
- `user-creator-identity`: real email confirmation and password reset
- `registration`: email confirmation now actually sends
- `forgot-password`: password reset emails now actually send

# Dependencies and Non-goals
- Dependencies: `establish-platform-foundation`
- Non-goals: change email template content, add email retry logic, modify
  password complexity requirements

# Impact
Adds real email delivery to all environments. Registration and password-reset
flows now work end-to-end. The change is backward-compatible: if
`RESEND_API_KEY` is not set, the stub implementation is used automatically.