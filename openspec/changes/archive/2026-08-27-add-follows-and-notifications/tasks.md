# Tasks

## 1. Domain model and persistence

- [x] 1.1 Add `CreatorFollow`, `NotificationPreference`, and `Notification` entities + EF configurations in `src/Trippify.Infrastructure/Notifications.cs`.
- [x] 1.2 Register the new `DbSet<>` properties on `AppDbContext` in `src/Trippify.Infrastructure/Persistence.cs`.
- [x] 1.3 Add forward-only EF Core migration `AddCreatorFollowsAndNotifications` with unique indexes and indexes for unread lookup.

## 2. ASP.NET Core API surface

- [x] 2.1 Create `src/Trippify.Api/NotificationsEndpoints.cs` mapping:
  - `POST /api/v1/creators/{slug}/follow`
  - `DELETE /api/v1/creators/{slug}/follow`
  - `GET /api/v1/creators/{slug}/follow` (own follow status)
  - `GET /api/v1/creators/{slug}/followers/count` (public)
  - `GET /api/v1/me/notifications`
  - `POST /api/v1/me/notifications/{notificationId}/read`
  - `GET /api/v1/me/notification-preferences`
  - `PUT /api/v1/me/notification-preferences`
- [x] 2.2 Wire `MapNotifications()` in `Program.cs` after `MapOperations()`.
- [x] 2.3 Emit `NewReviewOnMyGuide` notifications inside `SubmitEvidence`-like flows; gate email delivery on `EmailEnabled`. (No external SMTP in this slice — outbox row records intent.)
- [x] 2.4 Emit `Trippify.Notifications` meter counters with low-cardinality `operation` tags.

## 3. Flutter client experience

- [x] 3.1 Add typed models (`CreatorFollowStatus`, `Notification`, `NotificationPreferences`) to `apps/trippify_flutter/lib/api_client.dart`.
- [x] 3.2 Add `toggleCreatorFollow`, `getCreatorFollowersCount`, `getNotifications`, `markNotificationRead`, `getNotificationPreferences`, `updateNotificationPreferences` API methods.
- [x] 3.3 Add a "Follow / Unfollow" toggle to `_AuthorScreen` and a "Get notifications" CTA on the public creator profile in `apps/trippify_flutter/lib/main.dart`.
- [x] 3.4 Build `_NotificationsScreen` accessible from the system menu showing unread badge, empty/loading states.
- [x] 3.5 Cover accessibility (semantics), loading/empty/success/error states in `apps/trippify_flutter/test/widget_test.dart`.

## 4. Concurrency, idempotency, privacy, retention, upgrades

- [x] 4.1 Tests in `tests/Trippify.ApiTests/FollowsNotificationsApiTests.cs` covering:
  - idempotent follow / unfollow
  - non-self follow rules
  - per-kind preferences suppress in-app/email
  - notification listing is restricted to recipient
  - reviews on creator's guide produce a notification for the creator
- [x] 4.2 Verify followers count is consistent and doesn't leak creator private fields.

## 5. OpenAPI, telemetry, documentation, quality gates

- [x] 5.1 Confirm `swashbuckle` documents the new endpoint contracts.
- [x] 5.2 Document `Trippify.Notifications` meter alerts (elevated 403, no PII in telemetry).
- [x] 5.3 Author `docs/notifications.md` and link from `README.md`.
- [x] 5.4 Run `dotnet build`, `dotnet test`, `flutter analyze`, and `flutter test`.
