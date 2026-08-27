# Context
`SystemScreen` is the only navigation entry in the current Flutter shell and
must remain the single source of truth for the visitor's primary actions.
The backend already exposes `/api/v1/me/profile` and role checks elsewhere,
but no single endpoint returns the fields the home tile renderer needs.

# Goals / Non-goals
- Goal: one round trip exposes `{email, displayName, avatarUrl, roles, isCreator, accountStatus, emailConfirmed}` for the bearer token.
- Goal: home tiles disappear when the visitor cannot use them, even before the server is contacted.
- Goal: sign-in / sign-out updates the home surface without app restart.
- Non-goal: redesigning every other screen; only `SystemScreen` changes.
- Non-goal: client-side JWT decoding; rely on the server projection.

# Decisions
- **Single session-summary endpoint.** A new `GET /api/v1/me/summary` returns
  roles and creator status derived from the principal and database so the
  client does not need to chain `me/profile` + role introspection + creator
  lookup.
- **Reactive token store.** `TokenStore` exposes a `ValueListenable<String?>`
  and `ApiClient` proxies it as `ValueListenable<String?> tokens`. Login,
  logout, and register screens mutate the token; the home surface listens and
  rebuilds.
- **Server remains authoritative.** Tiles for creator/admin areas stay
  hidden when the projection says the visitor lacks the role, but the server
  continues to enforce `[Authorize]` and role policies.
- **Failure handling.** When the summary call fails with 401, the token is
  cleared and the home falls back to the anonymous shape instead of leaving
  a half-loaded signed-in view.
- **Anonymous default.** First paint of the home screen uses the anonymous
  shape; the signed-in shape appears once the token + summary are available,
  so anonymous visitors never see protected tiles flash before redirect.

# Risks / Trade-offs
- Adding a public summary endpoint slightly widens the authenticated surface;
  the response is limited to non-sensitive projection fields and no
  credentials, so the blast radius matches `/me/profile`.
- Reactive rebuilds fire on every token write; the home screen uses lightweight
  `ValueListenableBuilder` so the cost is one rebuild per session event.
