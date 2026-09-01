# Trippify

Structured travel-guide marketplace using ASP.NET Core 8, PostgreSQL/PostGIS, and Flutter.

## Prerequisites

- .NET SDK 8.0 (`dotnet --version` should print `8.0.x`)
- PostgreSQL 16 with the PostGIS extension (bundled via Docker in `docker-compose.yml`)
- Flutter SDK 3.13+ with Dart 3
- `dotnet-ef` CLI (`dotnet tool install dotnet-ef -g`)
- Optional: Docker (recommended for spinning up the database locally)
- Optional: a [Resend](https://resend.com) API key (`RESEND_API_KEY`) for real email delivery. When unset, the API uses an in-process stub and confirmation/reset emails are silently dropped.

## Repository layout

- `src/Trippify.Api` — HTTP API, endpoints, OpenAPI / Swagger.
- `src/Trippify.Application` — shared application services and adapter contracts (`IAiAssistant`, `IEmailSender`, etc.).
- `src/Trippify.Domain` — shared primitives.
- `src/Trippify.Infrastructure` — EF Core entities, migrations, and `AppDbContext`.
- `apps/trippify_flutter` — Flutter mobile/web client.
- `tests/Trippify.ApiTests` — xUnit suite for the HTTP API.
- `tests/Trippify.ArchitectureTests` — architectural invariants.
- `docs/` — design notes for every domain.
- `openspec/changes/*` — spec-driven proposals.

## Quickstart (Docker database, local API + Flutter)

```sh
# 1. Bring up PostgreSQL/PostGIS on port 5437.
docker compose up -d postgres

# 2. Restore + build.
dotnet restore Trippify.sln
dotnet build Trippify.sln

# 3. Apply EF Core migrations.
dotnet ef database update \
  --project src/Trippify.Infrastructure \
  --startup-project src/Trippify.Api

# 4. Run the API.
dotnet run --project src/Trippify.Api
# → http://localhost:5000  (Swagger UI at /swagger)

# 5. Run the Flutter app (new terminal).
cd apps/trippify_flutter
flutter pub get
flutter run --dart-define API_BASE_URL=http://localhost:5000
```

## Quickstart (fully self-hosted with Docker)

```sh
# Bring up PostgreSQL and the API together.
docker compose up --build

# Apply pending migrations through the admin API once the container is healthy.
curl -X POST http://localhost:8080/api/v1/admin/system/upgrade \
  -H "Authorization: Bearer $ADMIN_TOKEN"
```

The image pins ASP.NET Core 8 and runs `dotnet Trippify.Api.dll` on port `8080`. Connection strings come from `ConnectionStrings__Postgres`; `SelfHosted__RunMigrationsOnStartup=true` lets the bootstrap apply migrations automatically.

## Tests

```sh
# Backend
dotnet test tests/Trippify.ApiTests --nologo
dotnet test tests/Trippify.ArchitectureTests --nologo

# Frontend
cd apps/trippify_flutter
flutter test
```

## Optional development demo data

An empty Development database can be populated with fictional traveler, creator, administrator, guide, commerce, review, notification, plugin, tenant, and assisted-import data:

```sh
DemoSeed__Enabled=true dotnet run --project src/Trippify.Api
```

The seeder is disabled by default and is never registered outside the `Development` environment. It preserves unrelated existing users, adds demo fixtures when no documented demo identity exists, and skips repeat runs when the complete demo identity set already exists. If startup reports a partial demo identity set, recreate the local development database or remove all documented demo accounts before trying again.

The fictional accounts use the local-only password `Seed!Pass123`:

- `demo@example.com` — traveler with a paid entitlement, favorite, trip, review, and notifications
- `creator@example.com` — active creator with three published guides
- `admin@example.com` — administrator
- `traveler2@example.com` through `traveler5@example.com` — additional travelers used for reviews and privacy-safe aggregate metrics

Never enable or reuse these credentials in a deployed environment.

## Deployment notes

Swagger lives at `/swagger`; liveness at `/health/live` and readiness at `/health/ready`. Set `ASPNETCORE_ENVIRONMENT=Production` and inject `ConnectionStrings__Postgres`, payment provider keys, AI provider keys, and remote object-storage credentials through your secret store. Never commit secrets.

For the production payment adapter set `Payment__Provider=http` and supply `Payment__Endpoint`, `Payment__ApiKey`, and `Payment__WebhookSecret`. The same values are bound to the documented `Payment:*` keys under the `Payment` section. The configured API key is sent on every checkout request as `Authorization: Bearer …`; missing values fail startup with a `ProviderConfigurationException`.

For the production AI adapter set `Ai__Provider=http` and supply `Ai__Endpoint`, `Ai__ApiKey`, and `Ai__Model`. The configured key is sent on every `POST /v1/ai/assist` request; missing values fail startup.

For the production object-storage adapter set `ObjectStorage__Provider=s3-compatible` and supply `ObjectStorage__Endpoint`, `ObjectStorage__Bucket`, `ObjectStorage__AccessKey`, and `ObjectStorage__SecretKey`. The access key is sent as the bearer credential on every `PUT/DELETE/HEAD` request. Missing values fail startup.

Local and self-hosted deployments keep `Payment__Provider=local` and `Ai__Provider=local` (and `ObjectStorage__Provider=local`) to retain the existing disabled behaviour without exposing the production path.

To deliver real confirmation and password-reset emails, set `RESEND_API_KEY` (see [`docs/identity.md`](docs/identity.md) for the full contract). Without the key, the API silently no-ops email sends — registration still succeeds, but the user never receives the message.

> If you see `System.InvalidOperationException: Required production database configuration is missing.` at startup, the API is running in `Production` (or `Staging`) without a `ConnectionStrings:Postgres` value. Either unset `ASPNETCORE_ENVIRONMENT` (run as `Development` locally) or provide the connection string:
>
> ```sh
> export ASPNETCORE_ENVIRONMENT=Development
> # or for production:
> export ConnectionStrings__Postgres="Host=...;Database=trippify;Username=...;Password=..."
> ```
>
> EF Core reporting `No migrations were applied. The database is already up to date.` is normal during this flow — it means the schema is current; the crash happens afterwards when the API host boots.

The `docker compose` stack is intentionally minimal: it starts the database and the API and waits for the DB healthcheck. For production deployments, swap the `docker-compose.yml` for your orchestrator of choice (Kubernetes, ECS, etc.) and reuse the multi-stage `src/Trippify.Api/Dockerfile`.

After upgrading the binary, call `POST /api/v1/admin/system/upgrade` once to apply pending migrations deterministically. Capture a backup via `POST /api/v1/admin/system/backup` before every upgrade; the row stores the snapshot payload with audit metadata.

## Documentation map

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
Commercial remix revenue (license policies, ancestry, approval, multi-party shares) is documented in [`docs/commercial-remixes.md`](docs/commercial-remixes.md).
Self-hosted distribution (containers, migrations, bootstrap, backup/restore, feature flags) is documented in [`docs/self-hosted.md`](docs/self-hosted.md).
Durable background-job leases, retries, handlers, deployment, and diagnostics are documented in [`docs/background-jobs.md`](docs/background-jobs.md).
