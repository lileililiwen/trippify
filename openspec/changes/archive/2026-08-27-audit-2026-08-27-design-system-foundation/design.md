# Design Decisions

## Token architecture

A 3-tier DTCG-style token tree:

1. **Primitive tokens** — raw values, one per concept. Example:
   `color.purple.500 = #6750A4`, `space.4 = 16`.
2. **Semantic tokens** — purpose-bound names that reference primitives.
   Example: `color.brand.primary = color.purple.500`,
   `space.component.padding = space.4`.
3. **Component tokens** — surface tokens to widgets. Example:
   `card.summary.padding = space.6`.

Components only read component (or semantic) tokens. The audit
identified that the current code reads raw `Color(0xFF...)` literals and
`EdgeInsets.all(24)` magic numbers, which is the bug we're fixing.

## Why a `ThemeExtension` for non-M3 tokens

`ColorScheme` covers primary/secondary/tertiary/error and their
containers. It does not cover "creator status", "purchased", "free", or
"warning", which the home summary card needs. A `ThemeExtension` is the
Material 3 idiomatic way to add semantic colors that survive a dark
theme switch.

## Why no new font

The current visual identity is "Material 3 default Roboto". Switching
fonts is a brand decision, not a UX-fix decision, and the audit does
not flag typography. Defer to a future "apply aesthetic" pass.

## Why `LoadingState` / `ErrorState` / `EmptyState` as widgets

12 of 22 screens repeat the same `FutureBuilder` triplet of "spinner",
"X is unavailable", and "no items". Three named widgets:

- `LoadingState()` — centered `CircularProgressIndicator` with the same
  padding.
- `ErrorState(message: ..., onRetry: ...)` — message + retry button. The
  message comes from a typed enum so screens can't drift.
- `EmptyState(message: ..., action: ...)` — message + optional CTA.

This is the smallest change that removes the inconsistency surfaced by
F19 and F23.

## Rollback

Tokens are additive. To roll back, point `_appTheme` back at the inline
`ThemeData` and delete the `lib/design/` package. No data migration.
