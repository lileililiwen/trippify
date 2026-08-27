# Tasks

## Form refactor (F3, F4, F5, F12)
- [x] Convert `_RegistrationScreenState` to `Form` + `TextFormField` with
      `validator`; render errors via `InputDecoration.errorText`; add
      email regex and confirm-password matcher.
- [x] Convert `_SignInScreenState` to `Form` + `TextFormField` with
      `validator`; inline email/password errors.
- [x] Convert `_ProfileScreenState` display-name field to
      `TextFormField` with a `maxLength` and `helperText` matching the
      server rule.
- [x] Convert `_ReviewSectionState` body field to `TextFormField` with
      `maxLength: 4000`, `minLines: 4`, and a `helperText` showing the
      30–4000 bound.
- [x] Convert the public guide feedback field to `TextFormField` with
      the same min/max surface.

## Contrast and color (F1, F2)
- [x] Replace `Colors.grey` body text on `_SystemScreenState` with the
      AA-compliant `colorScheme.onSurfaceVariant` token.
- [x] Replace the 5 hardcoded `Color(0xFF6750A4)` literals on
      `_SystemScreenState` with `Theme.of(context).colorScheme.primary`.

## Semantics (F6, F13)
- [x] Add `Semantics(header: true, child: ...)` to every screen-section
      title across all 22 screens.
- [x] Wrap the rating `Row` in `_ReviewSectionState` in
      `Semantics(container: true, value: rating, label: 'Rating: $rating of 5')`
      and label each `IconButton` with `Semantics(label: 'Set rating to $i')`.
- [x] Add `Semantics(label: ...)` to every icon-only `IconButton` that
      lacks a `tooltip:`.

## Error sinks (F7)
- [x] Replace the `SizedBox.shrink()` in `_ReviewSectionState` (line
      2064) with `ErrorState(message: ..., onRetry: () => setState(...))`.
- [x] Replace the `SizedBox.shrink()` in `_ReleasesSection._freshnessView`
      (line 987) with `ErrorState(message: ..., onRetry: ...)`.

## Testing
- [x] `flutter analyze` clean.
- [x] `flutter test` passes; existing widget tests updated to use
      `find.byType(TextFormField)` and `find.text('Email is invalid')`.
- [x] Add a contrast golden test that renders the four home shapes at
      light and dark and asserts ≥ 4.5:1.
- [x] Add a widget test that asserts an inline `errorText` appears on
      the offending field after submit with bad data.
