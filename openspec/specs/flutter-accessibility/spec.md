# flutter-accessibility Specification

## Purpose
TBD - created by archiving change audit-2026-08-27-accessibility-fixes. Update Purpose after archive.
## Requirements
### Requirement: AA contrast for body text
Every body-text element in light and dark mode SHALL meet WCAG 1.4.3
(≥ 4.5:1) against the surface it sits on.

#### Scenario: SystemScreen muted text
- **WHEN** `_SystemScreenState` renders the welcome copy, the "isCreator:
  No" label, the "Roles: None" label, and the "No" status string
- **THEN** the rendered text color resolves to a token whose contrast
  on `colorScheme.surface` is at least 4.5:1 in both brightnesses

### Requirement: Inline form validation
Every form-bearing screen SHALL render validation errors next to the
offending field via `InputDecoration.errorText` rather than as a
status string at the bottom of the form.

#### Scenario: Registration password mismatch
- **WHEN** the user enters mismatched passwords and taps "Create account"
- **THEN** the confirm-password field shows the error text "Passwords do
  not match" and the submit button does not advance

#### Scenario: Registration weak email
- **WHEN** the user enters "not-an-email" and taps "Create account"
- **THEN** the email field shows the error text "Enter a valid email
  address" and the submit button does not advance

### Requirement: Visible field bounds
Every form field with a server-enforced length rule SHALL display the
rule as `helperText` and SHALL apply a `maxLength` that prevents typing
past the upper bound.

#### Scenario: Review body length
- **WHEN** the user opens the review form
- **THEN** the body field shows the helper text "30–4000 characters"
  and caps input at 4000 characters

### Requirement: Semantic structure
Every screen section title SHALL be wrapped in
`Semantics(header: true, ...)`, and every icon-only `IconButton` SHALL
carry a `Semantics(label:)` or `tooltip:` describing its action.

#### Scenario: Review form rating
- **WHEN** a TalkBack user focuses the rating row
- **THEN** the screen reader announces "Rating: 3 of 5" rather than
  five unlabeled buttons

#### Scenario: Icon-only action
- **WHEN** a screen renders an `IconButton` with only an icon
- **THEN** the action is announced by a screen reader via the button's
  label or `tooltip`

### Requirement: Visible error states
Every `FutureBuilder` that can fail SHALL render an `ErrorState` widget
with a message and a retry button, never a `SizedBox.shrink()`.

#### Scenario: Reviews load failure
- **WHEN** the reviews future rejects
- **THEN** the reviews list area shows the typed error message and a
  retry button

