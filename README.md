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
