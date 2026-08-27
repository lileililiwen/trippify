# Tasks

## Token package
- [x] Create `apps/trippify_flutter/lib/design/tokens.dart` with a
      `TrippifyTokens` `ThemeExtension` covering semantic colors
      (`creator`, `purchased`, `free`, `warning`, `danger`) and
      spacing (`xs..xxl`) and radius (`sm..xl`).
- [x] Create `apps/trippify_flutter/lib/design/theme.dart` exporting
      `lightTheme` and `darkTheme` from a single seed via
      `ColorScheme.fromSeed`.
- [x] Replace `_appTheme` in `main.dart` with the new `lightTheme`.

## SystemScreen rewrite
- [x] Remove the 5 hardcoded `Color(0xFF6750A4)` literals in
      `_SystemScreenState` (header avatar, header name, summary title,
      creator-yes chip).
- [x] Remove the 14 raw `TextStyle(fontSize:)` overrides in
      `_SystemScreenState`; replace with `Theme.of(context).textTheme.*`
      references.
- [x] Replace the `Colors.grey` body text (welcome copy, "isCreator:
      No", "Roles: None", "No") with `colorScheme.onSurfaceVariant`.

## Shared state widgets
- [x] Add `LoadingState`, `ErrorState`, `EmptyState` widgets under
      `lib/design/`.
- [x] Convert `FutureBuilder` blocks in `DiscoveryScreen`,
      `LibraryScreen`, `NotificationsScreen`, `PublicGuideScreen`,
      `AuthorScreen`, `SystemStatusScreen`, `TenantDashboardScreen`,
      `PluginCatalogScreen`, `AssistedImportScreen`, `LicensePanelScreen`,
      `PublicCreatorScreen`, `CreatorEnrollmentScreen` to use the new
      widgets (or document why a screen is exempt).

## Dark theme
- [x] Add `darkTheme:` to `MaterialApp` and `themeMode: ThemeMode.system`.

## Testing
- [x] `flutter analyze` clean.
- [x] `flutter test` passes; existing widget tests still cover the four
      home shapes.
- [x] Add a widget test that asserts the new `EmptyState` renders a CTA
      button when one is supplied.
- [x] Add a contrast test (golden) that measures the body-text color
      against the surface at both light and dark brightnesses and
      asserts ≥ 4.5:1.
