# Home Navigation Specification

## Purpose
Render the Flutter home surface (`SystemScreen`) as the primary navigation
entry, adapting to the visitor's session state, roles, and creator status
without exposing entries the visitor cannot use.

## Requirements

### Requirement: Auth-aware home surface
The home surface SHALL render distinct shapes for anonymous, signed-in user,
signed-in creator, and signed-in administrator visitors, driven by the token
store and the session summary projection.

#### Scenario: Anonymous home
- **WHEN** no access token is stored on the device
- **THEN** the home surface shows public discovery, planning, find-a-creator,
  and self-hosted-status entries; no signed-in, creator, or administrator
  entries appear

#### Scenario: Signed-in non-creator home
- **WHEN** a signed-in visitor without an active creator profile opens the
  home surface
- **THEN** the home surface shows personal library, profile, notifications,
  notification preferences, plugin catalog, tenant, assisted import, and
  self-hosted-status entries plus a "Become a creator" entry; no creator
  dashboard or administrator entry appears

#### Scenario: Signed-in creator home
- **WHEN** a signed-in visitor with an active creator profile opens the
  home surface
- **THEN** the home surface shows creator dashboard, my guides, and license
  policies entries; the "Become a creator" entry does not appear

#### Scenario: Administrator home
- **WHEN** a signed-in visitor whose roles include `Administrator` opens the
  home surface
- **THEN** the home surface additionally shows an administrator operations
  entry alongside the signed-in entries

### Requirement: Reactive session transitions
The home surface SHALL update without restart when the access token appears,
changes, or disappears.

#### Scenario: Sign-in re-renders home
- **WHEN** the sign-in screen writes a new access token
- **THEN** the home surface re-renders in the appropriate signed-in shape
  within the same frame

#### Scenario: Sign-out reverts to anonymous
- **WHEN** the sign-out action clears the stored token
- **THEN** the home surface reverts to the anonymous shape and protected
  entries disappear

### Requirement: Server-authoritative session summary
The home surface SHALL derive role and creator visibility from a server
projection and SHALL NOT decode tokens or assume entitlements locally.

#### Scenario: Stale role claim
- **WHEN** the cached session summary no longer reflects the visitor's server
  state
- **THEN** a refresh of the session summary on focus restores accurate
  visibility before the visitor interacts

### Requirement: In-place sign-out
The home surface SHALL provide a sign-out affordance for signed-in visitors
without requiring navigation to a separate settings page.

#### Scenario: Sign-out from home
- **WHEN** a signed-in visitor invokes the sign-out affordance on the home
  surface
- **THEN** the API logout endpoint is called, the token is cleared, and the
  home surface reverts to the anonymous shape

### Requirement: Email-unverified warning banner
The home surface SHALL display a verification banner above the hero for any
signed-in visitor whose email has not yet been confirmed.

#### Scenario: Unverified banner
- **WHEN** a signed-in visitor with `emailConfirmed == false` opens the
  home surface
- **THEN** a warning banner is rendered with a resend-verification action
  and the banner disappears once the next summary reports the email
  as confirmed

#### Scenario: Verified visitor
- **WHEN** a signed-in visitor with `emailConfirmed == true` opens the
  home surface
- **THEN** no verification banner is rendered
