#!/bin/sh
# First start of a database: runs bootstrap.sql and sets every login role's password, once.
#
# Runs as the one-shot `db-init` service on the pinned postgres image, as `admin` (PGUSER). The
# connection comes from PGHOST, PGPORT, PGUSER and PGDATABASE; every password is a Compose secret
# file under /run/secrets, so none is in the container's configured environment and `docker
# inspect` shows none (PGPASSWORD is exported only inside this process).
# Passwords are set with psql's \password, fed from stdin: libpq hashes (SCRAM-SHA-256) on the
# client, so the server's DDL log sees only the verifier. Never `set -x`, never echo a password.
# See deploy/docs/postgres.md.

set -eu

secrets=/run/secrets
bootstrap=/db-init/bootstrap.sql

log() {
  printf 'db-init: %s\n' "$*"
}

fail() {
  printf 'db-init: %s\n' "$*" >&2
  exit 1
}

# $(cat ...) strips the trailing newline of the file.
admin_password="$(cat "$secrets/postgres-admin-password")"
[ -n "$admin_password" ] || fail 'the admin password is empty'
PGPASSWORD="$admin_password"
export PGPASSWORD
unset admin_password

# The roles to set up. cli, api, jobs and backup are required in every environment: a missing or
# empty secret stops here (Compose mounts no file for an empty variable). Only `reader` is
# optional: outside Development it gets no password, so a missing file skips it. An existing
# empty file is always a mistake.
set_roles=''
names=''
for role in cli api jobs backup reader; do
  file="$secrets/postgres-$role-password"
  if [ ! -f "$file" ]; then
    [ "$role" = reader ] || fail "the secret postgres-$role-password for role $role is missing or empty"
    log "no secret for role $role, skipped"
    continue
  fi
  [ -s "$file" ] || fail "the secret postgres-$role-password for role $role is empty"
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

# Done check: with every role of the set holding a password, a later `up` has nothing to do. It
# runs no bootstrap and no ALTER, so a second `up` changes no role and no password. A run that
# stopped halfway leaves a role without one, so the next `up` does everything again.
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
  # /dev/tty when one exists, so a manual run must have no TTY: `docker compose run -T db-init`.
  password="$(cat "$secrets/postgres-$role-password")"
  printf '%s\n%s\n' "$password" "$password" | psql -X -v ON_ERROR_STOP=1 -q -c "\\password $role" >/dev/null ||
    fail "setting the password of role $role failed"
done
unset password

for role in $set_roles; do
  scram="$(psql -X -v ON_ERROR_STOP=1 -tA -c "SELECT count(*) FROM pg_authid WHERE rolname = '$role' AND rolpassword LIKE 'SCRAM-SHA-256\$%'")"
  [ "$scram" = 1 ] || fail "role $role has no SCRAM-SHA-256 password after setting it"
done

log 'done'
