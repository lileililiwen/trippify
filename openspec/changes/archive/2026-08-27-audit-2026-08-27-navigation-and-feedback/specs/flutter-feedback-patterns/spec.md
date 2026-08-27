# Flutter Feedback Patterns Specification

## Purpose
Standardize transient success, transient error, and pull-to-refresh
feedback across every Flutter screen so the user always knows the
result of an action and can recover from a stale view.

## ADDED Requirements

### Requirement: Snackbar for transient results
The system SHALL surface every transient success (favorite added,
fork created, trip saved, payment started) and every transient
error (network blip, payment declined) via
`ScaffoldMessenger.showSnackBar` rather than as a status string in
the body.

#### Scenario: Favorite added
- **WHEN** the user taps the favorite button on a public guide
- **THEN** a snackbar appears at the bottom of the screen with the
  text "Added to favorites" and auto-dismisses after a few seconds

#### Scenario: Fork failed
- **WHEN** the fork API rejects
- **THEN** a snackbar appears with the typed error message and an
  optional "Retry" action; the screen state does not silently revert

### Requirement: Pull-to-refresh on every list surface
The system SHALL wrap every list or feed surface in
`RefreshIndicator(onRefresh: ...)` so the user can refresh without
navigating away.

#### Scenario: Library pull-to-refresh
- **WHEN** the user pulls down on the library list
- **THEN** the list re-fetches its `Future` and the new items appear
  in place without navigating away

### Requirement: Visible error taxonomy
The system SHALL distinguish between network failure, server error,
and "not found" via the typed `ErrorState` widget defined in
`flutter-design-tokens`.

#### Scenario: Guide not found
- **WHEN** the public guide API returns 404
- **THEN** the screen shows the `ErrorState` with the message "This
  guide is no longer available" and a "Browse other guides" CTA

#### Scenario: Server error
- **WHEN** the public guide API returns 500
- **THEN** the screen shows the `ErrorState` with the message
  "Trippify is temporarily unavailable" and a "Retry" button
