# Guide versioning and freshness

Buyers need a record of what changed between versions and a freshness signal so they know whether a guide's information is still trustworthy. This slice introduces immutable releases, optional changelogs, public freshness indicators, and a fan-out to entitled buyers whenever a new release appears.

## Releases

- `POST /api/v1/guides/{guideId}/releases { changelog }` lets the guide's creator record a new release. The endpoint records an immutable `GuideRelease` row holding `versionNumber`, `changelog`, `title`, `publishedAt`, `publisherUserId`, and a `nodeSummary` snapshot.
- `GET /api/v1/guides/{guideId}/releases?limit=N` returns releases newest-first. `limit` is clamped between `1` and `100`.
- `GET /api/v1/releases/{releaseId}` returns a single release by id.
- Releases are idempotent: if the recomputed `nodeSummary` matches the latest release, no new row is written — only existing metadata is returned.
- An initial release is auto-created when a guide is published (`POST /api/v1/guides/{guideId}/publish`).

## Freshness

- `GET /api/v1/guides/{guideId}/freshness` returns the latest release's version and `daysSinceLatest`. When no release exists, `latestVersion` is `0` and `daysSinceLatest` is `null`.
- The endpoint is public and does not require authentication. It piggybacks on the existing rate limiter.

## Buyer notifications

`POST /api/v1/guides/{guideId}/releases` (and the initial publish path) trigger `NotificationFanOut.QueueGuideUpdatedAsync`, which fans out a `NewGuidePublished` notification to every buyer with an active `PurchaseEntitlement` and the per-kind preferences that allow it. Each notification carries the guide slug so the Flutter in-app feed stays linked to the changelog.

## Operations

The `Trippify.Versioning` meter emits `trippify.versioning.commands` with low-cardinality `operation` tags (`release-published`, `releases-listed`, `release-read`). Bodies and titles are not part of the telemetry — only counts and ids. Alert on elevated `403` (someone probing release listings) or `404` (deleted guide being polled).

## Privacy guarantees

- Release endpoints never expose creator private profile fields. The `nodeSummary` describes ordered places but never embeds addresses or notes that the public guide does not already surface.
- Buyer notifications are routed through `NotificationFanOut`, so per-kind preferences remain the only gate for both in-app and email delivery.
- Bulk creation of releases is bounded by the existing rate limiter; the 409 path (`Only published guides can receive releases.`) protects the immutability invariant.
