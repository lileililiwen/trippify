# Tasks

## Navigation shell
- [x] Create `apps/trippify_flutter/lib/shell/signed_in_shell.dart`
      rendering a `Scaffold` with a `NavigationBar` (4 destinations:
      home, discover, my library, plan; creators see a 5th "Create"
      tab replacing "Plan").
- [x] Register a `/shell/*` set of routes in `MaterialApp` that mount
      each signed-in destination inside the shell.
- [x] Add a `bottomNavigationBar` slot on `_SystemScreenState` and
      remove the three `_actionButton` entries; the shell owns the
      navigation now.
- [x] Update `/profile`, `/notifications`, `/notification-preferences`,
      `/plugins`, `/tenant`, `/assisted-import`, `/license-panel`,
      `/system`, `/creator/dashboard`, `/admin/operations` to mount
      inside the shell.

## Pop and dialog
- [x] Add `PopScope(canPop: false, onPopInvoked: ...)` to
      `_ProfileScreenState` and `GuideWorkspaceScreen` (and any other
      form-bearing screen with unsaved-changes risk); the `onPopInvoked`
      callback shows an `AlertDialog` with "Discard changes" / "Keep
      editing".

## Snackbar
- [x] Add `ScaffoldMessenger.showSnackBar` for every transient
      success in `PublicGuideScreen` (favorite added, favorite removed,
      fork created, trip saved) and `LibraryScreen` (trip deleted,
      trip title updated).
- [x] Add an `errorBuilder`-style snackbar for the catch-all errors
      in `PublicGuideScreen`, `LibraryScreen`, `NotificationsScreen`,
      `AuthorScreen` (replace the status string with a snackbar).

## RefreshIndicator
- [x] Wrap the `FutureBuilder` lists in
      `DiscoveryScreen`, `LibraryScreen`, `PublicGuideScreen`,
      `NotificationsScreen`, `AuthorScreen`, `SystemStatusScreen`,
      `TenantDashboardScreen`, `PluginCatalogScreen`,
      `AssistedImportScreen`, `LicensePanelScreen`,
      `PublicCreatorScreen`, `CreatorEnrollmentScreen` in
      `RefreshIndicator(onRefresh: () async { setState(() {...}); })`.

## Surface coverage
- [x] Add a date picker for verified trip evidence on
      `_VerifiedTripsSectionState` (uses the existing
      `submitEvidence` API).
- [x] Add a placeholder map preview (gray box with "Map preview") on
      `_DayRouteSection` since the backend does not yet return tiles.
- [x] Add an evidence upload picker (file/image picker) on
      `_VerifiedTripsSectionState`.

## Testing
- [x] `flutter analyze` clean.
- [x] `flutter test` passes; existing widget tests for the home
      shapes updated to mount inside the new shell.
- [x] Add a widget test that asserts the `NavigationBar` shows 4
      tabs for an anonymous user and 5 for a creator.
- [x] Add a widget test that asserts the `PopScope` dialog appears
      on the profile screen when there are unsaved changes.
- [x] Add a widget test that asserts a snackbar appears after a
      successful favorite toggle.
