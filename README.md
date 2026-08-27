# Trippify

Structured travel-guide marketplace using ASP.NET Core 8, PostgreSQL/PostGIS, and Flutter.

## Development

1. `docker compose up -d postgres` (PostgreSQL is exposed on development port `5437`).
2. `/home/paul/.dotnet/dotnet restore Trippify.sln`
3. `/home/paul/.dotnet/dotnet ef database update --project src/Trippify.Infrastructure --startup-project src/Trippify.Api`
4. `/home/paul/.dotnet/dotnet run --project src/Trippify.Api`
5. Run the Flutter app with `--dart-define API_BASE_URL=http://localhost:5000`.

Swagger is at `/swagger`; liveness and readiness are `/health/live` and `/health/ready`. Production must inject database and provider secrets. Never commit secrets.

Identity security, privacy projections, and operator controls are documented in [`docs/identity.md`](docs/identity.md).
Structured guide ownership, concurrency, privacy, and operations are documented in [`docs/guides.md`](docs/guides.md).
Day routes, transport segments, and party-size budgets are documented in [`docs/planning.md`](docs/planning.md).
Publication rules, paid previews, and the discovery catalog are documented in [`docs/discovery.md`](docs/discovery.md).
Checkout, webhook-confirmed orders, ledgers, and entitlements are documented in [`docs/commerce.md`](docs/commerce.md).
Favorites, My Trips, and permission-aware forks with provenance are documented in [`docs/library.md`](docs/library.md).
Verified-purchaser reviews, author replies, moderation, reports, and update feedback are documented in [`docs/reviews.md`](docs/reviews.md).
Verified trip evidence, retention, badge lifecycle, and opt-in coarse actual metrics are documented in [`docs/verified-trips.md`](docs/verified-trips.md).
Creator sales/income dashboards and audited role-scoped administration are documented in [`docs/operations.md`](docs/operations.md).
Creator follows and preference-aware notifications are documented in [`docs/notifications.md`](docs/notifications.md).
Immutable releases, changelogs, buyer update notifications, and freshness indicators are documented in [`docs/versioning.md`](docs/versioning.md).
Integration plugin system (manifests, signatures, scope, lifecycle, audit) is documented in [`docs/plugins.md`](docs/plugins.md).
Managed SaaS hosting (tenants, subscriptions, quotas, export/deletion) is documented in [`docs/managed-saas.md`](docs/managed-saas.md).
Assisted imports, draft review, and linked translations are documented in [`docs/assisted-import.md`](docs/assisted-import.md).
