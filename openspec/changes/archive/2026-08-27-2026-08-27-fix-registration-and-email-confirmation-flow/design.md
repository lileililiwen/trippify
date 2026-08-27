# Fix registration and email confirmation flow

## Why
The Flutter registration screen is missing a confirm-password field, and the
email confirmation flow never delivers the verification message. The current
implementation writes a 204 status to the response even when the
`IEmailSender` would throw, so failures are silent.

## What Changes
- Add a confirm-password field to the Flutter registration screen
- Fix the email confirmation flow to properly handle `IEmailSender` failures
- Ensure the API returns appropriate error responses when email sending fails
- Add validation to confirm passwords match before submission

## Capabilities
## New Capabilities
- `registration-password-confirmation`: client-side password confirmation validation
- `email-confirmation-error-handling`: server-side error handling for email sending failures

## Modified Capabilities
- `user-creator-identity`: add email confirmation error handling
- `registration`: add password confirmation validation

# Dependencies and Non-goals
- Dependencies: `establish-platform-foundation`
- Non-goals: change email template content, add email retry logic, modify password complexity requirements

# Impact
Adds password confirmation validation on the client side, improves error handling for email sending failures, and provides better feedback to users when registration or email confirmation fails.