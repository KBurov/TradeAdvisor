#!/usr/bin/env bash
set -euo pipefail

# Find env file (prefer compose/.env, else repo .env.local)
ENV_FILE=""
if [[ -f "$(dirname "$0")/compose/.env" ]]; then
  ENV_FILE="$(dirname "$0")/compose/.env"
elif [[ -f "$(dirname "$0")/../.env.local" ]]; then
  ENV_FILE="$(dirname "$0")/../.env.local"
fi

if [[ -n "${ENV_FILE}" ]]; then
  set -o allexport
  source "${ENV_FILE}"
  set +o allexport
else
  echo "WARN: no env file found; falling back to defaults"
fi

PGHOST="${PGHOST:-127.0.0.1}"
PGPORT="${POSTGRES_PORT:-5432}"
PGUSER="${POSTGRES_USER:-trade}"
PGPASSWORD="${POSTGRES_PASSWORD:-trade}"
PGDATABASE="${POSTGRES_DB:-trade}"

export PGPASSWORD

PSQL="psql -h ${PGHOST} -p ${PGPORT} -U ${PGUSER} -d ${PGDATABASE} -v ON_ERROR_STOP=1"

# Create schema_version table if missing
$PSQL <<'SQL'
CREATE TABLE IF NOT EXISTS public.schema_version (
  version      INTEGER PRIMARY KEY,
  filename     TEXT NOT NULL,
  applied_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
SQL

# Apply migrations in order
DIR="$(dirname "$0")/sql"
shopt -s nullglob
for f in "${DIR}"/[0-9][0-9][0-9]_*.sql; do
  base="$(basename "$f")"
  ver="${base%%_*}"            # 001 from 001_foo.sql

  already="$($PSQL -t -A -c "SELECT 1 FROM public.schema_version WHERE version = ${ver} LIMIT 1" || true)"
  if [[ "${already}" == "1" ]]; then
    echo "== Skipping ${base} (already applied)"
    continue
  fi

  echo "== Applying ${base}"
  $PSQL -f "$f"
  $PSQL -c "INSERT INTO public.schema_version(version, filename) VALUES (${ver}, '$base');"
done

echo "== Done."
