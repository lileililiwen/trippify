# Tasks

## Sign-in flow
- [x] Replace `pushReplacementNamed('/profile')` in
      `_SignInScreenState.submit` with
      `Navigator.popUntil(context, (r) => r.isFirst) || Navigator.pushReplacementNamed(context, '/')`.

## Registration completion
- [x] Add a `RegistrationConfirmationScreen` (route `/register/done`)
      with a checklist (check inbox, check spam, request resend) and
      a "Resend verification" action that calls the existing resend
      API.
- [x] In `_RegistrationScreenState.submit`, after successful
      `register`, `Navigator.pushReplacementNamed(context,
      '/register/done')`.

## Typed error enum
- [x] Add `enum AppError { notFound, unauthorized, paymentDeclined,
      network, server, unknown }` in `lib/api_client.dart`.
- [x] Replace the `catch (_) { ... }` blocks in
      `PublicGuideScreen`, `LibraryScreen`, `NotificationsScreen`,
      `AuthorScreen`, `PlanningScreen` with a `try/catch` that maps
      the error to `AppError` and renders the typed `ErrorState`.
- [x] Wire the existing HTTP error mapping in `api_client.dart` to
      produce `AppError` rather than raw exceptions.

## Empty states with CTAs
- [x] Add a "Browse the catalog" CTA on the empty states for
      `LibraryScreen` and `NotificationsScreen`.
- [x] Add a "Become a creator" CTA on the empty state of
      `CreatorDashboardScreen` (when the user is signed in but not
      yet a creator).
- [x] Add a "Browse the catalog" CTA on the empty state of
      `DiscoveryScreen` (the search returned no results).
- [x] Add a "Browse the catalog" CTA on the anonymous home in
      `_SystemScreenState._buildAnonymousHome`.

## Pricing visibility
- [x] On `PublicGuideScreen`, render the price
      (`${data.priceMinorUnits} ${data.currencyCode}`) as the
      `FilledButton.label` (or in a `Text` next to the button) so the
      user sees the cost before tapping "Buy and unlock".

## Testing
- [x] `flutter analyze` clean.
- [x] `flutter test` passes; existing widget tests updated to assert
      the new screen flow.
- [x] Add a widget test that asserts the registration confirmation
      screen renders the checklist and the resend action.
- [x] Add a widget test that asserts each empty state surfaces a CTA
      button.
- [x] Add a widget test that asserts a 404 from the public-guide
      endpoint renders the "no longer available" copy.
