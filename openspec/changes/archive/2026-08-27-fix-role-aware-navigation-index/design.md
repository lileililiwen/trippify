# Context
The enum contains both `plan` and `create`, while each role sees only one. Raw enum indices therefore do not match visible navigation indices.

# Goals / Non-goals
- Make selection and tap mapping valid for every role and first-frame state.
- Preserve the existing four-destination role-specific design.

# Decisions
- Build a list of `(SignedInDestination, NavigationDestination)` entries and use it for both `selectedIndex` and tap callbacks.
- While an authenticated summary is unresolved, render the page body without the role-dependent bar.
- If a resolved role reaches a route it cannot represent, select Home rather than passing an invalid index; backend authorization remains authoritative.

# Authorization, privacy, and failure modes
- Navigation visibility remains a presentation concern and does not replace API authorization.
- Summary failure cannot produce an out-of-range index or expose a role-only destination.

# Migration and rollback
- No persistence or API migration.
