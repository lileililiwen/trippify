# Tasks

- [x] 1. Backend: add `GET /api/v1/me/summary` returning
      `{email, displayName, avatarUrl, roles, isCreator, accountStatus, emailConfirmed}`
      with `[Authorize]`; document in OpenAPI; add xUnit cases for anonymous,
      user, creator, and administrator responses.
- [x] 2. Flutter: extend `TokenStore` and `SecureTokenStore` /
      `MemoryTokenStore` with a `ValueListenable<String?> tokens`; expose
      `ApiClient.tokens` and `ApiClient.isLoggedIn`; ensure existing tests still
      cover token read/write semantics.
- [x] 3. Flutter: add `ApiClient.getMySummary()`; wire sign-in, register, and
      sign-out screens to mutate `tokens`; cover with widget/unit tests.
- [x] 4. Flutter: refactor `SystemScreen` into anonymous, signed-in user,
      signed-in creator, and signed-in administrator shapes; hide tiles the
      visitor cannot use; add in-place sign-out affordance and an
      email-unverified warning banner for signed-in visitors whose
      `emailConfirmed` is false.
- [x] 5. Flutter widget tests: anonymous home hides all protected tiles;
      signed-in non-creator sees "Become a creator" and not "Creator dashboard";
      signed-in creator sees "Creator dashboard" and not "Become a creator";
      administrator sees "Admin operations"; sign-out reverts to anonymous;
      email-unverified banner appears only when `emailConfirmed` is false.
- [x] 6. Docs: update `docs/identity.md` (session-summary endpoint) and
      `README.md` quickstart; run `dotnet test`, `flutter analyze`, and
      `flutter test`.

