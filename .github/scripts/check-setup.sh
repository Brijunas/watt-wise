#!/usr/bin/env bash
# Asserts the behaviour of ./setup (docs/setup.md, "Checking setup") on a throwaway Fedora container
# that has only git and the Docker CLI. Run from anywhere with Docker running:
#   bash .github/scripts/check-setup.sh                         CI mode: setup --skip-secrets, no token
#   bash .github/scripts/check-setup.sh --token-file <file>     also the secrets steps, with that token
#   bash .github/scripts/check-setup.sh --token-file <file> --checks   also the development.md checks
#   bash .github/scripts/check-setup.sh --interactive           the token is typed at setup's prompt
# It tests the committed HEAD of the repository this script is in (a clone), not the working tree.
# The token file is mounted read-only into the container; no value is ever printed.
set -euo pipefail

usage() {
  echo "usage: check-setup.sh [--token-file <file> [--checks] | --interactive [--checks]]" >&2
  exit 2
}

token_src=""
run_checks=false
interactive=false
while [ "$#" -gt 0 ]; do
  case "$1" in
    --token-file)
      [ "$#" -ge 2 ] || usage
      token_src="$2"
      shift 2
      ;;
    --checks)
      run_checks=true
      shift
      ;;
    --interactive)
      interactive=true
      shift
      ;;
    *) usage ;;
  esac
done
if [ -n "$token_src" ] && [ "$interactive" = true ]; then usage; fi
if [ "$run_checks" = true ] && [ -z "$token_src" ] && [ "$interactive" = false ]; then usage; fi
if [ -n "$token_src" ]; then
  [ -r "$token_src" ] || {
    echo "The token file is not readable." >&2
    exit 2
  }
  token_src="$(realpath "$token_src")"
fi
with_token=false
if [ -n "$token_src" ] || [ "$interactive" = true ]; then with_token=true; fi
if [ "$with_token" = true ]; then command -v jq > /dev/null || {
  echo "jq is required." >&2
  exit 2
}; fi

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

# The setup check's home: everything runs as `dev`, prep as the container's root.
dev_home=/home/dev
repo="$dev_home/watt-wise"
token_dir="$dev_home/.config/watt-wise"
token_file="$token_dir/op-service-account-token"
x_root() { docker exec "$container" "$@"; }
x_dev() { docker exec -u dev -e HOME="$dev_home" -w "$dev_home" "$container" "$@"; }
# A shell for `dev` that reads ~/.bashrc, like a new terminal. Its job-control warnings go to stderr.
dev_shell() { x_dev bash -ic "cd $repo && $1" 2> /dev/null; }

run_setup() { # log file, setup arguments; prints the output and returns setup's exit code
  local log="$1"
  shift
  x_dev bash -c 'cd ~/watt-wise && ./setup "$@"' setup "$@" < /dev/null 2>&1 | tee "$log"
  return "${PIPESTATUS[0]}"
}
expect_setup_ok() { # description, log file, setup arguments
  local description="$1" log="$2" rc=0
  shift 2
  run_setup "$log" "$@" || rc=$?
  expect_eq "$description exits 0" 0 "$rc"
}
expect_no_change() { # description, log file
  if grep -E '^setup: .*: changed' "$2"; then fail "$1: a step reported 'changed' (lines above)"; fi
  pass "$1 reports no 'changed'"
}

snapshot() { # prints what a rerun must leave as it was
  # shellcheck disable=SC2016
  x_dev bash -c '
    cd ~/watt-wise
    echo "bashrc: $(sha256sum < ~/.bashrc)"
    echo "git status:"; git status --porcelain
    echo "rpm:"; rpm -qa | sort
    echo "mise:"; mise ls --installed
    echo "repos:"; sha256sum /etc/yum.repos.d/* | sort
    if [ -f deploy/.env ]; then echo "deploy/.env: $(sha256sum < deploy/.env)"; fi
    echo "bashrc.d:"; sha256sum ~/.bashrc.d/* 2>/dev/null
    echo "husky:"; find .husky/_ -type f 2>/dev/null | sort | xargs -r sha256sum
    # The token is only hashed twice over, so a difference shows without printing anything useful.
    tf=~/.config/watt-wise/op-service-account-token
    if [ -e "$tf" ]; then echo "token: $(stat -c "%a %U" "$tf") $(sha256sum < "$tf" | sha256sum)"; fi
    if [ -e ~/.config/watt-wise ]; then echo "token folder: $(stat -c "%a %U" ~/.config/watt-wise)"; fi
  ' 2>&1
}

echo "Building the setup-check image"
docker build -q -t wattwise-setup-check - < "$root/.github/setup-check/Dockerfile" > /dev/null

# The committed HEAD, as a clone: this worktree's .git can be a pointer file.
git clone -q --no-local "$root" "$work/src"
chmod 755 "$work"
chmod -R a+rX "$work/src"

docker_args=(-d --name "$container" -v "$work/src:/src:ro" -e GITHUB_TOKEN)
if [ -n "$token_src" ]; then docker_args+=(-v "$token_src:/run/secrets/op-token:ro"); fi
if [ "$run_checks" = true ]; then
  endpoint="$(docker context inspect --format '{{.Endpoints.docker.Host}}')"
  case "$endpoint" in
    unix://*) ;;
    *) fail "the Docker endpoint '$endpoint' is not a unix socket" ;;
  esac
  # The host network lets the Testcontainers fixtures reach the ports their containers publish on
  # the host's loopback. The socket's group is read inside the container and given to `dev` below.
  docker_args+=(-v "${endpoint#unix://}:/var/run/docker.sock" --network host -e TESTCONTAINERS_HOST_OVERRIDE=localhost)
fi
docker run "${docker_args[@]}" wattwise-setup-check sleep infinity > /dev/null

# Prep, as container root: let git read the read-only mount, then clone it as `dev`.
x_root git config --system --add safe.directory '*'
if [ "$run_checks" = true ]; then
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
fi
x_dev git clone -q /src "$repo"
if [ -n "$token_src" ]; then
  # Looser modes than setup wants, so the first run has to tighten them. The copy happens inside
  # the container; the token never reaches an argument list.
  x_root bash -c "mkdir -p '$token_dir' && cp /run/secrets/op-token '$token_file' && chmod 755 '$token_dir' && chmod 644 '$token_file' && chown -R dev:dev '$dev_home/.config'"
  expect_eq "the prepared token file starts with loose modes" "644 755" "$(x_root stat -c '%a' "$token_file") $(x_root stat -c '%a' "$token_dir")"
fi

# --- Done item 1: after git clone and ./setup the tools and the checks work --------------------

if [ "$with_token" = false ]; then
  expect_setup_ok "setup --skip-secrets (first run)" "$work/run1.log" --skip-secrets
  grep -qx 'setup: mode: headless' "$work/run1.log" || fail "the first run didn't print 'mode: headless'"
  pass "a machine without the desktop app runs headless"
elif [ "$interactive" = true ]; then
  echo "setup will ask for the token; paste it at the hidden prompt."
  rc=0
  docker exec -it -u dev -e HOME="$dev_home" -w "$repo" "$container" ./setup || rc=$?
  expect_eq "setup with a terminal prompts for the token and exits 0" 0 "$rc"
else
  expect_setup_ok "setup (first run)" "$work/run1.log"
  grep -qx 'setup: token: changed' "$work/run1.log" || fail "the first run didn't report the token as changed (tightened)"
  pass "the first run tightens the loose token file modes"
fi
snap1="$(snapshot)"
[ -z "$(x_dev git -C "$repo" status --porcelain)" ] || fail "the clone is dirty after the first run"
pass "git status is clean after the first run"

# --- Done item 2: rerunning ./setup changes nothing ---------------------------------------------

if [ "$with_token" = false ]; then
  expect_setup_ok "setup --skip-secrets (second run)" "$work/run2.log" --skip-secrets
else
  expect_setup_ok "setup (second run)" "$work/run2.log"
fi
expect_no_change "the second run" "$work/run2.log"
snap2="$(snapshot)"
if [ "$snap1" != "$snap2" ]; then
  diff <(echo "$snap1") <(echo "$snap2") | head -20 || true
  fail "the second run changed the bashrc, the clone, the packages, the tools, the repo files or deploy/.env"
fi
pass "the second run leaves ~/.bashrc, git status, rpm packages, mise tools, yum repos and deploy/.env as they were"

expect_eq "bashrc has exactly one mise activation" 1 "$(x_dev grep -c 'mise activate' "$dev_home/.bashrc")"
for tool in node pnpm dotnet betterleaks zizmor op; do
  dev_shell "$tool --version" > /dev/null || fail "'$tool --version' fails in a new shell"
done
pass "node, pnpm, dotnet, betterleaks, zizmor and op run in a new shell"
expect_eq "the Husky hooks are installed" ".husky/_" "$(x_dev git -C "$repo" config core.hooksPath)"

# The hook really runs: an empty commit in a new shell goes through pre-commit (betterleaks, then
# lint-staged). The commit is undone afterwards.
rc=0
out="$(x_dev bash -ic "cd $repo && git -c user.name=setup-check -c user.email=setup-check@localhost -c commit.gpgsign=false commit --allow-empty -m check" 2>&1)" || rc=$?
expect_eq "an empty commit passes the pre-commit hook" 0 "$rc"
case "$out" in *[Ll]int-staged* | *"No staged files"*) ;; *) echo "$out" >&2; fail "the pre-commit hook's lint-staged step didn't run" ;; esac
pass "the pre-commit hook ran (lint-staged output seen)"
x_dev git -C "$repo" reset -q --hard HEAD~1
expect_eq "the clone is clean after undoing the commit" "$snap2" "$(snapshot)"

# setup refuses to run as root and changes nothing in root's home.
root_before="$(x_root sh -c 'cat /root/.bashrc 2>&1 | sha256sum')"
rc=0
out="$(docker exec -u 0 -e HOME=/root "$container" bash "$repo/setup" < /dev/null 2>&1)" || rc=$?
[ "$rc" -ne 0 ] || fail "setup exited 0 as root"
case "$out" in *"your own user"*) ;; *) fail "setup's message as root doesn't say 'your own user'" ;; esac
expect_eq "setup as root stops and leaves /root/.bashrc as it was" "$root_before" "$(x_root sh -c 'cat /root/.bashrc 2>&1 | sha256sum')"

# --- Done item 3: a headless run keeps the token in a mode 600 file, without the desktop app ----

if [ "$with_token" = false ]; then
  expect_eq "--skip-secrets writes no token line to ~/.bashrc" 0 "$(x_dev grep -c OP_SERVICE_ACCOUNT_TOKEN "$dev_home/.bashrc" || true)"
  x_dev test ! -e "$token_file" || fail "--skip-secrets created a token file"
  pass "--skip-secrets creates no token file"

  # Without a token file and without a terminal, setup stops and names the file; it never hangs.
  bashrc_before="$(x_dev sha256sum "$dev_home/.bashrc")"
  rc=0
  out="$(x_dev timeout 120 bash -c 'cd ~/watt-wise && ./setup' < /dev/null 2>&1)" || rc=$?
  [ "$rc" -ne 0 ] || fail "setup without a token file and without a terminal exited 0"
  [ "$rc" -ne 124 ] || fail "setup without a terminal hung until the timeout"
  case "$out" in *".config/watt-wise/op-service-account-token"*) ;; *) fail "the no-terminal message doesn't name the token file" ;; esac
  pass "setup without a token file and without a terminal stops and names the file (exit $rc)"
  x_dev test ! -e "$token_file" || fail "the no-terminal run created a token file"
  expect_eq "the no-terminal run leaves ~/.bashrc as it was" "$bashrc_before" "$(x_dev sha256sum "$dev_home/.bashrc")"

  # mise activated from a file under ~/.bashrc.d (as on vidar) counts: a rerun adds no line.
  # shellcheck disable=SC2016
  x_dev bash -c 'mkdir -p ~/.bashrc.d && grep -F "mise activate" ~/.bashrc > ~/.bashrc.d/mise.sh && grep -vF "mise activate" ~/.bashrc > ~/.bashrc.new && mv ~/.bashrc.new ~/.bashrc'
  expect_setup_ok "setup --skip-secrets with mise activated under ~/.bashrc.d" "$work/run3.log" --skip-secrets
  expect_no_change "the run with a ~/.bashrc.d activation" "$work/run3.log"
  expect_eq "bashrc gets no mise line when ~/.bashrc.d activates mise" 0 "$(x_dev grep -c 'mise activate' "$dev_home/.bashrc" || true)"
else
  expect_eq "the token file is mode 600 owned by dev, in a mode 700 folder owned by dev" "600 dev 700 dev" \
    "$(x_dev stat -c '%a %U' "$token_file") $(x_dev stat -c '%a %U' "$token_dir")"
  # shellcheck disable=SC2016
  expected_line="[ -r \"$token_file\" ] && export OP_SERVICE_ACCOUNT_TOKEN=\"\$(cat \"$token_file\")\""
  expect_eq "bashrc has the token line exactly once" 1 "$(x_dev grep -cxF -- "$expected_line" "$dev_home/.bashrc")"
  expect_eq "bashrc has no other mention of the token" 1 "$(x_dev grep -c OP_SERVICE_ACCOUNT_TOKEN "$dev_home/.bashrc")"
  x_dev bash -c 'ls /opt/1Password/1password > /dev/null 2>&1 || command -v 1password > /dev/null 2>&1' && fail "the 1Password desktop app is installed in the container"
  pass "the 1Password desktop app is not installed"

  # deploy/.env: every key of the template, no unresolved reference.
  keys="$(grep -oE '^[A-Z0-9_]+=' "$work/src/deploy/development/compose.settings.tpl" | tr -d '=')"
  [ -n "$keys" ] || fail "no keys in the template"
  for key in $keys; do
    x_dev grep -q "^$key=" "$repo/deploy/.env" || fail "deploy/.env has no $key"
  done
  pass "deploy/.env has every key of the template"
  if x_dev grep -q '{{' "$repo/deploy/.env"; then fail "deploy/.env still has an unresolved reference"; fi
  pass "deploy/.env has no unresolved reference"

  # --- Done item 4: the service account reads one vault and fails on any other ----------------
  vaults="$(dev_shell 'op vault list --format json')"
  jq -e 'length == 1 and .[0].name == "Watt Wise Development"' <<< "$vaults" > /dev/null || fail "the service account lists other vaults than 'Watt Wise Development'"
  pass "op vault list shows exactly the Watt Wise Development vault"
  dev_shell 'op item list --vault "Watt Wise Development" > /dev/null' || fail "the service account can't list the Watt Wise Development vault"
  dev_shell 'op read "op://Watt Wise Development/postgres-admin/username" > /dev/null' || fail "the service account can't read an item of the Watt Wise Development vault"
  pass "the service account reads the Watt Wise Development vault"
  # The Delegated signing vault exists in this account and the service account has no access to it.
  # The length-1 `op vault list` above is the main proof; this is a second look at one real vault.
  if dev_shell 'op item list --vault "Delegated signing" > /dev/null'; then fail "the service account lists another vault"; fi
  pass "the service account fails to list the Delegated signing vault"
fi

# --- Fedora only --------------------------------------------------------------------------------

rc=0
out="$(docker run --rm -u 1000 -v "$work/src:/src:ro" debian:trixie-slim bash /src/setup 2>&1 < /dev/null)" || rc=$?
[ "$rc" -ne 0 ] || fail "setup exited 0 on Debian"
case "$out" in *"Fedora"*) ;; *) fail "setup's message on Debian doesn't say Fedora" ;; esac
pass "setup stops on Debian with the Fedora message (exit $rc)"

# --- The development.md checks ------------------------------------------------------------------

if [ "$run_checks" = true ]; then
  checks_log="$work/checks.log"
  failed=()
  run_check() { # command
    echo "check: $1"
    if ! x_dev bash -ic "cd $repo && $1" >> "$checks_log" 2>&1; then failed+=("$1"); fi
  }
  run_check 'pnpm lint'
  run_check 'pnpm format:check'
  run_check 'pnpm typecheck'
  run_check 'pnpm test'
  run_check 'pnpm build'
  if grep -qF '(!)' "$checks_log"; then failed+=("pnpm build printed a (!) warning"); fi
  run_check 'dotnet build backend/WattWise.slnx -warnaserror'
  run_check 'dotnet format backend/WattWise.slnx --verify-no-changes'
  run_check 'dotnet test --solution backend/WattWise.slnx'
  if [ "${#failed[@]}" -gt 0 ]; then
    cp "$checks_log" "${TMPDIR:-/tmp}/check-setup-checks.log"
    printf 'FAIL: %s\n' "${failed[@]}" >&2
    fail "development.md checks failed; the log is ${TMPDIR:-/tmp}/check-setup-checks.log"
  fi
  pass "the development.md checks pass on the set-up machine"
fi

echo "check-setup: $passed checks passed in ${SECONDS}s"
