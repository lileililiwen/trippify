# Accessibility Fixes

## Why
The 2026-08-27 UX audit (`docs/ux-audit-2026-08-27.md`, F1, F3–F7, F12,
F13) identified P0 violations of WCAG 1.4.3 (contrast), 1.3.1 (info and
relationships), 3.3.1 (error identification), 3.3.3 (error suggestion),
and 4.1.2 (name, role, value). The most serious is the use of
`Colors.grey` (2.68:1) for body text on `SystemScreen`, which fails
AA. Form errors are not inline, no fields are wrapped in
`TextFormField` with `validator`, two `catch (_) { SizedBox.shrink() }`
sinks silently drop errors, and no screen section has a
`Semantics(header: true)`.

## What Changes
- Replace `Colors.grey` body text with AA-compliant tokens (F1).
- Convert all `TextField` + status-string forms to `Form` +
  `TextFormField` + `validator` chains that render errors inline via
  `InputDecoration.errorText` (F3).
- Add an RFC-5322-light email regex check (F4).
- Surface character limits as `helperText` on review, feedback, and
  description fields (F5, F12).
- Add `Semantics(header: true)` to every screen section title (F6).
- Replace the two `SizedBox.shrink()` error sinks with an `ErrorState`
  widget (F7).
- Wrap the rating row in `Semantics(value:, label: 'Rating: X of 5')`
  and provide a keyboard-accessible alternative (F13).
- Add `label` to every `IconButton` whose icon is the only meaning
  carrier (F13, extends F6).

## Capabilities
### New Capabilities
- `flutter-accessibility`: WCAG 2.2 AA compliance for the Flutter
  client (contrast, semantics, inline form validation).

### Modified Capabilities
- `home-navigation`: section headers in the four home shapes carry
  `Semantics(header: true)`.
- `user-creator-identity`: profile form errors are inline and the
  display-name length rule is enforced by the form, not only the
  server.

# Dependencies and Non-goals
- Dependencies: `home-navigation`, `flutter-design-tokens` (for AA
  color tokens).
- Non-goals: full WCAG 2.2 AAA pass; localization of error messages
  beyond English/Zh (already supported by the localizations delegates).

# Impact
No backend changes. Pure Flutter refactor. A new contrast-golden test
sits next to the existing widget tests.
