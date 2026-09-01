# Design: Complete Flutter workspace UX

## Navigation

Keep one visible-destination mapping per role and use named routes/deep links that resolve through a single auth/role gate. Primary navigation SHALL contain only high-frequency destinations; secondary capabilities belong in a profile/workspace menu with visible role labels. Public discovery and guide detail remain accessible anonymously.

The home surface is a single `WorkspaceScreen` rendered by the `/` route. Sections are projected from `MySummary` (`workspaceSectionsFor`) and rendered in this order: Creator workspace (creators) or Get started (non-creators), Discover & plan, Administration (administrators), Tenant (tenants), Account. Tapping a tile pushes the named route through `Navigator.pushNamed`. The bottom navigation retains Home, Discover, Library, and Plan/Create.

The shell's `onNavigate` callback for the `Home` destination now resolves to `/` (the workspace) instead of `/profile`, which keeps the workspace the single authenticated landing surface.

## Layout

Use constrained content widths on web, adaptive navigation on wide screens, scroll-safe forms, `Flexible`/wrapping for long text, and minimum touch targets. Remove the duplicate floating sign-out action; place account/session actions in a consistent menu or AppBar.

`ConstrainedWorkspace` (`Center` + `ConstrainedBox(maxWidth: 720)`) wraps the home, profile, and public guide bodies so a desktop browser does not stretch a single column. Profile rows use `Expanded` + `TextOverflow.ellipsis` for email, role list, and account status. The public guide's title, subtitle, day titles, and country/city chips all use `Wrap` or ellipsis truncation.

The duplicate sign-out floating action button in `SignedInShell` is removed. The home `AppBar` retains the single `Sign out` action with tooltip `Sign out`. The shell's `onSignedOut` callback is also invoked when the session controller reports the anonymous phase, which clears the route stack and routes to `/`.

## Route guard

`authorizedRouteRoles` plus a `_creatorOnlyRoutes` set drive the `_GuardedRoute` and `_GuardedShellRoute` widgets. These guards render `AccessDeniedScreen` for unauthenticated or role-mismatched deep links without exposing any underlying data. The server remains the authority for resource access; the guards only redirect navigation.

`AccessDeniedScreen` is a centred surface with the route name and a `Back to workspace` filled button. The accompanying `AppBar` is titled `Restricted` so the screen reader announces a single heading without duplicating the body copy.

## Guide detail

Show cover, title, author, destinations, duration, freshness, rating/review summary, price/currency, entitlement state, preview boundary, and a clear next action. Purchase failures and unavailable providers must retain context and offer retry.

The public guide screen adds `Wrap` chips for country code and cities, ellipsis truncation on the title, subtitle, day titles, and node names, and a constrained body width. The pricing line, buy / fork / save / favorite actions, and inline `status` text remain inside the same `ListView` so existing error and retry copy is preserved.

## Verification

Test role matrices, deep links, back navigation, empty/error/loading states, 320px-width layouts, keyboard traversal, and wide web layouts with screenshots or browser automation.

- Unit and widget tests in `test/workspace_screen_test.dart` exercise the section projection, the workspace rendering for each role, the access-denied surface, the email-unverified banner, the route guard for a creator-only and an admin-only route, and the absence of a floating sign-out action.
- `test/widget_test.dart` unskips the workspace, sign-out, email-unverified, and administrator home tests so the existing assertions run as part of the suite.
- A 320px-physical-size test asserts the home summary card does not throw and renders the long email after a viewport resize.
- The Flutter analyzer reports zero errors; only pre-existing info-level lints remain.

