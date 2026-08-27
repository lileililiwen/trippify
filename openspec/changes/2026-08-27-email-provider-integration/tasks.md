# Tasks

## Resend Sender Implementation
- [x] Add ResendEmailSender that POSTs to `https://api.resend.com/emails`
- [x] LocalProviders reads `RESEND_API_KEY` and falls back to stub when absent
- [x] Register endpoint catches email-sender exceptions so registration still succeeds
- [x] Password reset uses the same email pipeline

## Unit Tests
- [x] Add unit tests for ResendEmailSender (success, non-2xx response, request payload)
- [x] Add unit test for LocalProviders stub fallback when `RESEND_API_KEY` is unset
- [x] Add unit test for LocalProviders delegating to Resend when the key is set

## Configuration & Documentation
- [x] Document `RESEND_API_KEY` in README prerequisites section
- [x] Document `RESEND_API_KEY` in docs/identity.md
- [x] Add a documented placeholder for `RESEND_API_KEY` in docker-compose.yml

## Specs
- [x] Add `email-delivery-resend` capability spec
- [x] Add `email-configuration-resend` capability spec
