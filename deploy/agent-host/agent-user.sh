#!/usr/bin/env bash
#
# Non-sudo setup of the agent user (corvus) on tethys. Run after setup-tethys.sh,
# (docker works through `sg docker -c` while the login lacks the group). Safe to rerun: every step checks first.
#
# Usage: deploy/agent-host/agent-user.sh [--dry-run] [--repo DIR]
#   --dry-run  prints each step and changes nothing.
#   --repo DIR the main checkout to set up (default: $REPO_DIR, else ~/code/personal/watt-wise).
#
# Works on the main checkout, never the one holding the script, so deploy/.env and the Dev
# stack belong to the permanent checkout and not to a worktree. Pulls main (ff-only) first when
# the tree is clean.
#
# Overridable by env: REPO_DIR, WW_CONFIG_DIR (~/.config/wattwise), OP_VAULT ("Watt Wise Development").
#
# Writes no Claude settings. The only file it creates for Claude's environment is
# $WW_CONFIG_DIR/env, which exports OP_SERVICE_ACCOUNT_TOKEN (a credential).

# shellcheck disable=SC2016  # single quotes are deliberate: the text expands later, in the target shell
set -euo pipefail

DRY_RUN=0
REPO_ARG=""
while (( $# > 0 )); do
  arg=$1; shift
  case "$arg" in
    --dry-run) DRY_RUN=1 ;;
    --repo) [[ $# -gt 0 ]] || { echo "--repo needs a directory" >&2; exit 2; }; REPO_ARG=$1; shift ;;
    -h|--help) sed -n '2,17p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done

REPO_DIR="${REPO_ARG:-${REPO_DIR:-$HOME/code/personal/watt-wise}}"
WW_CONFIG_DIR="${WW_CONFIG_DIR:-$HOME/.config/wattwise}"
OP_TOKEN_FILE="$WW_CONFIG_DIR/op-token"
ENV_FILE="$WW_CONFIG_DIR/env"
OP_VAULT="${OP_VAULT:-Watt Wise Development}"
COMPOSE_FILE=deploy/docker-compose.development.yml
USE_SG=0   # 1 when docker only works through `sg docker -c` (group added, not yet in this login)
CMD=()
BASHRC="$HOME/.bashrc"
BASHRC_LINE='[ -f "$HOME/.config/wattwise/env" ] && . "$HOME/.config/wattwise/env" # wattwise'

log()  { printf '\n== %s\n' "$*"; }
ok()   { printf '   ok: %s\n' "$*"; }
die()  { printf 'error: %s\n' "$*" >&2; exit 1; }
# run CMD... runs the command, or only prints it in a dry run.
run()  { if (( DRY_RUN )); then printf '   dry-run: %s\n' "$*"; else "$@"; fi; }

# compose_cmd ARGS...: sets CMD to the compose command line, wrapped in `sg docker -c` when needed.
compose_cmd() {
  local base=(docker compose -f "$COMPOSE_FILE" "$@")
  if (( USE_SG )); then CMD=(sg docker -c "$(printf '%q ' "${base[@]}")"); else CMD=("${base[@]}"); fi
}

# Everything below runs in the repo, with mise's tools on PATH.
[[ -e "$REPO_DIR/.git" ]] || die "$REPO_DIR is not a git checkout; set REPO_DIR or pass --repo."
cd "$REPO_DIR"
export PATH="$HOME/.local/bin:$PATH"

log "Main checkout $REPO_DIR"
if [[ -n "$(git -C "$REPO_DIR" status --porcelain)" ]]; then
  echo "   warning: the tree is not clean; not pulling main"
elif [[ "$(git -C "$REPO_DIR" branch --show-current)" != main ]]; then
  echo "   warning: not on main; not pulling"
else
  run git -C "$REPO_DIR" pull --ff-only
fi

log "Git remote and credential helper"
# Pushes go over HTTPS with the fine-grained token (no Workflows permission), so GitHub rejects
# changes to .github/workflows server-side. An SSH remote would bypass the token's scope.
# The user name in the URL keeps it from matching an `insteadOf = https://github.com/` rewrite
# to an SSH alias (corvus's ~/code/personal/.gitconfig has one), which would undo this.
HTTPS_URL=https://Brijunas@github.com/Brijunas/watt-wise.git
if [[ "$(git -C "$REPO_DIR" remote get-url origin 2>/dev/null)" == "$HTTPS_URL" ]]; then
  ok "origin is already $HTTPS_URL"
else
  run git -C "$REPO_DIR" remote set-url origin "$HTTPS_URL"
fi
if (( DRY_RUN )); then
  echo "   dry-run: would run 'gh auth setup-git'"
elif gh auth status >/dev/null 2>&1; then
  gh auth setup-git
  ok "git uses gh's token for github.com (commit signing stays SSH)"
else
  echo "   warning: gh is not signed in; run setup-tethys.sh, then rerun this script"
fi

log "Preconditions"
[[ -s "$OP_TOKEN_FILE" ]] || die "$OP_TOKEN_FILE is missing; run setup-tethys.sh first."
[[ "$(stat -c %a "$OP_TOKEN_FILE")" == "600" ]] || die "$OP_TOKEN_FILE must be mode 600."
command -v docker >/dev/null 2>&1 || die "docker is not installed; run setup-tethys.sh first."
if ! docker info >/dev/null 2>&1; then
  if id -nG | tr ' ' '\n' | grep -qx docker || id -nG "$(id -un)" | tr ' ' '\n' | grep -qx docker; then
    if sg docker -c 'docker info' >/dev/null 2>&1; then
      USE_SG=1
      echo "   docker works through 'sg docker -c' (this login lacks the docker group); using that"
    elif (( DRY_RUN )); then echo "   dry-run: docker is not usable from this shell"
    else die "docker is not usable, not even through 'sg docker -c'. Is the docker service running?"; fi
  elif (( DRY_RUN )); then echo "   dry-run: docker is not usable and the user is not in the docker group"
  else die "the user is not in the docker group; run setup-tethys.sh first."; fi
fi
command -v op >/dev/null 2>&1 || die "op (1Password CLI) is not installed; run setup-tethys.sh first."
ok "token file, docker and op are in place"

log "Environment file $ENV_FILE"
desired='export OP_SERVICE_ACCOUNT_TOKEN="$(cat "$HOME/.config/wattwise/op-token")"'
if [[ -f "$ENV_FILE" ]] && [[ "$(cat "$ENV_FILE")" == "$desired" ]]; then
  ok "already written"
elif (( DRY_RUN )); then
  echo "   dry-run: would write $ENV_FILE"
else
  (umask 077 && printf '%s\n' "$desired" > "$ENV_FILE")
  ok "wrote $ENV_FILE"
fi
if grep -qsF '# wattwise' "$BASHRC"; then
  ok "$BASHRC already sources it"
elif (( DRY_RUN )); then
  echo "   dry-run: would append the source line to $BASHRC"
else
  printf '\n%s\n' "$BASHRC_LINE" >> "$BASHRC"
  ok "added the source line to $BASHRC"
fi
# shellcheck disable=SC1090
[[ -f "$ENV_FILE" ]] && . "$ENV_FILE"
if (( DRY_RUN )); then
  echo "   dry-run: would check 'op whoami'"
else
  op whoami >/dev/null 2>&1 || die "op whoami failed with the stored token."
  ok "op whoami works"
fi

log "mise and the toolchain"
if command -v mise >/dev/null 2>&1; then
  ok "mise: $(mise --version)"
else
  # https://mise.jdx.dev/getting-started.html
  if (( DRY_RUN )); then echo "   dry-run: curl -fsSL https://mise.run | sh"
  else curl -fsSL https://mise.run | sh; fi
fi
if ! grep -qs 'mise activate' "$BASHRC"; then
  if (( DRY_RUN )); then echo "   dry-run: would add 'eval \"\$(mise activate bash)\"' to $BASHRC"
  else printf '%s\n' 'eval "$("$HOME/.local/bin/mise" activate bash)"' >> "$BASHRC"; fi
fi
if ! command -v mise >/dev/null 2>&1 && (( DRY_RUN )); then
  echo "   dry-run: mise is not installed here; skipping the mise steps"
else
  run mise trust "$REPO_DIR/mise.toml"
  run mise install
  run mise exec -- pnpm install --frozen-lockfile
  run mise exec -- dotnet tool restore
fi

log "Development stack settings (deploy/.env)"
run op inject -f -i deploy/development/compose.settings.tpl -o deploy/.env

log "Compose stack"
if (( DRY_RUN )); then
  compose_cmd up -d --wait
  echo "   dry-run: first start via 'op run --env-file deploy/development/compose.env -- ${CMD[*]}'"
  echo "   dry-run: then bootstrap.sql and role passwords from 1Password (first time only)"
else
  # The images read the admin and pgAdmin passwords only to initialize empty volumes,
  # so the first start goes through op run (docs/setup.md step 5).
  compose_cmd up -d --wait
  op run --env-file deploy/development/compose.env -- "${CMD[@]}"
  ok "stack is up"
  roles_missing=1
  compose_cmd exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "SELECT 1 FROM pg_roles WHERE rolname = '"'"'api'"'"'"'
  if "${CMD[@]}" | grep -q 1; then
    roles_missing=0
  fi
  compose_cmd exec -T postgres sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
  "${CMD[@]}" < deploy/postgres/bootstrap.sql >/dev/null
  ok "bootstrap.sql applied (idempotent)"
  if (( roles_missing )); then
    for role in api cli jobs backup reader; do
      pw="$(op read "op://$OP_VAULT/postgres-$role/password")"
      compose_cmd exec -T -e PW="$pw" postgres sh -c 'psql -v ON_ERROR_STOP=1 -v pw="$PW" -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
      printf "ALTER ROLE %s PASSWORD :'pw';\n" "$role" | "${CMD[@]}" >/dev/null
      ok "password set for role $role"
    done
    unset pw
  else
    ok "roles already existed; passwords left alone"
  fi
fi

log "Migrations"
run mise exec -- op run --env-file backend/src/WattWise.Cli/.env.development -- \
  dotnet run --project backend/src/WattWise.Cli -- migrate

log "Done"
echo "   Next: run sync-user-claude.sh from vidar so tethys's Claude matches."
