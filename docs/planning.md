# Route, map, and budget planning
Creators plan each guide day as an ordered route with transport segments and track party-size-aware budgets through `/api/v1/guides/{guideId}`. The API is included in the generated OpenAPI document.

## Data and access boundaries
- `GET /days/{dayPosition}/route` returns the day's ordered markers (nodes with positions, coordinates, geocode status, and provider attribution) and ordered transport segments. `PUT /days/{dayPosition}/route` atomically replaces the segments.
- `GET /budget?partySize=N` returns categorized per-person amounts plus server-computed party totals; `PUT /budget` replaces entries. Party size is validated between 1 and 20.
- Every planning query is scoped to the authenticated owner. Another owner's guide returns `404`; anonymous requests return `401`. Clients never decide authorization.
- Transport segments are limited to 40 per day with durations of 0–1440 minutes; budget entries are limited to 12 unique categories. Amounts are non-negative minor units capped at 1000000000 per person.
- Currencies are normalized to uppercase three-letter codes. Mixed currencies are preserved per line; totals scale per line.
- Writes require the latest guide `concurrencyToken`; stale writes return `409`. Route and budget replacements are audited (`route-replaced`, `budget-replaced`) and bump `UpdatedAt`.

## Server-side geocoding
`PUT /api/v1/guides/{guideId}/structure` resolves eligible route locations server-side via the configured `Map` provider (`IMapProvider`). Nodes that arrive with `latitude`/`longitude` skip geocoding and are stored with `geocodeStatus = "Manual"`. Nodes with only an `address` (or a `name` when address is blank) call the provider, then persist the normalized coordinates plus attribution:

- **Resolved**: `geocodeStatus = "Resolved"`, `latitude`, `longitude`, `geocodeProviderName`, `geocodeProviderAttribution`, `geocodeProviderPlaceId`, and the original query (`resolvedQuery`) are stored.
- **Unresolved**: `geocodeStatus = "Unresolved"`, no fabricated coordinates are written, attribution still records the provider's name (and configurable attribution string), and `resolvedQuery` is preserved so the operator can re-resolve later.
- **Outage / timeout**: the `ReplaceStructure` endpoint catches provider exceptions, records an `Unresolved` row, and returns `200` so the creator can keep authoring. No successful coordinates are fabricated.

Geocode results are cached in `geocode_cache` keyed by SHA-256 of the normalized query for `Map:CacheRetentionHours` (default 7 days). The cache survives provider outages so re-attempts inside the retention window do not re-charge quota or reveal fresh secrets.

## Operations
The `Trippify.Planning` meter emits `trippify.planning.commands` with a low-cardinality `operation` tag. Alert on elevated `409` and `422` rates. Never record place names, coordinates, amounts, or user identifiers in telemetry.

Apply the forward-only EF Core migration before the matching API version. Verify migration history and the unique `(DayId, Position)` and `(GuideId, Category)` indexes after deployment.
