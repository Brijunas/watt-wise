#!/bin/sh
# `mise run db-init`: sets up a new, empty Development database. Starts PostgreSQL, then runs
# deploy/postgres/db-init.sh inside it (bootstrap.sql and every login role's password). It does
# nothing once the passwords exist, so a rerun is harmless.
#
# Run it through op run, which puts the passwords in this process's environment (compose.env):
#   op run --env-file deploy/development/compose.env -- sh deploy/postgres/db-init-run.sh
# The passwords leave only on the stdin of `docker compose exec`: never in an argument list and
# never in the exec's environment (no `-e`). COMPOSE_PROJECT_NAME and COMPOSE_ENV_FILES, when set,
# are Compose's own and select another project or env file (the first-start check uses both).
# Never `set -x`, never echo a password. See deploy/docs/postgres.md.

set -eu

root="$(cd "$(dirname "$0")/../.." && pwd)"
compose="$root/deploy/docker-compose.development.yml"

: "${POSTGRES_PASSWORD:?run through op run --env-file deploy/development/compose.env}"
: "${CLI_DATABASE_PASSWORD:?run through op run --env-file deploy/development/compose.env}"
: "${API_DATABASE_PASSWORD:?run through op run --env-file deploy/development/compose.env}"
: "${JOBS_DATABASE_PASSWORD:?run through op run --env-file deploy/development/compose.env}"
: "${BACKUP_DATABASE_PASSWORD:?run through op run --env-file deploy/development/compose.env}"
: "${READER_DATABASE_PASSWORD:?run through op run --env-file deploy/development/compose.env}"

# POSTGRES_PASSWORD is read by the image only when the volume is empty.
docker compose -f "$compose" up -d --wait postgres

# printf is a builtin, so no password is in an argument list.
{
  printf 'admin %s\n' "$POSTGRES_PASSWORD"
  printf 'cli %s\n' "$CLI_DATABASE_PASSWORD"
  printf 'api %s\n' "$API_DATABASE_PASSWORD"
  printf 'jobs %s\n' "$JOBS_DATABASE_PASSWORD"
  printf 'backup %s\n' "$BACKUP_DATABASE_PASSWORD"
  printf 'reader %s\n' "$READER_DATABASE_PASSWORD"
} | docker compose -f "$compose" exec -T -u postgres postgres sh /db-init/db-init.sh
