# Fix registration and email confirmation flow

The Flutter registration screen is missing a confirm-password field, and the
email confirmation flow never delivers the verification message. The current
implementation writes a 204 status to the response even when the
`IEmailSender` would throw, so failures are silent.
