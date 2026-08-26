# Guide publishing and discovery

Creators publish validated guides to a public catalog; visitors search it through `/api/v1/discovery` without authentication. The API is included in the generated OpenAPI document.

## Publication

- `POST /api/v1/guides/{id}/publish` validates readiness: non-empty summary, at least one day, and at least one place per day. Omitting pricing publishes as `FreePublic`; including `{ priceMinorUnits, currencyCode }` publishes as `Paid`.
- Prices are positive minor units capped at 1000000000 with a three-letter currency. Publishing records `PublishedAt`, an audit entry (`published:FreePublic` / `published:Paid`), and requires the latest concurrency token.
- Every guide receives a stable SEO slug (`/guides/{slug}`) generated from its title at creation; slugs never change and are unique among live guides.
- `POST /api/v1/guides/{id}/unpublish` returns a published guide to `Private` and removes it from discovery.

## Discovery boundaries

- Search supports country, city, tag, free/paid facets, free-text query, and paging. Only `FreePublic` and `Paid` guides appear; drafts, private, unlisted, archived, and soft-deleted guides never leak.
- Paid guide URLs return only configured preview fields: metadata plus day titles and place names/types. Coordinates, addresses, times, notes, ticket/reservation details, and sections are withheld until entitlement exists in a later change. Free guides return full outlines except ticket/reservation/opening-hour fields.
- Responses omit null fields globally so protected content is absent rather than blank.
- Author pages expose active creator profiles with their published guides only.

## Operations

The `Trippify.Discovery` meter emits `trippify.discovery.queries` with low-cardinality `operation` tags (`search`, `detail`, `author`). Alert on elevated 404/422 rates. Never record search terms, titles, or user identifiers in telemetry.

Apply the forward-only EF Core migration before the matching API version; it adds nullable publication columns and a partial unique index on `Slug`. Verify the index after deployment.
