#!/bin/sh
# First start of a database: runs bootstrap.sql and sets every login role's password, once.
#
# Run by `mise run db-init` (deploy/postgres/db-init-run.sh) inside the postgres container, as the
# `postgres` OS user: `docker compose exec -T -u postgres postgres sh /db-init/db-init.sh`. It connects
# as `admin` over the container's own socket (PGUSER and PGDATABASE default to POSTGRES_USER and
# POSTGRES_DB). The passwords arrive on stdin only, one `<role> <password>` line per role, `admin`
# first, so none is in an argument list, in the container's environment or in `docker inspect`.
# Passwords are set with psql's \password, fed from stdin: libpq hashes (SCRAM-SHA-256) on the
# client, so the server's DDL log sees only the verifier. Never `set -x`, never echo a password.
# See deploy/docs/postgres.md.

set -eu

bootstrap=/db-init/bootstrap.sql

log() {
  printf 'db-init: %s\n' "$*"
}

fail() {
  printf 'db-init: %s\n' "$*" >&2
  exit 1
}

PGUSER="${PGUSER:-${POSTGRES_USER:-}}"
PGDATABASE="${PGDATABASE:-${POSTGRES_DB:-}}"
export PGUSER PGDATABASE

# Read all of stdin before any psql runs: \password reads stdin too, and each psql gets its own pipe.
pw_admin=''
pw_cli=''
pw_api=''
pw_jobs=''
pw_backup=''
pw_reader=''
has_reader=''
while IFS=' ' read -r role password || [ -n "$role" ]; do
  case "$role" in
    admin) pw_admin="$password" ;;
    cli) pw_cli="$password" ;;
    api) pw_api="$password" ;;
    jobs) pw_jobs="$password" ;;
    backup) pw_backup="$password" ;;
    reader)
      pw_reader="$password"
      has_reader=1
      ;;
    '') ;;
    *) fail "unknown role $role on stdin" ;;
  esac
done
unset password

password_of() {
  case "$1" in
    cli) printf '%s' "$pw_cli" ;;
    api) printf '%s' "$pw_api" ;;
    jobs) printf '%s' "$pw_jobs" ;;
    backup) printf '%s' "$pw_backup" ;;
    reader) printf '%s' "$pw_reader" ;;
  esac
}

[ -n "$pw_admin" ] || fail 'the admin password is missing or empty'
PGPASSWORD="$pw_admin"
export PGPASSWORD
unset pw_admin

# The roles to set up. cli, api, jobs and backup are required in every environment: a missing or
# empty password stops here. Only `reader` is optional: without a line for it the role gets no
# password and is skipped. A reader line with an empty password is always a mistake.
set_roles=''
names=''
for role in cli api jobs backup reader; do
  if [ "$role" = reader ]; then
    if [ -z "$has_reader" ]; then
      log "no password for role $role, skipped"
      continue
    fi
    [ -n "$pw_reader" ] || fail "the password for role $role is empty"
  else
    [ -n "$(password_of "$role")" ] || fail "the password for role $role is missing or empty"
  fi
  set_roles="$set_roles $role"
  names="${names:+$names,}'$role'"
done

count_roles() {
  # $1 is an extra condition on pg_authid, built from the fixed role names above.
  psql -X -v ON_ERROR_STOP=1 -tA -c "SELECT count(*) FROM pg_authid WHERE rolname IN ($names) AND $1"
}

total=0
for role in $set_roles; do
  total=$((total + 1))
done

# Done check: with every role of the set holding a password, a later run has nothing to do. It
# runs no bootstrap and no ALTER, so a second run changes no role and no password. A run that
# stopped halfway leaves a role without one, so the next run does everything again.
done_count="$(count_roles 'rolpassword IS NOT NULL')" || fail 'cannot read pg_authid'
if [ "$done_count" = "$total" ]; then
  log 'already initialized, nothing to do'
  exit 0
fi

log 'running bootstrap.sql'
psql -X -v ON_ERROR_STOP=1 -q -f "$bootstrap" >/dev/null

for role in $set_roles; do
  log "setting the password of role $role"
  # printf is a builtin, so the value never appears in an argument list. \password reads
  # /dev/tty when one exists, so this must run without a TTY (`exec -T`).
  password="$(password_of "$role")"
  printf '%s\n%s\n' "$password" "$password" | psql -X -v ON_ERROR_STOP=1 -q -c "\\password $role" >/dev/null ||
    fail "setting the password of role $role failed"
done
unset password

for role in $set_roles; do
  scram="$(psql -X -v ON_ERROR_STOP=1 -tA -c "SELECT count(*) FROM pg_authid WHERE rolname = '$role' AND rolpassword LIKE 'SCRAM-SHA-256\$%'")"
  [ "$scram" = 1 ] || fail "role $role has no SCRAM-SHA-256 password after setting it"
done

log 'done'
