#!/usr/bin/env bash
# Asserts the first start of the Development stack (deploy/docs/development-stack.md, "First start"):
# on an empty volume `docker compose up cli` runs db-init (bootstrap and the login roles' passwords)
# and then cli (migrations and the smoke accounts), a second `up` changes nothing, no password
# leaks into a log or `docker inspect`, and a missing secret or a wrong password stops the start.
# Run from anywhere with Docker (and jq) available:
#   bash .github/scripts/check-first-start.sh
# It runs the real deploy/docker-compose.development.yml under its own project name, never
# `wattwise-development`, with throwaway passwords in a temporary env file, and removes its
# containers, network and volumes at the end. No password is ever printed. The project name is
# fixed, so run one copy per machine at a time. The apps' settings hold no admin password and no
# other role's password, and db-init's hold none; cli's hold its own.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
compose="$root/deploy/docker-compose.development.yml"
project=wattwise-first-start-check
network="${project}_default"
image="" # the compose file's postgres image, read from `config` below

for tool in docker jq; do
  command -v "$tool" > /dev/null || {
    echo "$tool is required." >&2
    exit 2
  }
done

work="$(mktemp -d)"
chmod 755 "$work"
env_file="$work/env"
env_wrong="$work/env.wrong-cli-password"
override="$work/override.yml"
patterns="$work/passwords.txt"

# Compose reads the shell's environment before --env-file: drop every setting a developer's shell
# may export, so the check can't reach the real stack's port or passwords.
unset COMPOSE_FILE COMPOSE_PROJECT_NAME COMPOSE_PROFILES COMPOSE_ENV_FILES
while read -r name; do unset "$name"; done < <(
  {
    grep -oE '\$\{[A-Z0-9_]+' "$compose"
    grep -oE '^ +environment: [A-Z0-9_]+$' "$compose" # the top-level secrets
  } | sed 's/[^A-Z0-9_]//g' | sort -u
)

dc() { docker compose -p "$project" --env-file "$env_file" -f "$compose" "$@"; }

cleanup() {
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

random() { head -c 256 /dev/urandom | tr -dc 'A-Za-z0-9' | head -c "$1"; }

# A fixed free port below 32768 (rootless Docker misreports ephemeral ports).
free_port() {
  local port
  for port in $(seq 25432 25532); do
    if ! (exec 3<> "/dev/tcp/127.0.0.1/$port") 2> /dev/null; then
      echo "$port"
      return
    fi
  done
  fail "no free port between 25432 and 25532"
}

# --- The env file: a plain value for every key the compose file requires, random passwords ------

admin_pw="$(random 32)"
cli_pw="$(random 32)"
api_pw="$(random 32)"
jobs_pw="$(random 32)"
backup_pw="$(random 32)"
reader_pw="$(random 32)"
smoke_password() { printf 'Aa1-%s' "$(random 24)"; } # meets the Identity rules: upper, lower, digit, symbol, 14+
smoke_user_pw="$(smoke_password)"
smoke_admin_pw="$(smoke_password)"
smoke_user_email="user@first-start.example"
smoke_admin_email="admin@first-start.example"

{
  # Every key behind `:?` needs a value for compose to read the file; only postgres is started.
  grep -oE '\$\{[A-Z0-9_]+:\?' "$compose" | sed 's/[^A-Z0-9_]//g' | sort -u | while read -r key; do
    case "$key" in
      *_BIND_ADDRESS) echo "$key=127.0.0.1" ;;
      POSTGRES_PORT) echo "$key=$(free_port)" ;;
      *_PORT) echo "$key=1" ;;
      POSTGRES_USER) echo "$key=admin" ;;
      POSTGRES_DB) echo "$key=postgres" ;;
      *) echo "$key=unused" ;;
    esac
  done
  echo "POSTGRES_PASSWORD=$admin_pw"
  echo "CLI_DATABASE_NAME=wattwise"
  echo "CLI_DATABASE_USERNAME=cli"
  echo "CLI_DATABASE_PASSWORD=$cli_pw"
  echo 'CLI_DATABASE_OPTIONS="-c role=owner"'
  echo "CLI_SMOKE_USER_EMAIL=$smoke_user_email"
  echo "CLI_SMOKE_USER_PASSWORD=$smoke_user_pw"
  echo "CLI_SMOKE_ADMIN_EMAIL=$smoke_admin_email"
  echo "CLI_SMOKE_ADMIN_PASSWORD=$smoke_admin_pw"
  echo "API_DATABASE_PASSWORD=$api_pw"
  echo "JOBS_DATABASE_PASSWORD=$jobs_pw"
  echo "BACKUP_DATABASE_PASSWORD=$backup_pw"
  echo "READER_DATABASE_PASSWORD=$reader_pw"
} > "$env_file"
chmod 600 "$env_file"
# The wrong-password case: the same file, with another password for cli's connection only.
grep -v '^CLI_DATABASE_PASSWORD=' "$env_file" > "$env_wrong"
echo "CLI_DATABASE_PASSWORD=$(random 32)" >> "$env_wrong"

# Every plain secret the check knows, one per line, for the leak counts below (grep -f, never argv).
printf '%s\n' "$admin_pw" "$cli_pw" "$api_pw" "$jobs_pw" "$backup_pw" "$reader_pw" "$smoke_user_pw" "$smoke_admin_pw" > "$patterns"
chmod 600 "$patterns"

# --- Helpers -------------------------------------------------------------------------------------

cid() { dc ps -aq "$1" | head -n 1; }

wait_done() { # service: waits until its one-shot container has exited
  local id
  id="$(cid "$1")"
  [ -n "$id" ] || fail "$1 has no container"
  timeout 300 docker wait "$id" > /dev/null || fail "$1 didn't finish within 300 seconds"
}

exit_code() { docker inspect -f '{{.State.ExitCode}}' "$(cid "$1")"; }

# `up -d cli`, then waits for the one-shots. Prints compose's output on an unexpected failure.
start_ok() { # description
  dc up -d cli > "$work/up.log" 2>&1 || {
    cat "$work/up.log" >&2
    fail "$1: 'up -d cli' failed"
  }
  wait_done db-init
  wait_done cli
}

# The same `up`, expected to fail because db-init stops.
start_fails() { # description
  local rc=0
  dc up -d cli > "$work/up.log" 2>&1 || rc=$?
  [ "$rc" -ne 0 ] || fail "$1: 'up -d cli' exited 0"
  wait_done db-init
}

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

db_init_log() { dc logs --no-log-prefix db-init 2>&1; }

cli_state() { # prints "absent" or the container's status
  local id
  id="$(cid cli)"
  if [ -z "$id" ]; then echo absent; else docker inspect -f '{{.State.Status}}' "$id"; fi
}

# --- 6. The compose file: api and jobs wait for db-init and cli ---------------------------------

config_json="$(dc config --format json)"
for service in api jobs; do
  for dependency in db-init cli; do
    expect_eq "$service waits for $dependency to complete successfully" service_completed_successfully \
      "$(jq -r --arg s "$service" --arg d "$dependency" '.services[$s].depends_on[$d].condition // "none"' <<< "$config_json")"
  done
done
image="$(jq -r '.services.postgres.image' <<< "$config_json")"
[ -n "$image" ] && [ "$image" != null ] || fail "the compose file's postgres service has no image"

# The apps' settings (what `docker inspect` shows) hold their own password only: no admin password,
# no other role's, no secret mounts. Each count method is checked on the app's own password.
secret_names=(admin cli api jobs backup reader)
secret_values=("$admin_pw" "$cli_pw" "$api_pw" "$jobs_pw" "$backup_pw" "$reader_pw")
for i in "${!secret_names[@]}"; do
  printf '%s\n' "${secret_values[$i]}" > "$work/pw-${secret_names[$i]}.txt"
done
for service in api jobs; do
  jq -c --arg s "$service" '.services[$s]' <<< "$config_json" > "$work/config-$service.json"
  expect_eq "$service's settings mount no Compose secret" null "$(jq -c '.secrets' "$work/config-$service.json")"
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

# --- Build once, then the first start on an empty volume ----------------------------------------

fresh
echo "Building the cli image"
dc build cli > "$work/build.log" 2>&1 || {
  tail -n 30 "$work/build.log" >&2
  fail "building the cli image failed"
}

start_ok "first start"

# 1. db-init and cli exit 0; every role logs in from another container, with SCRAM passwords.
expect_eq "db-init exits 0 on a new volume" 0 "$(exit_code db-init)"
expect_eq "cli exits 0 on a new volume" 0 "$(exit_code cli)"
expect_eq "db-init ran the bootstrap and set the passwords" 1 "$(db_init_log | grep -c '^db-init: done$' || true)"
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

# 2. The schema is migrated and the smoke accounts exist.
history="$(psql_admin wattwise 'SELECT count(*) FROM migrations.__ef_migrations_history')"
[ "$history" -ge 1 ] || fail "migrations.__ef_migrations_history has no row"
pass "the migration history has $history row(s)"
hangfire="$(psql_admin wattwise "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'hangfire' AND table_name = 'job'")"
expect_eq "Hangfire's job table exists in the hangfire schema" 1 "$hangfire"
expect_eq "both smoke accounts exist" 2 \
  "$(psql_admin wattwise "SELECT count(*) FROM app.identity_users WHERE normalized_email IN ('$smoke_user_email', '$smoke_admin_email')")"

# 4 (first start). No plain password in the postgres log or in db-init's container; none of the admin's in cli's.
dc logs --no-log-prefix postgres > "$work/postgres.log" 2>&1
expect_eq "the postgres log holds no plain password" 0 "$(count_leaks "$work/postgres.log")"
dc logs --no-log-prefix db-init > "$work/db-init.log" 2>&1
expect_eq "the db-init log holds no plain password" 0 "$(count_leaks "$work/db-init.log")"
dc logs --no-log-prefix cli > "$work/cli.log" 2>&1
expect_eq "the cli log holds no plain password" 0 "$(count_leaks "$work/cli.log")"
docker inspect "$(cid db-init)" > "$work/inspect-db-init.json"
expect_eq "docker inspect of db-init holds no plain password" 0 "$(count_leaks "$work/inspect-db-init.json")"
docker inspect "$(cid cli)" > "$work/inspect-cli.json"
expect_eq "docker inspect of cli holds no admin password" 0 "$(grep -cF -f "$work/pw-admin.txt" "$work/inspect-cli.json" || true)"
# The positive control: the same count method does find cli's own password in its environment.
[ "$(grep -cF -f "$work/pw-cli.txt" "$work/inspect-cli.json" || true)" -ge 1 ] || fail "the leak count can't see cli's own password in docker inspect of cli"
pass "the leak count finds cli's own password in docker inspect of cli (positive control)"

# 3. A second `up` changes nothing.
expect_eq "db-init didn't say 'already initialized' on the first start" 0 "$(db_init_log | grep -c 'already initialized, nothing to do' || true)"
sum_before="$(pg_authid_sum)"
alters_before="$(alter_count)"
[ "$alters_before" -ge 1 ] || fail "the first start logged no ALTER ROLE or ALTER USER; the count can't prove anything"
pass "the first start logged $alters_before ALTER statement(s) (the baseline)"
start_ok "second start"
expect_eq "db-init exits 0 on the second start" 0 "$(exit_code db-init)"
expect_eq "cli exits 0 on the second start" 0 "$(exit_code cli)"
[ "$(db_init_log | grep -c 'already initialized, nothing to do' || true)" -ge 1 ] || fail "db-init didn't say 'already initialized, nothing to do' on the second start"
pass "db-init says 'already initialized, nothing to do' on the second start"
expect_eq "a second start leaves pg_authid (names and verifiers) unchanged" "$sum_before" "$(pg_authid_sum)"
expect_eq "a second start adds no ALTER ROLE or ALTER USER to the postgres log" "$alters_before" "$(alter_count)"

# Recovery: a role without a password (a start that stopped halfway) makes the next start do it all again.
initialized_before="$(db_init_log | grep -c 'already initialized, nothing to do' || true)"
psql_admin postgres 'ALTER ROLE backup PASSWORD NULL' > /dev/null
start_ok "start after backup lost its password"
expect_eq "db-init doesn't say 'already initialized' when a role has no password" "$initialized_before" \
  "$(db_init_log | grep -c 'already initialized, nothing to do' || true)"
expect_eq "db-init exits 0 when it repairs a role without a password" 0 "$(exit_code db-init)"
for pair in "cli:$cli_pw" "api:$api_pw" "jobs:$jobs_pw" "backup:$backup_pw" "reader:$reader_pw"; do
  out=0
  login "${pair%%:*}" "${pair#*:}" > "$work/login.out" || out=$?
  expect_eq "role ${pair%%:*} logs in after the repair" "0 1" "$out $(tr -d '[:space:]' < "$work/login.out")"
done

# 5c. A wrong password for cli: db-init has nothing to do, so cli's own password differs from the role's.
env_file="$env_wrong"
start_ok "wrong cli password" # starts cli and waits for it to exit; here it exits non-zero
env_file="$work/env"
[ "$(exit_code cli)" -ne 0 ] || fail "cli exited 0 with a password that isn't the role's"
grep -qi 'password authentication failed' <<< "$(dc logs --no-log-prefix cli 2>&1)" || fail "cli's log doesn't say the password authentication failed"
pass "cli exits non-zero with a wrong password, and its log says the authentication failed"

# --- 5. Failure paths, each on a fresh volume (a reused project keeps its secret files) ---------

# A required role's secret missing (an empty variable mounts no file): db-init stops naming the role, cli doesn't run.
for role in cli api jobs backup; do
  upper="$(tr '[:lower:]' '[:upper:]' <<< "$role")"
  fresh
  grep -v "^${upper}_DATABASE_PASSWORD=" "$env_file" > "$work/env.missing"
  echo "${upper}_DATABASE_PASSWORD=" >> "$work/env.missing"
  env_file="$work/env.missing"
  start_fails "missing secret of $role"
  env_file="$work/env"
  [ "$(exit_code db-init)" -ne 0 ] || fail "db-init exited 0 without the secret of role $role"
  grep -q "role $role is missing or empty" <<< "$(db_init_log)" || fail "db-init's message doesn't name role $role"
  pass "db-init stops non-zero naming role $role when its secret is missing"
  case "$(cli_state)" in absent | created) pass "cli didn't run after db-init failed for role $role" ;; *) fail "cli ran although db-init failed for role $role" ;; esac
done

# A required role's secret present but empty (a file, through an override).
fresh
: > "$work/empty-secret"
chmod 644 "$work/empty-secret"
jobs_secret="postgres-jobs-$(printf 'pass%s' word)" # the compose file's secret name for jobs
cat > "$override" << EOF
secrets:
  $jobs_secret: !override
    file: $work/empty-secret
EOF
rc=0
dc -f "$override" up -d cli > "$work/up.log" 2>&1 || rc=$?
[ "$rc" -ne 0 ] || fail "'up -d cli' exited 0 with an empty secret file for jobs"
wait_done db-init
[ "$(exit_code db-init)" -ne 0 ] || fail "db-init exited 0 with an empty secret file for jobs"
grep -q 'for role jobs is empty' <<< "$(db_init_log)" || fail "db-init's message doesn't name role jobs for an empty file"
pass "db-init stops non-zero naming role jobs when its secret file is empty"

# Without reader's secret, reader is skipped and has no password; everything else works.
fresh
grep -v '^READER_DATABASE_PASSWORD=' "$env_file" > "$work/env.no-reader"
echo "READER_DATABASE_PASSWORD=" >> "$work/env.no-reader"
env_file="$work/env.no-reader"
start_ok "no reader secret"
env_file="$work/env"
expect_eq "db-init exits 0 without reader's secret" 0 "$(exit_code db-init)"
expect_eq "cli exits 0 without reader's secret" 0 "$(exit_code cli)"
expect_eq "db-init logs that reader was skipped" 1 "$(db_init_log | grep -c '^db-init: no secret for role reader, skipped$' || true)"
expect_eq "reader has no password" t "$(psql_admin postgres "SELECT rolpassword IS NULL FROM pg_authid WHERE rolname = 'reader'")"
rc=0
login reader "$reader_pw" > "$work/login.out" || rc=$?
[ "$rc" -ne 0 ] || fail "reader logged in without a password being set"
grep -q 'password authentication failed' "$work/login.out" || fail "reader's login wasn't refused as an authentication failure"
pass "reader can't log in without a password"
expect_eq "the other roles still got their passwords" 4 \
  "$(psql_admin postgres "SELECT count(*) FROM pg_authid WHERE rolname IN ('cli', 'api', 'jobs', 'backup') AND rolpassword LIKE 'SCRAM-SHA-256\$4096:%'")"

echo "check-first-start: $passed checks passed in ${SECONDS}s"
