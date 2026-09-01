#!/usr/bin/env bash
# Disposable PostgreSQL migration upgrade drill. This script proves the
# release-quality gate that PostgreSQL migrations apply to an empty database
# and that the upgrade from a previous release is exercised end-to-end.
#
# The script is invoked from CI in `--if-available` mode. When the
# environment lacks `docker` or a running PostgreSQL instance, the script
# exits 0 and records the limitation; the static checks in
# `tests/Trippify.ApiTests/PostgresMigrationUpgradeTests.cs` remain the
# authoritative offline proof of the migration chain.

set -euo pipefail

# Resolve the repository root regardless of where the script is invoked.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

CONTAINER_NAME="trippify-migration-drill"
DB_USER="trippify"
DB_PASSWORD="trippify_dev"
DB_NAME="trippify_drill"
CONNECTION="Host=127.0.0.1;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD}"

cleanup() {
  if command -v docker >/dev/null 2>&1; then
    docker rm -f "${CONTAINER_NAME}" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

if ! command -v docker >/dev/null 2>&1; then
  echo "postgres-upgrade-drill: docker not available; skipping real-PostgreSQL drill."
  echo "  The C# migration chain test (PostgresMigrationUpgradeTests) remains authoritative."
  exit 0
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "postgres-upgrade-drill: dotnet not available; skipping real-PostgreSQL drill."
  exit 0
fi

if ! docker info >/dev/null 2>&1; then
  echo "postgres-upgrade-drill: docker daemon not reachable; skipping real-PostgreSQL drill."
  exit 0
fi

echo "postgres-upgrade-drill: starting disposable PostgreSQL 16 (PostGIS)..."
docker run --rm -d --name "${CONTAINER_NAME}" \
  -e POSTGRES_USER="${DB_USER}" \
  -e POSTGRES_PASSWORD="${DB_PASSWORD}" \
  -e POSTGRES_DB="${DB_NAME}" \
  -p 5437:5432 \
  postgis/postgis:16-3.4 >/dev/null

# Wait for the database to be ready.
for _ in $(seq 1 30); do
  if docker exec "${CONTAINER_NAME}" pg_isready -U "${DB_USER}" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

echo "postgres-upgrade-drill: applying migrations to an empty database..."
(
  cd "${REPO_ROOT}"
  ConnectionStrings__Postgres="${CONNECTION}" \
  dotnet ef database update \
    --project src/Trippify.Infrastructure \
    --startup-project src/Trippify.Api \
    --no-build
)

# Pick the previous release's migration by listing the migration history and
# removing the head row. Then upgrade to head and re-run the smoke endpoint.
PREVIOUS_MIGRATION=$(dotnet ef migrations list \
  --project src/Trippify.Infrastructure \
  --startup-project src/Trippify.Api \
  --no-build | grep -v "20260901043105_AddCheckoutIdempotency" | tail -n 1 | tr -d ' ')
if [ -z "${PREVIOUS_MIGRATION:-}" ]; then
  echo "postgres-upgrade-drill: failed to locate a previous migration anchor."
  exit 1
fi

echo "postgres-upgrade-drill: downgrading to ${PREVIOUS_MIGRATION} to simulate a previous release..."
(
  cd "${REPO_ROOT}"
  ConnectionStrings__Postgres="${CONNECTION}" \
  dotnet ef database update "${PREVIOUS_MIGRATION}" \
    --project src/Trippify.Infrastructure \
    --startup-project src/Trippify.Api \
    --no-build
)

echo "postgres-upgrade-drill: upgrading to head and asserting smoke endpoints..."
(
  cd "${REPO_ROOT}"
  ConnectionStrings__Postgres="${CONNECTION}" \
  dotnet ef database update \
    --project src/Trippify.Infrastructure \
    --startup-project src/Trippify.Api \
    --no-build
)

# Hit a public, anonymous-safe smoke endpoint to confirm the upgraded schema
# still serves traffic. The endpoint lives at /api/v1/system/info and
# returns the application version plus the registered migration count.
API_PORT=8765
(
  cd "${REPO_ROOT}"
  ASPNETCORE_URLS="http://127.0.0.1:${API_PORT}" \
  ConnectionStrings__Postgres="${CONNECTION}" \
  SelfHosted__RunMigrationsOnStartup=false \
  dotnet run --project src/Trippify.Api --no-build >/tmp/trippify-drill.log 2>&1 &
)
API_PID=$!

for _ in $(seq 1 30); do
  if curl -fsS "http://127.0.0.1:${API_PORT}/health/live" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

STATUS=$(curl -s -o /tmp/trippify-drill-info.json -w "%{http_code}" "http://127.0.0.1:${API_PORT}/api/v1/system/info" || true)
kill "${API_PID}" 2>/dev/null || true
wait "${API_PID}" 2>/dev/null || true

if [ "${STATUS}" != "200" ]; then
  echo "postgres-upgrade-drill: smoke endpoint returned ${STATUS}; expected 200."
  cat /tmp/trippify-drill-info.json
  exit 1
fi

echo "postgres-upgrade-drill: success. Smoke endpoint responded 200."
