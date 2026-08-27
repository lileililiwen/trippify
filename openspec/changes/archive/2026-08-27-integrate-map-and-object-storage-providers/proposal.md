# Why
The current map adapter always returns no result and object storage returns a fabricated local URI without persisting bytes.

# What Changes
Add production map/geocoding and object-storage adapters, validated configuration, durable media metadata, upload controls, and safe provider failure behavior.

# Capabilities
## New Capabilities
- `map-object-storage-providers`: real server-side geocoding and durable private/public object storage.

# Dependencies and Non-goals
- Dependencies: route planning, guide media, managed SaaS quotas, and provider-secret configuration.
- Non-goals: turn-by-turn navigation, video transcoding, or a general-purpose file drive.

# Impact
Changes provider registration, route/media APIs, object metadata, Flutter map/media states, migrations, and deployment documentation.
