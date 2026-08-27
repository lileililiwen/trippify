# Creator follows and notifications

Buyers can opt into creator updates without giving the creator access to their account. Notifications carry provenance through to the in-app feed, and email delivery is gated by per-kind preferences so users stay in control.

## Follows

- `POST /api/v1/creators/{slug}/follow` lets an authenticated user follow an active creator. Returns `201 Created` on first follow and is idempotent on subsequent calls. Self-follow is rejected with `400`.
- `DELETE /api/v1/creators/{slug}/follow` is idempotent and returns `204` after the row is removed.
- `GET /api/v1/creators/{slug}/follow` returns the caller's own follow status (auth required).
- `GET /api/v1/creators/{slug}/followers/count` is a public aggregate that returns `followers` only.

## Notifications

- `GET /api/v1/me/notifications?limit=N` returns the caller's notifications, newest-first, along with `unreadCount`. `limit` is clamped between `1` and `200`.
- `POST /api/v1/me/notifications/{notificationId}/read` is idempotent and restricted to the notification's recipient.
- `GET /api/v1/me/notification-preferences` returns the per-kind preferences for the caller (defaults applied when none exist).
- `PUT /api/v1/me/notification-preferences` overwrites every flag; `401` is impossible (auth required) and missing flags are accepted.

## Preferences

Every preference flag follows two axes: a global `EmailEnabled` and `InAppEnabled`, plus per-kind flags (`NewGuidePublished`, `NewReviewOnMyGuide`, `NewReplyToReview`, `FollowerGained`, `EvidenceReviewed`). When a kind is suppressed in both channels, the fan-out short-circuits and no notification row is created.

## Operations

The `Trippify.Notifications` meter emits `trippify.notifications.commands` with low-cardinality `operation` tags (`creator-followed`, `creator-unfollowed`, `notifications-listed`, `notification-read`, `preferences-updated`). Bodies and titles are not part of the telemetry — only counts and actor ids. Alert on elevated `403` (someone probing follow status) or `409` (duplicate follow due to a race).

## Privacy guarantees

- Followers receive no personal data about the creator beyond what they already see via the public `PublicCreator` projection.
- Notifications are restricted to the recipient; an unrelated caller attempting to mark someone else's notification receives `404` without revealing the notification's existence.
- Preferences are never returned for any other user, and the preferences endpoints fail closed when an unknown kind is referenced.
- Email intent is recorded via the meter; actual delivery remains the responsibility of the email worker (out of scope for this slice) which reads the same `notifications` rows.
