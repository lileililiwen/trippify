# flutter-empty-and-onboarding Specification

## Purpose
TBD - created by archiving change audit-2026-08-27-empty-and-onboarding. Update Purpose after archive.
## Requirements
### Requirement: Post-action confirmation screen
The system SHALL route the user to a dedicated confirmation screen
after registration, displaying a checklist of next steps and a
"Resend verification" action.

#### Scenario: Successful registration
- **WHEN** the registration API succeeds
- **THEN** the app replaces the registration route with a
  confirmation screen that shows the user's email, three
  numbered steps (check inbox, check spam, request resend), and a
  "Resend verification" button that calls the existing resend API

### Requirement: Sign-in preserves caller context
The system SHALL return the user to the route they were on before
sign-in when one exists, and to the home route when the sign-in was
triggered from a fresh launch.

#### Scenario: Sign-in from /discover
- **WHEN** the user taps a protected action on `/discover`, is routed
  to `/sign-in`, and successfully signs in
- **THEN** the user lands back on `/discover` (the previous route)
  and not on `/profile`

#### Scenario: Sign-in from a fresh launch
- **WHEN** the user opens the app and signs in with no prior route
- **THEN** the user lands on `/` (the home route)

### Requirement: Typed error taxonomy
The system SHALL classify every error response from the API into a
typed enum (`notFound`, `unauthorized`, `paymentDeclined`, `network`,
`server`, `unknown`) and SHALL render the matching user-facing copy
via the shared `ErrorState` widget.

#### Scenario: Guide 404
- **WHEN** the public-guide API returns 404
- **THEN** the screen renders the message "This guide is no longer
  available" with a "Browse other guides" CTA

#### Scenario: Payment declined
- **WHEN** the checkout API returns a payment-declined error
- **THEN** the screen renders the message "Your payment was declined.
  Please try a different payment method" with a "Try again" CTA

### Requirement: CTA-paired empty states
Every empty state SHALL include at least one action button pointing
the user toward the next sensible step.

#### Scenario: First-time visitor library
- **WHEN** a signed-in user with no library entries opens
  `/library`
- **THEN** the empty state shows a "Browse the catalog" button that
  navigates to `/discover`

#### Scenario: First-time visitor notifications
- **WHEN** a signed-in user with no notifications opens
  `/notifications`
- **THEN** the empty state shows an "Adjust notification
  preferences" button that navigates to
  `/notification-preferences`

#### Scenario: Empty search results
- **WHEN** the discovery search returns no results
- **THEN** the empty state shows a "Clear filters" button that
  resets the search and pricing filters

