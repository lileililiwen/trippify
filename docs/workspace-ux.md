# Flutter workspace UX

The Flutter app presents a single home surface for every authenticated
role. The surface is implemented by `WorkspaceScreen` in
`apps/trippify_flutter/lib/shell/workspace_screen.dart` and is
rendered by the `/` route for both anonymous and authenticated
visitors.

## Sections

`workspaceSectionsFor(MySummary)` projects the session summary into a
list of `WorkspaceSection`s. The renderer iterates the sections in
order and never re-queries the server. Each section is a labelled
`Card` containing `WorkspaceEntry` tiles. The tiles use
`TextOverflow.ellipsis` and the parent is wrapped in
`ConstrainedWorkspace` so long titles, emails, role lists, and chip
text wrap instead of overflowing on a 320px viewport.

The current sections, in the order they are rendered, are:

1. `Creator workspace` for users with `isCreator: true`, otherwise a
   single `Get started` section whose only entry is the
   `Become a creator` enrollment CTA at `/creator/enroll`.
2. `Discover & plan` — `Discover guides` at `/discover` and
   `My library` at `/library`. Always present.
3. `Administration` — `Admin operations` at `/admin/operations`.
   Rendered only when the session summary contains the
   `Administrator` role.
4. `Tenant` — `My tenant` at `/tenant` and `Assisted import` at
   `/assisted-import`. Rendered when the session summary contains the
   `Tenant` role.
5. `Account` — `My profile`, `Notifications`, and
   `Notification preferences`. Always present.

Server authorization remains the source of truth for every tile. The
`/admin/operations`, `/tenant`, `/assisted-import`, `/plugins`,
`/creator/dashboard`, `/guides`, and `/license-panel` routes are
wrapped in `_GuardedRoute` and `_GuardedShellRoute` route guards. A
traveler who follows an admin deep link sees `AccessDeniedScreen`
with a "Back to workspace" action; no protected data is rendered.

## Layout

- `ConstrainedWorkspace` centres and caps the home, profile, and
  public-guide content at `kWorkspaceMaxContentWidth = 720` so a
  desktop browser does not stretch a single column to the full
  viewport.
- Profile rows use `Expanded(child: Text(..., overflow:
  TextOverflow.ellipsis, maxLines: 2))` so long emails, role lists,
  and account status wrap or truncate accessibly.
- The public guide's title, subtitle, day titles, and country/city
  chips all use `Wrap` or `TextOverflow.ellipsis` so a long title
  does not push the buy / fork / favorite actions off the visible
  area on a narrow phone.

## Sign-out

There is one sign-out control. It lives in the home `AppBar` of the
`SystemScreen` and uses the tooltip `Sign out`. The
`SignedInShell` no longer renders a floating sign-out button — the
spec calls for a single, consistent location and the AppBar action
keeps the bottom navigation free for the primary destinations. Tapping
the control calls `SessionController.signOut` and replaces the route
stack with the anonymous home.

## Unverified email banner

When `MySummary.emailConfirmed` is `false`, the workspace renders an
unverified-email banner above the sections. The banner exposes a
`Resend` `TextButton` that calls `AppApi.resendVerification`. The
button shows a `Sending…` progress state while the request is
inflight and re-enables itself after the response settles.
