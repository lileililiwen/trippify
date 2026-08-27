# Empty States and Onboarding

## Why
The 2026-08-27 UX audit (`docs/ux-audit-2026-08-27.md`, F15, F16,
F22–F25) flagged that:
- `SignInScreen` does `pushReplacementNamed('/profile')` after login,
  so users who came from `/discover` lose their place.
- `RegistrationScreen` after success shows a status string with no
  next step.
- Empty states are helpful in copy but never paired with a CTA.
- "Guide not found.", "Discovery is unavailable.", and "Planning
  access denied" all use the same generic error text for 404, 500,
  and offline.
- `PublicGuideScreen` checkout does not show the price next to the
  "Buy and unlock" button.

## What Changes
- Replace `pushReplacementNamed('/profile')` with
  `Navigator.popUntil` returning to the previous route.
- Add a `RegistrationConfirmationScreen` that renders after
  registration with explicit next steps (check inbox, spam folder,
  resend) and a "Resend verification" action.
- Pair every `EmptyState` with at least one CTA pointing at the
  primary user action.
- Add a typed error enum so screens render the right copy for 404,
  500, and offline.
- Display the price next to the "Buy and unlock" button on
  `PublicGuideScreen` so the user sees the cost before tapping.
- Add first-time-user empty states on Library and Notifications
  with a "Browse the catalog" / "Adjust notification preferences"
  CTA.

## Capabilities
### New Capabilities
- `flutter-empty-and-onboarding`: typed error taxonomy, CTA-paired
  empty states, and an explicit registration-confirmation flow.

### Modified Capabilities
- `home-navigation`: the anonymous home gains a "Browse the catalog"
  CTA pointing at `/discover`.
- `user-creator-identity`: registration completion routes to a
  confirmation screen rather than leaving the user on the form.

# Dependencies and Non-goals
- Dependencies: `home-navigation`, `flutter-design-tokens`,
  `flutter-accessibility`, `flutter-feedback-patterns`.
- Non-goals: full in-app tour / coach marks (deferred to a separate
  change); push-notification onboarding (covered by
  `add-follows-and-notifications` in the roadmap).

# Impact
No backend changes. Pure Flutter refactor. Existing widget tests must
be updated to assert the new confirmation flow and the typed errors.
