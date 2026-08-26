# Structured guide authoring

Active creators can create and edit private structured travel guides through `/api/v1/guides`. The API is included in the generated OpenAPI document.

## Data and access boundaries

- Metadata, ordered days, ordered place/activity nodes, prose sections, and media references are stored separately.
- Every authoring query is scoped to the authenticated owner. Another owner's guide returns `404` to avoid disclosing its existence.
- New guides start as `Draft`. This slice permits `Draft`, `Private`, and `Archived`; publication belongs to later changes.
- Deletes are soft deletes. Audit records retain actor, action, guide identifier, and timestamp.
- Media is limited to 10 MB. Storage outages return `503` without creating a media record.

## Safe writes

Create accepts an optional `Idempotency-Key` header of at most 100 characters. Repeating a key for the same creator returns the original guide. Mutations require the latest `concurrencyToken`; stale writes return `409`. Structure replacement is atomic and stores list order as zero-based positions.

## Operations

The `Trippify.Guides` meter emits `trippify.guide.commands` with a low-cardinality `operation` tag. Alert on elevated `409`, `422`, and `503` rates. Never record prose, titles, coordinates, storage keys, or user identifiers in telemetry.

Apply the forward-only EF Core migration before the matching API version. Verify migration history and unique ordering indexes after deployment.
