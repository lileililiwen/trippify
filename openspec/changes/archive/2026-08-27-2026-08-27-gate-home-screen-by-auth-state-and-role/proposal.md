# Why
The Flutter home screen currently shows every navigation tile to every visitor.
Server-side authorization still blocks the protected endpoints, but the UI
should not advertise actions the visitor cannot complete, and it should react
in-place when the session token appears or disappears.

# What Changes
- Add a session-summary endpoint that returns the authenticated user's display
  name, roles, creator status, and account status in one round trip.
- Make the Flutter token store reactive so any sign-in, sign-out, or token
  refresh re-renders the home screen without a full reload.
- Render the home screen in three shapes — anonymous, signed-in user, signed-in
  creator, signed-in administrator — driven by session state and role claims.
- Hide navigation tiles that the visitor cannot use, and add an in-place
  sign-out affordance for signed-in visitors.

# Capabilities
## New Capabilities
- `home-navigation`: client-side home surface that adapts to auth state and roles.

## Modified Capabilities
- `user-creator-identity`: add session-summary projection used by the home
  surface to render roles and creator status.

# Dependencies and Non-goals
- Dependencies: `add-user-and-creator-identity`, `establish-platform-foundation`.
- Non-goals: profile editing UX changes, notification preferences surface,
  self-hosted status page (already exists), role management UI for admins,
  any new server authorization rules beyond exposing the projection.

# Impact
Adds one authenticated API endpoint, one Dart listenable, refactors
`SystemScreen` to listen to the token store, and adds Flutter widget tests
covering the four home-screen shapes.
