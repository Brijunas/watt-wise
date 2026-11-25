#!/usr/bin/env bash
# Asserts the behaviour of `mise run setup` (docs/setup.md, "Checking setup") on a throwaway container of
# a stock distribution (the CI matrix entry) that has only git, Docker's CLI and mise, then runs the eight
# development.md checks on the set-up machine. Run from anywhere with Docker running:
#   bash .github/scripts/check-setup.sh                         CI mode: setup --skip-secrets, no token
#   bash .github/scripts/check-setup.sh --token-file <file>     also the secrets steps, with that token
#   bash .github/scripts/check-setup.sh --source <dir>          test <dir>'s working tree, uncommitted changes included
#   bash .github/scripts/check-setup.sh --base <image> [--install <command>] [--install-op <command>]   another matrix entry
# By default it tests the committed HEAD of the repository this script is in (a clone), not the working tree.
# The container sees the host's Docker socket (the checks' Testcontainers fixtures start containers on it).
# The token file holds the bare token; it is mounted read-only into the container and no value is ever printed.
set -euo pipefail

usage() {
  echo "usage: check-setup.sh [--source <dir>] [--token-file <file>] [--base <image> [--install <command>] [--install-op <command>]]" >&2
  exit 2
}

token_src=""
source_dir=""
base_image=""
install_cmd=""
install_op_cmd=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    --token-file)
      [ "$#" -ge 2 ] || usage
      token_src="$2"
      shift 2
      ;;
    --source)
      [ "$#" -ge 2 ] || usage
      source_dir="$2"
      shift 2
      ;;
    --base)
      [ "$#" -ge 2 ] || usage
      base_image="$2"
      shift 2
      ;;
    --install)
      [ "$#" -ge 2 ] || usage
      install_cmd="$2"
      shift 2
      ;;
    --install-op)
      [ "$#" -ge 2 ] || usage
      install_op_cmd="$2"
      shift 2
      ;;
    *) usage ;;
  esac
done
if [ -n "$token_src" ]; then
  [ -r "$token_src" ] || {
    echo "The token file is not readable." >&2
    exit 2
  }
  token_src="$(realpath "$token_src")"
  command -v jq > /dev/null || {
    echo "jq is required." >&2
    exit 2
  }
fi
with_token=false
if [ -n "$token_src" ]; then with_token=true; fi

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
work="$(mktemp -d)"
container="wattwise-setup-check-$$"
cleanup() {
  docker rm -f "$container" > /dev/null 2>&1 || true
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

# The check's machine: everything runs as `dev`, prep as the container's root. The clone sits at the
# same path as the repository on the host, like on a developer's machine.
dev_home=/home/dev
repo="$root"
config_dir="$dev_home/.config/watt-wise"
token_file="$config_dir/op.env"
x_root() { docker exec "$container" "$@"; }
x_dev() { docker exec -u dev -e HOME="$dev_home" -w "$repo" "$container" "$@"; }
# A login shell for `dev` in the clone: ~/.bashrc (sourced by the image's ~/.bash_profile) activates mise like a developer's terminal does.
# The script is $1, its arguments are $0 and on.
dev_run() { x_dev bash -lc "$@"; }

# setup's own --derive-url and --check-bind, which the deploy/.env checks and the smoke rows use.
cli() { dev_run 'node tools/setup/setup.ts "$@"' setup "$@"; }

run_setup() { # log file, setup arguments; prints the output and returns setup's exit code
  local log="$1"
  shift
  dev_run 'mise run setup -- "$@"' setup "$@" < /dev/null 2>&1 | tee "$log"
  return "${PIPESTATUS[0]}"
}
expect_setup_ok() { # description, log file, setup arguments
  local description="$1" log="$2" rc=0
  shift 2
  run_setup "$log" "$@" || rc=$?
  expect_eq "$description exits 0" 0 "$rc"
}
expect_unchanged() { # description, log file: every step line says unchanged or skipped
  if grep -E '^setup: [^:]+: changed' "$2"; then fail "$1: a step reported 'changed' (lines above)"; fi
  grep -qE '^setup: [^:]+: unchanged' "$2" || fail "$1: no step reported 'unchanged'"
  pass "$1 reports only unchanged and skipped steps"
}

snapshot() { # prints what a rerun must leave as it was
  # shellcheck disable=SC2016
  x_dev bash -c '
    echo "bashrc: $(sha256sum < ~/.bashrc)"
    echo "git status:"; git status --porcelain
    echo "packages:"
    if command -v rpm > /dev/null; then rpm -qa | sort
    elif command -v dpkg-query > /dev/null; then dpkg-query -W | sort
    elif command -v apk > /dev/null; then apk info | sort
    else echo "no supported package manager (rpm, dpkg, apk)" >&2; exit 3
    fi
    echo "mise:"; ~/.local/bin/mise ls --installed
    if [ -f deploy/.env ]; then echo "deploy/.env: $(sha256sum < deploy/.env)"; fi
    echo "husky:"; find .husky/_ -type f 2>/dev/null | sort | xargs -r sha256sum
    echo "config folder:"; find ~/.config/watt-wise -printf "%p %m %u\n" 2>/dev/null | sort
    # The token is only hashed twice over, so a difference shows without printing anything useful.
    if [ -e ~/.config/watt-wise/op.env ]; then echo "token: $(sha256sum < ~/.config/watt-wise/op.env | sha256sum)"; fi
  ' 2>&1
}

echo "Building the setup-check image"
build_args=()
if [ -n "$base_image" ]; then build_args+=(--build-arg "BASE=$base_image"); fi
if [ -n "$install_cmd" ]; then build_args+=(--build-arg "INSTALL=$install_cmd"); fi
if [ -n "$install_op_cmd" ]; then build_args+=(--build-arg "INSTALL_OP=$install_op_cmd"); fi
docker build -q "${build_args[@]}" -t wattwise-setup-check - < "$root/.github/setup-check/Dockerfile" > /dev/null

# The committed HEAD, as a clone: this worktree's .git can be a pointer file.
if [ -n "$source_dir" ]; then
  # The given working tree, as the container's clone source: its tracked and untracked files that
  # git doesn't ignore, committed into a throwaway repository (the real one is never touched).
  [ -d "$source_dir" ] || {
    echo "The source directory does not exist." >&2
    exit 2
  }
  source_dir="$(cd "$source_dir" && pwd)"
  git -C "$source_dir" rev-parse --is-inside-work-tree > /dev/null 2>&1 || {
    echo "The source directory is not a git working tree." >&2
    exit 2
  }
  mkdir "$work/src"
  (cd "$source_dir" && git ls-files -z --cached --others --exclude-standard | while IFS= read -r -d '' f; do [ -e "$f" ] && printf '%s\0' "$f"; done | tar --null -T - -cf -) | tar -xf - -C "$work/src"
  git -C "$work/src" init -q
  git -C "$work/src" -c core.excludesFile=/dev/null add -A
  git -C "$work/src" -c user.name=setup-check -c user.email=setup-check@localhost -c commit.gpgsign=false commit -q --no-verify -m "working tree of $source_dir"
else
  git clone -q --no-local "$root" "$work/src"
fi
chmod 755 "$work"
chmod -R a+rX "$work/src"

endpoint="$(docker context inspect --format '{{.Endpoints.docker.Host}}')"
case "$endpoint" in
  unix://*) ;;
  *) fail "the Docker endpoint '$endpoint' is not a unix socket" ;;
esac
# The host network lets the Testcontainers fixtures reach the ports their containers publish on the
# host's loopback. The socket's group is read inside the container and given to `dev` below.
docker_args=(-d --name "$container" -v "$work/src:/src:ro" -e GITHUB_TOKEN
  -v "${endpoint#unix://}:/var/run/docker.sock" --network host -e TESTCONTAINERS_HOST_OVERRIDE=localhost)
if [ -n "$token_src" ]; then docker_args+=(-v "$token_src:/run/secrets/op-token:ro"); fi
docker run "${docker_args[@]}" wattwise-setup-check sleep infinity > /dev/null

# Prep, as container root: let git read the read-only mount, give `dev` the socket, then clone as `dev`.
x_root git config --system --add safe.directory '*'
x_root bash -c "mkdir -p '$repo' && chown dev:dev '$repo'"
# `dev` joins the group that owns the mounted socket (its gid depends on the host and on rootless
# Docker's id mapping). This comes before any `docker exec -u dev`, which reads /etc/group.
sock_gid="$(x_root stat -c %g /var/run/docker.sock)"
sock_group="$(x_root getent group "$sock_gid" | cut -d: -f1 || true)"
if [ -z "$sock_group" ]; then
  sock_group=docker-host
  x_root groupadd -g "$sock_gid" "$sock_group"
fi
x_root usermod -aG "$sock_group" dev
x_dev docker version --format '{{.Server.Version}}' > /dev/null || fail "dev can't reach the Docker socket"
pass "dev reaches the host's Docker socket"
x_dev git clone -q /src "$repo"
x_dev bash -c 'grep -E "^PRETTY_NAME=" /etc/os-release'
pass "setup runs on the matrix image ${base_image:-fedora:latest}"
dev_run 'mise trust --quiet' > /dev/null
if [ -n "$token_src" ]; then
  # A token file with looser modes than setup wants, so the first run has to tighten them. It is
  # built inside the container; the token never reaches an argument list.
  x_root bash -c "mkdir -p '$config_dir' && printf 'OP_SERVICE_ACCOUNT_TOKEN=%s\n' \"\$(tr -d '[:space:]' < /run/secrets/op-token)\" > '$token_file' && chmod 755 '$config_dir' && chmod 644 '$token_file' && chown -R dev:dev '$dev_home/.config'"
  expect_eq "the prepared token file starts with loose modes" "644 755" "$(x_root stat -c '%a' "$token_file") $(x_root stat -c '%a' "$config_dir")"
fi

# --- After git clone and `mise run setup` the tools and the checks work ---------------------------

bashrc_before="$(x_dev sha256sum "$dev_home/.bashrc")"
if [ "$with_token" = false ]; then
  expect_setup_ok "setup --skip-secrets (first run)" "$work/run1.log" --skip-secrets
else
  expect_setup_ok "setup with the token file (first run)" "$work/run1.log"
  grep -qx 'setup: token: changed' "$work/run1.log" || fail "the first run didn't report the token as changed (tightened)"
  pass "the first run tightens the loose token file modes"
fi
grep -qx 'setup: tools: changed' "$work/run1.log" || fail "the first run didn't report the tools as changed"
pass "the first run installs the tools from mise.toml"
if x_dev grep -qx 'ID=fedora' /etc/os-release; then
  # The image has no ICU: the Fedora profile installs it on the first run.
  grep -qx 'setup: fedora-packages: changed' "$work/run1.log" || fail "the first run didn't report fedora-packages as changed"
  pass "the Fedora profile installs the missing ICU package on the first run"
fi
expect_eq "setup leaves ~/.bashrc as it was" "$bashrc_before" "$(x_dev sha256sum "$dev_home/.bashrc")"
snap1="$(snapshot)" || fail "the snapshot failed: $snap1"
[ -z "$(x_dev git -C "$repo" status --porcelain)" ] || fail "the clone is dirty after the first run"
pass "git status is clean after the first run"

# --- Rerunning setup changes nothing ------------------------------------------------------------

if [ "$with_token" = false ]; then
  expect_setup_ok "setup --skip-secrets (second run)" "$work/run2.log" --skip-secrets
else
  expect_setup_ok "setup with the token file (second run)" "$work/run2.log"
fi
expect_unchanged "the second run" "$work/run2.log"
if x_dev grep -qx 'ID=fedora' /etc/os-release; then
  grep -qx 'setup: fedora-packages: unchanged' "$work/run2.log" || fail "the second run didn't report fedora-packages as unchanged"
  pass "the Fedora profile reports unchanged on the rerun"
fi
snap2="$(snapshot)" || fail "the snapshot failed: $snap2"
if [ "$snap1" != "$snap2" ]; then
  diff <(echo "$snap1") <(echo "$snap2") | head -20 || true
  fail "the second run changed the bashrc, the clone, the packages, the tools, the config folder or deploy/.env"
fi
pass "the second run leaves ~/.bashrc, git status, packages, mise tools, the config folder and deploy/.env as they were"

for tool in node pnpm dotnet betterleaks zizmor shellcheck op; do
  dev_run "$tool --version" > /dev/null 2>&1 || fail "'$tool --version' fails in a new shell"
done
pass "node, pnpm, dotnet, betterleaks, zizmor, shellcheck and op run in a new shell"
expect_eq "the Husky hooks are installed" ".husky/_" "$(x_dev git -C "$repo" config core.hooksPath)"

# The hook really runs: an empty commit in a new shell goes through pre-commit (betterleaks, then
# lint-staged). The commit is undone afterwards.
rc=0
out="$(dev_run 'git -c user.name=setup-check -c user.email=setup-check@localhost -c commit.gpgsign=false commit --allow-empty -m check' 2>&1)" || rc=$?
expect_eq "an empty commit passes the pre-commit hook" 0 "$rc"
case "$out" in *[Ll]int-staged* | *"No staged files"*) ;; *) echo "$out" >&2; fail "the pre-commit hook's lint-staged step didn't run" ;; esac
pass "the pre-commit hook ran (lint-staged output seen)"
x_dev git -C "$repo" reset -q --hard HEAD~1
expect_eq "the clone is clean after undoing the commit" "$snap2" "$(snapshot)"

# setup refuses to run as root and changes nothing in root's home. Root runs dev's node: it has no mise.
node_path="$(dev_run 'mise which node')"
root_state() { x_root bash -c 'find /root -xdev -printf "%p %m %s\n" | sort | sha256sum'; }
root_before="$(root_state)"
rc=0
out="$(docker exec -u 0 -e HOME=/root -w "$repo" "$container" "$node_path" tools/setup/setup.ts < /dev/null 2>&1)" || rc=$?
[ "$rc" -ne 0 ] || fail "setup exited 0 as root"
case "$out" in *"your own user"*) ;; *) fail "setup's message as root doesn't say 'your own user'" ;; esac
expect_eq "setup as root stops and leaves root's home as it was" "$root_before" "$(root_state)"

# --- The headless token ---------------------------------------------------------------------------

if [ "$with_token" = false ]; then
  x_dev test ! -e "$token_file" || fail "--skip-secrets created a token file"
  pass "--skip-secrets creates no token file"
  x_dev test ! -e "$repo/deploy/.env" || fail "--skip-secrets rendered deploy/.env"
  pass "--skip-secrets renders no deploy/.env"

  # Without a token file and without a terminal, setup stops and names the file; it never hangs.
  rc=0
  out="$(x_dev timeout 300 bash -lc 'mise run setup' < /dev/null 2>&1)" || rc=$?
  [ "$rc" -ne 0 ] || fail "setup without a token file and without a terminal exited 0"
  [ "$rc" -ne 124 ] || fail "setup without a terminal hung until the timeout"
  case "$out" in *"watt-wise/op.env"*) ;; *) echo "$out" >&2; fail "the no-terminal message doesn't name the token file" ;; esac
  pass "setup without a token file and without a terminal stops and names the file (exit $rc)"
  x_dev test ! -e "$token_file" || fail "the no-terminal run created a token file"
  expect_eq "the no-terminal run leaves ~/.bashrc as it was" "$bashrc_before" "$(x_dev sha256sum "$dev_home/.bashrc")"
else
  expect_eq "the token file is mode 600 owned by dev, in a mode 700 folder owned by dev" "600 dev 700 dev" \
    "$(x_dev stat -c '%a %U' "$token_file") $(x_dev stat -c '%a %U' "$config_dir")"

  # deploy/.env: every key of the template, no unresolved reference.
  keys="$(grep -oE '^[A-Z0-9_]+=' "$work/src/deploy/development/compose.settings.tpl" | tr -d '=')"
  [ -n "$keys" ] || fail "no keys in the template"
  for key in $keys; do
    x_dev grep -q "^$key=" "$repo/deploy/.env" || fail "deploy/.env has no $key"
  done
  pass "deploy/.env has every key of the template"
  # The derived keys are not in the template: setup appends them.
  for app in API JOBS FRONTEND ADMIN; do
    for suffix in BIND_ADDRESS PORT; do
      expect_eq "deploy/.env has ${app}_${suffix} exactly once" 1 "$(x_dev grep -c "^${app}_${suffix}=" "$repo/deploy/.env")"
    done
    bind="$(x_dev sed -n "s/^${app}_BIND_ADDRESS=\"\(.*\)\"\$/\1/p" "$repo/deploy/.env")"
    case "$bind" in 127.0.0.1 | ::1) ;; *) fail "deploy/.env's ${app}_BIND_ADDRESS is not a loopback address" ;; esac
    # Each rendered URL, fed through setup's own function, gives the two lines the file holds.
    rendered="$(x_dev sed -n "s/^${app}_URL=\"\(.*\)\"\$/\1/p" "$repo/deploy/.env")"
    expect_eq "${app}_URL derives the ${app}_BIND_ADDRESS and ${app}_PORT lines of deploy/.env" \
      "$(x_dev grep -E "^${app}_(BIND_ADDRESS|PORT)=" "$repo/deploy/.env")" "$(cli --derive-url "$app" "$rendered" 2> /dev/null)"
  done
  for key in POSTGRES_BIND_ADDRESS GRAFANA_BIND_ADDRESS OTLP_BIND_ADDRESS MAILPIT_SMTP_BIND_ADDRESS MAILPIT_BIND_ADDRESS; do
    bind="$(x_dev sed -n "s/^${key}=\"\(.*\)\"\$/\1/p" "$repo/deploy/.env")"
    [ -n "$bind" ] || fail "deploy/.env has no $key"
    cli --check-bind "$key" "$bind" > /dev/null 2>&1 || fail "deploy/.env's $key is not a loopback address"
  done
  pass "deploy/.env has the five infrastructure bind addresses, each on a loopback address"
  if x_dev grep -q '{{' "$repo/deploy/.env"; then fail "deploy/.env still has an unresolved reference"; fi
  pass "deploy/.env has no unresolved reference"

  # The service account reads one vault and fails on any other.
  vaults="$(dev_run 'op vault list --format json')"
  jq -e 'length == 1 and .[0].name == "Watt Wise Development"' <<< "$vaults" > /dev/null || fail "the service account lists other vaults than 'Watt Wise Development'"
  pass "op vault list shows exactly the Watt Wise Development vault"
  dev_run 'op item list --vault "Watt Wise Development" > /dev/null' || fail "the service account can't list the Watt Wise Development vault"
  dev_run 'op read "op://Watt Wise Development/postgres-admin/username" > /dev/null' || fail "the service account can't read an item of the Watt Wise Development vault"
  pass "the service account reads the Watt Wise Development vault"
  if dev_run 'op item list --vault "Delegated signing" > /dev/null 2>&1'; then fail "the service account lists another vault"; fi
  pass "the service account fails to list the Delegated signing vault"
fi

# --- The command line: --derive-url and --check-bind --------------------------------------------
# The tables are node:test cases in tools/setup (`node --test tools/`); these rows only prove the
# options work end to end in the container.

out="$(cli --derive-url API "http://localhost:5062" 2> /dev/null)"
expect_eq "--derive-url derives a localhost URL" "API_BIND_ADDRESS=\"127.0.0.1\"
API_PORT=\"5062\"" "$out"
out="$(cli --derive-url ADMIN "http://[::1]:5174" 2> /dev/null)"
expect_eq "--derive-url derives an IPv6 loopback URL under the app's own name" "ADMIN_BIND_ADDRESS=\"::1\"
ADMIN_PORT=\"5174\"" "$out"
rc=0
out="$(cli --derive-url FRONTEND "https://localhost:5062" 2> /dev/null)" || rc=$?
[ "$rc" -ne 0 ] || fail "--derive-url accepted https"
expect_eq "--derive-url prints nothing on stdout for https" "" "$out"
case "$(cli --derive-url FRONTEND "https://localhost:5062" 2>&1 > /dev/null)" in *FRONTEND_URL*"'frontend' item"*) ;; *) fail "--derive-url's message doesn't name FRONTEND_URL and the 'frontend' item" ;; esac
pass "--derive-url rejects https, naming the key and the 1Password item"
rc=0
cli --derive-url API "http://example.com:5062" > /dev/null 2>&1 || rc=$?
[ "$rc" -ne 0 ] || fail "--derive-url accepted a public host"
rc=0
cli --derive-url API "http://localhost:0" > /dev/null 2>&1 || rc=$?
[ "$rc" -ne 0 ] || fail "--derive-url accepted port 0"
pass "--derive-url rejects a public host and port 0"
rc=0
cli --derive-url API > /dev/null 2>&1 || rc=$?
[ "$rc" -ne 0 ] || fail "--derive-url with one argument exited 0"
rc=0
cli --derive-url BOGUS "http://localhost:5062" > /dev/null 2>&1 || rc=$?
[ "$rc" -ne 0 ] || fail "--derive-url accepted an unknown app"
pass "--derive-url stops on a missing argument and on an unknown app"

for address in 127.0.0.1 127.255.255.255 ::1; do
  out="$(cli --check-bind POSTGRES_BIND_ADDRESS "$address" 2>&1)" || fail "--check-bind rejected $address"
  [ -z "$out" ] || fail "--check-bind $address printed: $out"
done
pass "--check-bind accepts a loopback address and prints nothing"
for address in 0.0.0.0 :: 192.0.2.10 localhost 127.0.0.01; do
  rc=0
  out="$(cli --check-bind MAILPIT_BIND_ADDRESS "$address" 2>&1)" || rc=$?
  [ "$rc" -ne 0 ] || fail "--check-bind accepted '$address'"
  case "$out" in *MAILPIT_BIND_ADDRESS*"'mailpit' item"*"not a loopback address"*) ;; *) fail "--check-bind's message for '$address' is not the expected one: $out" ;; esac
done
pass "--check-bind rejects a non-loopback address, naming the key and the '1Password' item"
rc=0
cli --check-bind BOGUS_BIND_ADDRESS 127.0.0.1 > /dev/null 2>&1 || rc=$?
[ "$rc" -ne 0 ] || fail "--check-bind accepted an unknown key"
pass "--check-bind refuses an unknown key"

# --- The development.md checks ------------------------------------------------------------------

# `mise run check` is the eight checks. Testcontainers runs for real on the mounted socket.
checks_log="$work/checks.log"
rc=0
dev_run 'mise run check' < /dev/null > "$checks_log" 2>&1 || rc=$?
if [ "$rc" -ne 0 ]; then
  cp "$checks_log" "${TMPDIR:-/tmp}/check-setup-checks.log"
  tail -n 100 "$checks_log" >&2
  fail "mise run check failed (exit $rc); the log is ${TMPDIR:-/tmp}/check-setup-checks.log"
fi
if grep -nF '(!)' "$checks_log"; then fail "pnpm build printed a (!) warning (lines above)"; fi
pass "the development.md checks pass on the set-up machine"

echo "check-setup: $passed checks passed in ${SECONDS}s"
