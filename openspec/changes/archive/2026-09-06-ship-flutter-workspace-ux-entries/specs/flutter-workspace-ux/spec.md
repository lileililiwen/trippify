# Flutter workspace UX

## MODIFIED Requirements

### Requirement: Authorized workflows SHALL be discoverable

Each authenticated role MUST have a predictable entry point to its authorized workflows, while unauthorized capabilities MUST be hidden or clearly denied without relying on client authorization. The following entries MUST be present and reachable from the appropriate home or shell surface: a "Discover guides" home entry with the discovery → guide → author chain; planning empty-state and denied-state copy with party-size totals from the home surface; notifications and notification-preferences home entries; plugin-catalog, tenant-dashboard, assisted-import, license-policies, and self-hosted-status home entries; public-creator-search and release-history home entries; a profile entry from the signed-in shell; and guide-workspace drag-to-reorder. Each entry MUST be covered by a non-skipped test.

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

#### Scenario: Deferred workflow entries are shipped and tested

- **Given** the workspace navigation for each authorized role
- **When** the build and widget tests run
- **Then** every listed entry is reachable from its home or shell surface
- **And** no widget test for those entries is permanently skipped or cites an already-archived change

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
