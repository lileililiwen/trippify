# Flutter workspace UX

## ADDED Requirements

### Requirement: Authorized workflows SHALL be discoverable

Each authenticated role MUST have a predictable entry point to its authorized workflows, while unauthorized capabilities MUST be hidden or clearly denied without relying on client authorization.

#### Scenario: Creator workspace

- **Given** an authenticated creator
- **When** the creator opens the workspace menu
- **Then** guide authoring, planning, releases, sales, reviews, and notifications have discoverable entries
- **And** server authorization remains authoritative

#### Scenario: Traveler cannot access admin tools

- **Given** an authenticated traveler
- **When** the traveler follows an admin deep link
- **Then** the app shows an access-denied state with a safe return action
- **And** no admin data is rendered

### Requirement: Layout SHALL remain usable across widths

Core screens MUST avoid horizontal overflow and preserve readable controls at supported mobile and web widths.

#### Scenario: Long account values on narrow screen

- **Given** a narrow viewport and a long email, role list, or guide title
- **When** the account or guide screen renders
- **Then** text wraps or truncates accessibly
- **And** primary actions remain reachable without horizontal scrolling

### Requirement: Sign-out SHALL have one clear location

The authenticated shell MUST expose one consistent sign-out action with confirmation or clear immediate feedback and MUST return to the anonymous home after completion.

#### Scenario: Sign out

- **Given** an authenticated user
- **When** the user selects sign out
- **Then** local credentials are cleared
- **And** the app returns to anonymous home with status feedback
