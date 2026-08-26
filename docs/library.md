# Personal library and forks

Users curate favorites, manage personal trip plans, and fork published guides into independent private copies.

## Favorites

- `POST /api/v1/library/favorites/{guideId}` marks a published guide (free or paid) as a favorite; duplicates are rejected with `409`.
- `DELETE /api/v1/library/favorites/{guideId}` removes the favorite.
- `GET /api/v1/library/favorites` lists favorites with guide metadata and pricing, ordered by most recently added.

## My Trips

- `POST /api/v1/library/trips { guideId, title? }` creates a personal trip plan. The guide must be owned, freely published, or accessible through an active entitlement; unlisted and paid guides without entitlement are rejected with `404`.
- `GET /api/v1/library/trips` lists the user's trips ordered by most recently updated.
- `PATCH /api/v1/library/trips/{tripId}` updates title, notes (up to 4000 characters), or status (`Planning`, `Active`, `Completed`, `Archived`).
- `DELETE /api/v1/library/trips/{tripId}` removes the trip; all operations are scoped to the authenticated user.

## Non-commercial forks with provenance

- `POST /api/v1/library/forks { guideId }` creates an independent, private, editable `Draft` copy of an allowed guide. Access mirrors the trip rules: owned, freely published, or entitled paid guides are forkable.
- Forks copy metadata, ordered days, nodes, and sections. New unique slugs are generated from the source title. The source guide receives an audited `forked` entry with the forker's identity.
- One fork per source per user is enforced; repeated requests return `409`. Forked guides carry `sourceGuideId` and `forkedAt` so provenance remains visible and the source receives creator credit.

## Operations

The `Trippify.Library` meter emits `trippify.library.commands` with low-cardinality `operation` tags (`favorite-added`, `favorite-removed`, `trip-created`, `trip-updated`, `trip-deleted`, `fork-created`). Alert on elevated 409 (duplicate favorites or duplicate forks). Never record titles, notes, or identifiers in telemetry payloads beyond aggregate operation counts.

Apply the forward-only EF Core migration before the matching API version. Verify foreign-key cascades from favorites and trips survive guide soft-deletion (SetNull on trip references), and that the unique index `(UserId, GuideId)` on favorites holds under concurrent requests.
