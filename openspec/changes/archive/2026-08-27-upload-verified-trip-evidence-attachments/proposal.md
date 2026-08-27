# Why
The Flutter evidence workflow advertises attachment selection, but it only shows a coming-soon message and the API accepts no uploaded evidence.

# What Changes
Add privacy-scoped evidence attachments, mobile/web file selection, validation, malware-scan state, retention deletion, and reviewer access.

# Capabilities
## New Capabilities
- `verified-evidence-attachments`: secure image/document evidence linked to verified-trip submissions.

# Dependencies and Non-goals
- Dependencies: verified-trip evidence, durable jobs, and production object storage.
- Non-goals: public attachment display, identity-document verification, or permanent archival.

# Impact
Changes evidence persistence/API, object storage lifecycle, reviewer Flutter surfaces, migrations, privacy documentation, and tests.
