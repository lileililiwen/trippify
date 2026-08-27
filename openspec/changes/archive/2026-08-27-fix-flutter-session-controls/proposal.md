# Why
The authenticated home screen's sign-out button attempts a dummy login instead of invoking logout, so users may remain signed in and receive an erroneous authentication failure.

# What Changes
Use the session API consistently, clear local credentials on every logout outcome, reset authenticated state, and test navigation/session behavior.

# Capabilities
## New Capabilities
- `flutter-session-controls`: reliable, accessible sign-out and expired-session handling across Flutter shells.

# Dependencies and Non-goals
- Dependencies: user identity API and Flutter navigation shell.
- Non-goals: changing token formats, adding social login, or redesigning authentication screens.

# Impact
Changes Flutter session coordination, home/shell actions, secure token cleanup, tests, and small UX documentation.
