# Navigation and Feedback

## Why
The 2026-08-27 UX audit (`docs/ux-audit-2026-08-27.md`, F8–F11, F14)
flagged that the app has 22 named routes with no `BottomNavigationBar`,
`NavigationBar`, `Drawer`, or `NavigationRail`, and that the only
navigational feedback the user gets is a status string rendered in
place. There is no `SnackBar` or `AlertDialog` anywhere in the
codebase, and 12 of 22 screens lack pull-to-refresh. The `home-
navigation` spec talks about a "home surface" that adapts to auth
state, but the home only exposes three action buttons; the rest of the
app is reachable only by deep link.

## What Changes
- Add a signed-in `NavigationBar` shell with 4–5 destinations
  (discover, my library, planning, profile, plus an admin/creator tab
  shown only when relevant).
- Add `PopScope(canPop: false, onPopInvoked: ...)` on every form
  screen that has unsaved changes, prompting with `AlertDialog`.
- Add `ScaffoldMessenger.showSnackBar` for every transient success
  (favorite added, fork created, payment started) and every transient
  error (network blip, payment declined).
- Wrap every list/feed surface in `RefreshIndicator(onRefresh: ...)`
  so the user can pull-to-refresh regardless of screen.
- Add surface-level widgets for surfaces the backend supports but
  the client hides (date picker for evidence, map preview, evidence
  upload picker).

## Capabilities
### New Capabilities
- `flutter-navigation-shell`: a `NavigationBar`-based signed-in shell
  with 4–5 destinations and auth/role-aware visibility.
- `flutter-feedback-patterns`: snackbars, dialogs, and
  `RefreshIndicator` used uniformly across every screen.

### Modified Capabilities
- `home-navigation`: the four home shapes no longer carry the three
  action buttons; the navigation shell takes their place and the home
  becomes a summary + quick actions only.

# Dependencies and Non-goals
- Dependencies: `home-navigation`, `flutter-design-tokens`,
  `flutter-accessibility`.
- Non-goals: full Material 3 adaptive shell for tablets and desktop
  (deferred); per-screen skeletons (use a centered
  `CircularProgressIndicator` until `flutter-design-tokens` adds a
  `LoadingState` skeleton variant).

# Impact
No backend changes. Pure Flutter refactor. Existing widget tests must
be updated to mount screens inside the new shell, but no test should
have to assert on the shell itself.
