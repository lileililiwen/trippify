# Design Decisions

## Why a `NavigationBar` and not a `Drawer`

`NavigationBar` (Material 3) is the idiomatic bottom shell for
phone-first apps and meets the user's thumb. A `Drawer` requires an
extra gesture, hides destinations behind a menu, and gets in the way
of one-handed use. The app is phone-first; tablets can grow a
`NavigationRail` later without a spec change.

## Why 4–5 destinations

Per Material guidelines, the `NavigationBar` cap is 5 destinations
with labels. The chosen set:

1. Home (always)
2. Discover (always)
3. My library (always)
4. Plan (signed-in only)
5. Create (creator only) — replaces "My guides" for creators

Admin operators get a separate `/admin/operations` route, reachable
from the Profile screen. This keeps the bar at 4 for non-creators and
5 for creators without an "Admin" tab cluttering the everyday UI.

## Why `PopScope` on form screens

`WillPopScope` is deprecated. `PopScope(canPop: false, onPopInvoked)`
plus an `AlertDialog` is the M3 idiom. It fires before the system
back gesture and the AppBar back button, covering both paths.

## Why a `ScaffoldMessenger.showSnackBar`

A status string in the body is invisible once the user scrolls.
`SnackBar` lives at the bottom of the screen, animates in/out, and
auto-dismisses. The audit (F10) flagged that 0 of 22 screens use it.

## Why `RefreshIndicator` everywhere

The platform gesture is pull-to-refresh; a user who expects it on
every list and doesn't find it concludes the data is stale. Adding it
to 12 screens is mechanical and improves perceived freshness without
any backend change.

## Rollback

The shell is opt-in per route. To roll back, register the existing
named routes in `MaterialApp.routes` without the shell wrapper. The
`PopScope` and `RefreshIndicator` wrappers are local widgets and can
be removed per screen.
