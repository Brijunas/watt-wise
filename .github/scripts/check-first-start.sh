#!/usr/bin/env bash
# Asserts the first start of the Development stack (deploy/docs/development-stack.md, "First start"):
# on an empty volume `mise run db-init` (deploy/postgres/db-init-run.sh) bootstraps the database and
# sets the login roles' passwords with SCRAM, a second run changes nothing, no password leaks into a
# log, `docker inspect`, `docker ps` or the host's process list, and a missing or empty password
# stops the run. Then the Cli's `migrate` runs on the host and the stack's Api reaches healthy.
# Run from anywhere with Docker, jq and dotnet available:
#   bash .github/scripts/check-first-start.sh
# It runs the real deploy/docker-compose.development.yml under its own project name, never
# `wattwise-development`, with throwaway passwords in a temporary env file, and removes its
# containers, network and volumes at the end. No password is ever printed. The project name is
# fixed, so run one copy per machine at a time.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
compose="$root/deploy/docker-compose.development.yml"
project=wattwise-first-start-check
network="${project}_default"
image="" # the compose file's postgres image, read from `config` below

for tool in docker jq dotnet ps; do
  command -v "$tool" > /dev/null || {
    echo "$tool is required." >&2
    exit 2
  }
done

work="$(mktemp -d)"
chmod 755 "$work"
env_file="$work/env"
patterns="$work/passwords.txt"

# Compose reads the shell's environment before --env-file: drop every setting a developer's shell
# may export, so the check can't reach the real stack's port or passwords.
unset COMPOSE_FILE COMPOSE_PROJECT_NAME COMPOSE_PROFILES COMPOSE_ENV_FILES
while read -r name; do unset "$name"; done < <(
  {
    grep -oE '\$\{[A-Z0-9_]+' "$compose"
    printf '%s\n' CLI_DATABASE_PASSWORD BACKUP_DATABASE_PASSWORD READER_DATABASE_PASSWORD
  } | sed 's/[^A-Z0-9_]//g' | sort -u
)

dc() { docker compose -p "$project" --env-file "$env_file" -f "$compose" "$@"; }

cleanup() {
  touch "$work/ps.stop" 2> /dev/null || true
  docker compose -p "$project" --env-file "$env_file" -f "$compose" down -v --remove-orphans > /dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

passed=0
fail() {
  echo "FAIL: $*" >&2
  exit 1
}
pass() {
  passed=$((passed + 1))
  echo "ok: $*"
}
expect_eq() { # description, expected, actual
  [ "$2" = "$3" ] || fail "$1: expected '$2', got '$3'"
  pass "$1"
}

random() { head -c 4096 /dev/urandom | tr -dc 'A-Za-z0-9' | head -c "$1"; }

# A fixed free port below 32768 (rootless Docker misreports ephemeral ports), searched from $1.
free_port() {
  local port
  for port in $(seq "$1" "$(($1 + 100))"); do
    if ! (exec 3<> "/dev/tcp/127.0.0.1/$port") 2> /dev/null; then
      echo "$port"
      return
    fi
  done
  fail "no free port from $1"
}

# --- The env file: a plain value for every key the compose file requires, random passwords ------

admin_pw="$(random 32)"
cli_pw="$(random 32)"
api_pw="$(random 32)"
jobs_pw="$(random 32)"
backup_pw="$(random 32)"
reader_pw="$(random 32)"
jwt_key="$(random 64)"
pg_port="$(free_port 25432)"
api_port="$(free_port 25532)"

{
  grep -oE '\$\{[A-Z0-9_]+:\?' "$compose" | sed 's/[^A-Z0-9_]//g' | sort -u | while read -r key; do
    case "$key" in
      *_BIND_ADDRESS) echo "$key=127.0.0.1" ;;
      POSTGRES_PORT) echo "$key=$pg_port" ;;
      API_PORT) echo "$key=$api_port" ;;
      *_PORT) echo "$key=1" ;;
      POSTGRES_USER) echo "$key=admin" ;;
      POSTGRES_DB) echo "$key=postgres" ;;
      *) echo "$key=unused" ;;
    esac
  done
  echo "POSTGRES_PASSWORD=$admin_pw"
  echo "CLI_DATABASE_PASSWORD=$cli_pw"
  echo "API_DATABASE_NAME=wattwise"
  echo "API_DATABASE_USERNAME=api"
  echo "API_DATABASE_PASSWORD=$api_pw"
  echo "API_JWT_SIGNING_KEY=$jwt_key"
  echo "JOBS_DATABASE_NAME=wattwise"
  echo "JOBS_DATABASE_USERNAME=jobs"
  echo "JOBS_DATABASE_PASSWORD=$jobs_pw"
  echo "BACKUP_DATABASE_PASSWORD=$backup_pw"
  echo "READER_DATABASE_PASSWORD=$reader_pw"
  echo "FRONTEND_URL=http://localhost:3000"
  echo "ADMIN_URL=http://localhost:3001"
} > "$env_file"
chmod 600 "$env_file"

# Every plain secret the check knows, one per line, for the leak counts below (grep -f, never argv).
printf '%s\n' "$admin_pw" "$cli_pw" "$api_pw" "$jobs_pw" "$backup_pw" "$reader_pw" "$jwt_key" > "$patterns"
chmod 600 "$patterns"

# --- Helpers -------------------------------------------------------------------------------------

cid() { dc ps -aq "$1" | head -n 1; }

pw_of() {
  case "$1" in
    admin) printf '%s' "$admin_pw" ;;
    cli) printf '%s' "$cli_pw" ;;
    api) printf '%s' "$api_pw" ;;
    jobs) printf '%s' "$jobs_pw" ;;
    backup) printf '%s' "$backup_pw" ;;
    reader) printf '%s' "$reader_pw" ;;
  esac
}

# The stdin of db-init.sh for the given roles: `<role> <password>` lines.
input_lines() {
  local r
  for r in "$@"; do printf '%s %s\n' "$r" "$(pw_of "$r")"; done
}

# `mise run db-init`'s script, as op run would call it: the passwords in the environment, another
# project and env file through Compose's own variables. $1 names a variable to leave out. Output
# goes to $work/last.log and is added to $work/db-init.log.
run_db_init() {
  local rc=0
  (
    set -a
    # shellcheck source=/dev/null
    . "$env_file"
    set +a
    [ -z "${1:-}" ] || unset "$1"
    export COMPOSE_PROJECT_NAME="$project" COMPOSE_ENV_FILES="$env_file"
    sh "$root/deploy/postgres/db-init-run.sh"
  ) > "$work/last.log" 2>&1 || rc=$?
  cat "$work/last.log" >> "$work/db-init.log"
  return "$rc"
}

# db-init.sh straight on the running postgres, with stdin from the caller; output as above.
db_init_stdin() {
  local rc=0
  dc exec -T -u postgres postgres sh /db-init/db-init.sh > "$work/last.log" 2>&1 || rc=$?
  cat "$work/last.log" >> "$work/db-init.log"
  return "$rc"
}

last_log_count() { grep -cF -- "$1" "$work/last.log" || true; }

fresh() {
  dc down -v --remove-orphans > "$work/down.log" 2>&1 || {
    cat "$work/down.log" >&2
    fail "'down -v' of $project failed"
  }
}

psql_admin() { # database, sql: runs in the postgres container, where the socket is trusted
  dc exec -T postgres psql -X -U admin -d "$1" -v ON_ERROR_STOP=1 -tA -c "$2"
}

# A login from another container on the stack's network: the postgres container's own socket and
# loopback are `trust`, so a login there proves nothing.
login() { # role, password; prints psql's error, returns its exit code
  PGPASSWORD="$2" docker run --rm --network "$network" -e PGPASSWORD "$image" \
    psql -X -h postgres -U "$1" -d wattwise -tA -c 'SELECT 1' 2>&1
}

count_leaks() { # file of text to search: the number of lines holding any known password
  grep -cF -f "$patterns" "$1" || true
}

alter_count() { dc logs --no-log-prefix postgres 2> /dev/null | grep -cE 'ALTER (ROLE|USER)' || true; }

pg_authid_sum() {
  # Role settings (statement_timeout, search_path) live in pg_db_role_setting.
  psql_admin postgres "SELECT md5(string_agg(concat_ws('|', a.rolname, a.rolcanlogin, a.rolsuper, a.rolpassword, (SELECT string_agg(s.setconfig::text, ';' ORDER BY s.setdatabase) FROM pg_db_role_setting s WHERE s.setrole = a.oid)), ',' ORDER BY a.rolname)) FROM pg_authid a"
}

app_role_count() { psql_admin postgres "SELECT count(*) FROM pg_authid WHERE rolname IN ('cli', 'api', 'jobs', 'backup', 'reader')"; }

# The Cli's `migrate` on the host, as `mise run up` runs it; $1 is the password it connects with.
migrate() {
  local rc=0
  (
    export Database__Host=127.0.0.1 Database__Port="$pg_port" Database__Name=wattwise Database__Username=cli
    export Database__Password="$1" Database__Options='-c role=owner'
    dotnet run --project "$root/backend/src/WattWise.Cli" -- migrate
  ) > "$work/migrate.log" 2>&1 || rc=$?
  return "$rc"
}

sample_ps() { # stop file: the host's process list, again and again, until the file exists
  while [ ! -e "$1" ]; do
    ps -eww -o args= >> "$work/ps.log" 2> /dev/null || true
    sleep 0.1
  done
}

# --- The compose file: no db-init or cli service, nothing holds another role's password ---------

config_json="$(dc config --format json)"
for service in db-init cli; do
  expect_eq "the compose file has no $service service" false "$(jq -r --arg s "$service" '.services | has($s)' <<< "$config_json")"
done
expect_eq "the compose file has no top-level secrets" null "$(jq -c '.secrets' <<< "$config_json")"
image="$(jq -r '.services.postgres.image' <<< "$config_json")"
[ -n "$image" ] && [ "$image" != null ] || fail "the compose file's postgres service has no image"

# The apps' settings (what `docker inspect` shows) hold their own password only: no admin password,
# no other role's. Each count method is checked on the app's own password.
secret_names=(admin cli api jobs backup reader)
for name in "${secret_names[@]}"; do
  printf '%s\n' "$(pw_of "$name")" > "$work/pw-$name.txt"
done
for service in api jobs; do
  jq -c --arg s "$service" '.services[$s]' <<< "$config_json" > "$work/config-$service.json"
  expect_eq "$service doesn't wait for db-init or cli" '[]' \
    "$(jq -c '[.depends_on // {} | keys[] | select(. == "db-init" or . == "cli")]' "$work/config-$service.json")"
  for name in "${secret_names[@]}"; do
    n="$(grep -cF -f "$work/pw-$name.txt" "$work/config-$service.json" || true)"
    if [ "$name" = "$service" ]; then
      [ "$n" -ge 1 ] || fail "the leak count can't see $service's own password in its settings"
      pass "the leak count finds $service's own password in its settings (positive control)"
    else
      expect_eq "$service's settings hold no $name password" 0 "$n"
    fi
  done
done

# --- 1. The first run on an empty volume ---------------------------------------------------------

fresh
: > "$work/ps.log"
sample_ps "$work/ps.stop" &
sampler=$!
rc=0
run_db_init || rc=$?
touch "$work/ps.stop"
wait "$sampler" || true
[ "$rc" -eq 0 ] || {
  cat "$work/last.log" >&2
  fail "db-init failed on a new volume"
}
pass "db-init exits 0 on a new volume"
expect_eq "db-init ran the bootstrap and set the passwords" 1 "$(last_log_count 'db-init: done')"

for pair in "cli:$cli_pw" "api:$api_pw" "jobs:$jobs_pw" "backup:$backup_pw" "reader:$reader_pw"; do
  role="${pair%%:*}"
  out=0
  login "$role" "${pair#*:}" > "$work/login.out" || out=$?
  expect_eq "role $role logs in from another container with its password" "0 1" "$out $(tr -d '[:space:]' < "$work/login.out")"
  rc=0
  login "$role" "wrong-$(random 12)" > "$work/login.out" || rc=$?
  [ "$rc" -ne 0 ] || fail "role $role logged in with a wrong password"
  grep -q 'password authentication failed' "$work/login.out" || fail "role $role's wrong password wasn't refused as an authentication failure"
  pass "role $role is refused with a wrong password"
  expect_eq "role $role's password is a SCRAM-SHA-256 verifier with 4096 iterations" 1 \
    "$(psql_admin postgres "SELECT count(*) FROM pg_authid WHERE rolname = '$role' AND rolpassword LIKE 'SCRAM-SHA-256\$4096:%'")"
done

# 2. No plain password in the postgres log, db-init's output, `docker inspect`, `docker ps` or the
# host's process list; each count method is checked on a value it must find.
dc logs --no-log-prefix postgres > "$work/postgres.log" 2>&1
expect_eq "the postgres log holds no plain password" 0 "$(count_leaks "$work/postgres.log")"
expect_eq "db-init's output holds no plain password" 0 "$(count_leaks "$work/db-init.log")"
docker inspect "$(cid postgres)" > "$work/inspect-postgres.json"
for name in cli api jobs backup reader; do
  expect_eq "docker inspect of postgres holds no $name password" 0 "$(grep -cF -f "$work/pw-$name.txt" "$work/inspect-postgres.json" || true)"
done
[ "$(grep -cF -f "$work/pw-admin.txt" "$work/inspect-postgres.json" || true)" -ge 1 ] || fail "the leak count can't see the admin password in docker inspect of postgres"
pass "the leak count finds the admin password in docker inspect of postgres (positive control)"
{
  dc ps -a --format json
  docker ps -a --no-trunc --format '{{.Command}} {{.Names}}'
} > "$work/ps-output.txt"
expect_eq "docker ps holds no plain password" 0 "$(count_leaks "$work/ps-output.txt")"
[ "$(grep -cF 'db-init-run.sh' "$work/ps.log" || true)" -ge 1 ] || fail "the process list sampler never saw db-init-run.sh"
pass "the process list sampler saw the run ($(wc -l < "$work/ps.log") lines)"
expect_eq "the host's process list held no plain password during db-init" 0 "$(count_leaks "$work/ps.log")"

# 3. A second run changes nothing.
expect_eq "db-init didn't say 'already initialized' on the first run" 0 "$(grep -c 'already initialized, nothing to do' "$work/db-init.log" || true)"
sum_before="$(pg_authid_sum)"
alters_before="$(alter_count)"
[ "$alters_before" -ge 1 ] || fail "the first run logged no ALTER ROLE or ALTER USER; the count can't prove anything"
pass "the first run logged $alters_before ALTER statement(s) (the baseline)"
run_db_init || {
  cat "$work/last.log" >&2
  fail "db-init failed on the second run"
}
expect_eq "db-init says 'already initialized, nothing to do' on the second run" 1 "$(last_log_count 'already initialized, nothing to do')"
expect_eq "a second run leaves pg_authid (names and verifiers) unchanged" "$sum_before" "$(pg_authid_sum)"
expect_eq "a second run adds no ALTER ROLE or ALTER USER to the postgres log" "$alters_before" "$(alter_count)"

# Recovery: a role without a password (a run that stopped halfway) makes the next run do it all again.
psql_admin postgres 'ALTER ROLE backup PASSWORD NULL' > /dev/null
run_db_init || {
  cat "$work/last.log" >&2
  fail "db-init failed after backup lost its password"
}
expect_eq "db-init doesn't say 'already initialized' when a role has no password" 0 "$(last_log_count 'already initialized, nothing to do')"
for pair in "cli:$cli_pw" "api:$api_pw" "jobs:$jobs_pw" "backup:$backup_pw" "reader:$reader_pw"; do
  out=0
  login "${pair%%:*}" "${pair#*:}" > "$work/login.out" || out=$?
  expect_eq "role ${pair%%:*} logs in after the repair" "0 1" "$out $(tr -d '[:space:]' < "$work/login.out")"
done

# The run script stops on a password that op run didn't provide, before touching Docker.
for variable in POSTGRES_PASSWORD CLI_DATABASE_PASSWORD READER_DATABASE_PASSWORD; do
  rc=0
  run_db_init "$variable" || rc=$?
  [ "$rc" -ne 0 ] || fail "db-init-run.sh exited 0 without $variable"
  grep -q "$variable" "$work/last.log" || fail "db-init-run.sh's message doesn't name $variable"
  pass "db-init-run.sh stops non-zero naming $variable when it is missing"
done

# --- 4. The Cli's migrate on the host, then the Api ----------------------------------------------

echo "Running the Cli's migrate on the host (builds it first)"
rc=0
migrate "wrong-$(random 12)" || rc=$?
[ "$rc" -ne 0 ] || fail "migrate exited 0 with a password that isn't the role's"
grep -qi 'password authentication failed' "$work/migrate.log" || fail "migrate's output doesn't say the password authentication failed"
pass "migrate exits non-zero with a wrong password, and says the authentication failed"
migrate "$cli_pw" || {
  tail -n 30 "$work/migrate.log" >&2
  fail "migrate failed on a new database"
}
expect_eq "migrate's output holds no plain password" 0 "$(count_leaks "$work/migrate.log")"
history="$(psql_admin wattwise 'SELECT count(*) FROM migrations.__ef_migrations_history')"
[ "$history" -ge 1 ] || fail "migrations.__ef_migrations_history has no row"
pass "the migration history has $history row(s)"
expect_eq "Hangfire's job table exists in the hangfire schema" 1 \
  "$(psql_admin wattwise "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'hangfire' AND table_name = 'job'")"
migrate "$cli_pw" || {
  tail -n 30 "$work/migrate.log" >&2
  fail "a second migrate failed"
}
expect_eq "a second migrate leaves the migration history as it was" "$history" \
  "$(psql_admin wattwise 'SELECT count(*) FROM migrations.__ef_migrations_history')"

# The Api (dev target) builds and reaches healthy with its own role: its health check reads the database.
echo "Starting the Api (builds the dev image, then dotnet watch builds the Api)"
dc up -d --no-deps --wait --wait-timeout 900 api > "$work/up.log" 2>&1 || {
  tail -n 30 "$work/up.log" >&2
  dc logs --no-log-prefix --tail 40 api >&2 || true
  fail "the Api didn't become healthy"
}
pass "the Api is healthy"
dc logs --no-log-prefix api > "$work/api.log" 2>&1
expect_eq "the Api's log holds no plain password" 0 "$(count_leaks "$work/api.log")"
docker inspect "$(cid api)" > "$work/inspect-api.json"
for name in admin cli jobs backup reader; do
  expect_eq "docker inspect of the Api holds no $name password" 0 "$(grep -cF -f "$work/pw-$name.txt" "$work/inspect-api.json" || true)"
done
dc stop api > /dev/null 2>&1 || true

# --- 5. Failure paths, on a fresh volume: each stops before the bootstrap -------------------------

fresh
dc up -d --wait postgres > "$work/up.log" 2>&1 || {
  tail -n 30 "$work/up.log" >&2
  fail "postgres didn't start on a fresh volume"
}
: > "$work/db-init.log"

# A required role's password missing: db-init.sh stops naming the role, before creating any role.
for role in cli api jobs backup; do
  rc=0
  # shellcheck disable=SC2046 # the role names are fixed words without spaces
  input_lines $(printf '%s\n' admin cli api jobs backup reader | grep -vx "$role") | db_init_stdin || rc=$?
  [ "$rc" -ne 0 ] || fail "db-init exited 0 without the password of role $role"
  grep -q "role $role is missing or empty" "$work/last.log" || fail "db-init's message doesn't name role $role"
  pass "db-init stops non-zero naming role $role when its password is missing"
done

# A required role's password present but empty, and the admin's missing or empty.
rc=0
{
  input_lines admin cli api backup reader
  printf 'jobs \n'
} | db_init_stdin || rc=$?
[ "$rc" -ne 0 ] || fail "db-init exited 0 with an empty password for jobs"
grep -q 'role jobs is missing or empty' "$work/last.log" || fail "db-init's message doesn't name role jobs for an empty password"
pass "db-init stops non-zero naming role jobs when its password is empty"

rc=0
input_lines cli api jobs backup reader | db_init_stdin || rc=$?
[ "$rc" -ne 0 ] || fail "db-init exited 0 without the admin password"
grep -q 'the admin password is missing or empty' "$work/last.log" || fail "db-init's message doesn't name the admin password"
pass "db-init stops non-zero when the admin password is missing"

rc=0
{
  input_lines admin cli api jobs backup
  printf 'reader \n'
} | db_init_stdin || rc=$?
[ "$rc" -ne 0 ] || fail "db-init exited 0 with an empty password for reader"
grep -q 'the password for role reader is empty' "$work/last.log" || fail "db-init's message doesn't say reader's password is empty"
pass "db-init stops non-zero when reader's password is empty"

rc=0
{
  input_lines admin cli api jobs backup reader
  printf 'owner %s\n' "$(random 8)"
} | db_init_stdin || rc=$?
[ "$rc" -ne 0 ] || fail "db-init exited 0 with an unknown role on stdin"
pass "db-init stops non-zero on an unknown role"

expect_eq "no failed run created a login role" 0 "$(app_role_count)"
expect_eq "no failed run's output holds a plain password" 0 "$(count_leaks "$work/db-init.log")"

# Without a line for reader, reader is skipped and has no password; everything else works.
rc=0
input_lines admin cli api jobs backup | db_init_stdin || rc=$?
[ "$rc" -eq 0 ] || {
  cat "$work/last.log" >&2
  fail "db-init failed without reader's line"
}
expect_eq "db-init logs that reader was skipped" 1 "$(last_log_count 'db-init: no password for role reader, skipped')"
expect_eq "reader has no password" t "$(psql_admin postgres "SELECT rolpassword IS NULL FROM pg_authid WHERE rolname = 'reader'")"
rc=0
login reader "$reader_pw" > "$work/login.out" || rc=$?
[ "$rc" -ne 0 ] || fail "reader logged in without a password being set"
grep -q 'password authentication failed' "$work/login.out" || fail "reader's login wasn't refused as an authentication failure"
pass "reader can't log in without a password"
expect_eq "the other roles still got their passwords" 4 \
  "$(psql_admin postgres "SELECT count(*) FROM pg_authid WHERE rolname IN ('cli', 'api', 'jobs', 'backup') AND rolpassword LIKE 'SCRAM-SHA-256\$4096:%'")"

echo "check-first-start: $passed checks passed in ${SECONDS}s"
