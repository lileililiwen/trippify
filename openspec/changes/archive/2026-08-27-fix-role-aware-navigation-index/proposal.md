# Why
Opening `/guides` can crash before creator status loads because the shell passes enum index `4` to a four-item navigation bar. Creator navigation also maps the visible Create tab at index `3` to the Plan enum value.

# What Changes
Derive selection and tap behavior from one role-aware destination list, and avoid rendering role-dependent navigation until the session summary is resolved.

# Capabilities
## Modified Capabilities
- `flutter-navigation-shell`: keep role-aware navigation indices valid during loading and interaction.

# Dependencies and Non-goals
- Dependencies: `flutter-navigation-shell` and authenticated user summary.
- Non-goals: redesigning navigation, changing route names, or adding new destinations.

# Impact
Changes the Flutter signed-in shell and widget regression tests only.
