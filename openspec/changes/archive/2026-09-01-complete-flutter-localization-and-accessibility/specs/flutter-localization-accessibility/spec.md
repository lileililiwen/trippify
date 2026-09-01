# Flutter localization and accessibility

## ADDED Requirements

### Requirement: Supported locales SHALL have complete UI copy

All user-facing Flutter copy MUST come from generated localization resources for English and Simplified Chinese, including errors, labels, tooltips, status, empty, and provider states.

#### Scenario: Chinese locale

- **Given** the app runs with `zh`
- **When** a user opens sign-in, discovery, guide detail, library, and settings
- **Then** visible copy is Chinese or an explicitly documented fallback
- **And** dates, currencies, and counts use locale-aware formatting

### Requirement: Core workflows SHALL meet accessibility gates

Core workflows MUST provide keyboard navigation, visible focus, semantic name/role/state, sufficient measured contrast, non-color status cues, and accessible field errors.

#### Scenario: Invalid registration

- **Given** a user submits invalid registration data
- **When** validation runs
- **Then** each invalid field has an associated accessible error
- **And** focus moves predictably to the first invalid field

#### Scenario: Async failure

- **Given** a discovery or save request fails
- **When** the error state renders
- **Then** assistive technology receives the failure announcement
- **And** a labeled retry action is keyboard reachable

### Requirement: Reduced motion SHALL be respected

Animations and transitions MUST provide reduced-motion behavior without hiding state changes or feedback.

#### Scenario: Reduced motion preference

- **Given** the platform requests reduced motion
- **When** a screen transitions or displays progress
- **Then** non-essential animation is removed or shortened
- **And** loading/completion state remains understandable
