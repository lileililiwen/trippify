# flutter-design-tokens Specification

## Purpose
TBD - created by archiving change audit-2026-08-27-design-system-foundation. Update Purpose after archive.
## Requirements
### Requirement: Semantic color tokens
The system SHALL expose a `TrippifyTokens` `ThemeExtension` that maps
purpose-bound names (`creator`, `purchased`, `free`, `warning`, `danger`,
`onSurfaceMuted`) to color values, and SHALL resolve to AA-compliant
contrast on the surface in both light and dark modes.

#### Scenario: Body-text contrast in light mode
- **WHEN** a screen renders `onSurfaceMuted` text on a `surface` background in light mode
- **THEN** the measured contrast ratio is at least 4.5:1

#### Scenario: Body-text contrast in dark mode
- **WHEN** a screen renders `onSurfaceMuted` text on a `surface` background in dark mode
- **THEN** the measured contrast ratio is at least 4.5:1

### Requirement: Type, spacing, and radius tokens
The system SHALL expose component-level tokens for type scale
(`displayLarge`..`labelSmall`), spacing (`xs`..`xxl`), and radius
(`sm`..`xl`) derived from a primitive scale.

#### Scenario: SystemScreen reads tokens
- **WHEN** `_SystemScreenState` builds the four home shapes
- **THEN** the header avatar, header name, summary title, and "isCreator" chip read from `Theme.of(context).extension<TrippifyTokens>()` and `colorScheme` only; no raw `Color(0xFF...)` literal and no raw `TextStyle(fontSize:)` override remains in the file

### Requirement: Light and dark themes
The system SHALL register a `lightTheme` and a `darkTheme` built from a
single seed color and SHALL set `themeMode: ThemeMode.system` so the
client follows the OS preference.

#### Scenario: OS dark-mode preference
- **WHEN** the host operating system is set to dark mode
- **THEN** the Flutter app renders with the dark theme without code changes

### Requirement: Shared state widgets
The system SHALL provide a `LoadingState`, `ErrorState`, and `EmptyState`
widget used by every list/feed surface, with the `ErrorState.message`
sourced from a typed enum so screens cannot drift.

#### Scenario: ErrorState retry
- **WHEN** the underlying `Future` rejects
- **THEN** `ErrorState` renders the message and a retry button that
  re-invokes the future

