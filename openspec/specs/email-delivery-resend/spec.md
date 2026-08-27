# email-delivery-resend Specification

## Purpose
TBD - created by archiving change 2026-08-27-email-provider-integration. Update Purpose after archive.
## Requirements
### Requirement: Resend-backed email delivery
The system SHALL deliver account emails (confirmation, password reset) through the Resend REST API when a `RESEND_API_KEY` is configured, by POSTing a JSON payload to `https://api.resend.com/emails` with a `Bearer` token, a fixed `from` address, and the recipient/subject/body supplied by the application.

#### Scenario: Successful send
- **WHEN** the email sender dispatches a message with a configured API key
- **THEN** the request includes `Authorization: Bearer <key>` and a JSON body with `from`, `to`, `subject`, and `text` fields, and the sender returns successfully on a 2xx response

### Requirement: Graceful failure handling
The system SHALL throw an `HttpRequestException` that includes the response status and body when the Resend API returns a non-2xx response, and the calling endpoints SHALL catch that exception so that registration and password reset still succeed.

#### Scenario: Non-2xx from Resend
- **WHEN** the Resend API responds with a non-success status
- **THEN** the sender raises an exception containing the status and body, and the registration endpoint logs the error and returns success without propagating it to the caller

### Requirement: Stub fallback when no key is configured
The system SHALL complete email send calls without contacting any external service when `RESEND_API_KEY` is empty or absent, so that local development and tests do not require network access.

#### Scenario: Development without a key
- **WHEN** the application starts without `RESEND_API_KEY` and a code path invokes the email sender
- **THEN** the call returns immediately and no HTTP request is issued

