# flutter-navigation-shell Specification

## Purpose
TBD - created by archiving change audit-2026-08-27-navigation-and-feedback. Update Purpose after archive.
## Requirements
### Requirement: Signed-in shell
The system SHALL mount a `Scaffold` with a `NavigationBar` of 4–5
destinations for every signed-in route, hiding destinations the
visitor cannot use.

#### Scenario: Non-creator home shell
- **WHEN** a signed-in non-creator opens the app
- **THEN** the `NavigationBar` shows Home, Discover, My library, and
  Plan, and no Create or Admin tab appears

#### Scenario: Creator home shell
- **WHEN** a signed-in creator opens the app
- **THEN** the `NavigationBar` shows Home, Discover, My library, and
  Create, and no Admin tab appears

#### Scenario: Anonymous visitor
- **WHEN** no access token is stored on the device
- **THEN** no `NavigationBar` is shown and named-route navigation
  continues to work as today

### Requirement: Unsaved-changes guard
The system SHALL intercept the system back gesture and the AppBar
back button on form-bearing screens with unsaved changes and SHALL
prompt the user before discarding.

#### Scenario: Profile screen dirty form
- **WHEN** the user edits the display name and tries to leave the
  profile screen
- **THEN** an `AlertDialog` appears with "Discard changes" and
  "Keep editing"; tapping "Discard changes" pops the route, tapping
  "Keep editing" leaves the route and the form intact

