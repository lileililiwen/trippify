# Design System Foundation

## Why
The Flutter client has a single `ThemeData` defined inline at the top of
`main.dart` with a hardcoded seed color, no dark theme, and no shared
text-style, spacing, or radius tokens. `SystemScreen` carries 14 raw
`TextStyle(fontSize:)` overrides and 5 hardcoded `Color(0xFF6750A4)`
literals, which means every visual tweak today is a hunt-and-peck. The
2026-08-27 UX audit (`docs/ux-audit-2026-08-27.md`, F2, F17, F18, F24)
flagged this as the root cause of multiple downstream issues.

## What Changes
- Extract a `lib/design/` package containing semantic color, type, spacing,
  radius, and motion tokens.
- Define `lightTheme` and `darkTheme` from a single seed and a single
  `ColorScheme`.
- Replace the inline `_appTheme` with a top-level constant that references
  the new tokens.
- Provide a `Theme.of(context).extension<TrippifyTokens>()` for custom
  semantic colors that the M3 `ColorScheme` does not cover (e.g. creator,
  purchased, free, warning).
- Rewrite `SystemScreen` to read tokens, eliminating the 14 raw
  `TextStyle` overrides and the 5 hardcoded hex literals.
- Add a `LoadingState`, `ErrorState`, and `EmptyState` widget used by
  every `FutureBuilder` list.

## Capabilities
### New Capabilities
- `flutter-design-tokens`: semantic color, type, spacing, radius, motion
  tokens and a dark theme.

### Modified Capabilities
- `home-navigation`: header and summary copy in the four home shapes
  reads tokens instead of hardcoded styles.

# Dependencies and Non-goals
- Dependencies: `home-navigation` (must already be merged).
- Non-goals: changing the M3 seed color; introducing a new font; refactoring
  the entire screen set (per-screen token adoption happens incrementally
  in the accessibility and navigation changes).

# Impact
All current screens continue to render with the same visual identity, but
every future change to the seed, the type scale, or the spacing scale
becomes a one-line edit at the token level. The dark theme is a free
byproduct of the same `ColorScheme.fromSeed` call.
