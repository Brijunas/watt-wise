#!/usr/bin/env bash
#
# Makes the user-level Claude Code config on a remote host match this machine's.
# Run on vidar. Nothing personal is stored in the repo: the script reads vidar's
# files at run time.
#
# Usage: deploy/agent-host/sync-user-claude.sh [--check] [--dry-run] [--host tethys]
#   --check    changes nothing; diffs the normalized user config of both machines
#              (CLAUDE.md hash, marketplaces, plugins, skills, merged settings keys)
#              and exits 1 on any difference.
#   --dry-run  prints what a sync would do, changes nothing.
#   --host H   the ssh host (default: tethys, or $AGENT_HOST).
#
# What it syncs:
#   - ~/.claude/CLAUDE.md, copied verbatim.
#   - extraKnownMarketplaces and enabledPlugins: marketplaces are added and plugins
#     installed, enabled or disabled with the remote `claude plugin` CLI.
#   - Orca's skills (orca-cli, orchestration, orca-computer-use; herdr only if herdr
#     exists remotely), installed with the remote `orca skills install`.
#   - These keys of settings.json, merged with jq: permissions.defaultMode, model,
#     modelSettings, remoteControlAtStartup, agentPushNotifEnabled,
#     inputNeededNotifEnabled, crossSessionInbound, worktree, enabledPlugins,
#     extraKnownMarketplaces, theme, verbose, timeFormat, editorMode. Never hooks,
#     statusLine (Orca manages them per host) or credentials. The remote file is
#     backed up first.
#   - Memory: the remote's canonical ~/.claude/wattwise-memory/ is symlinked as
#     `memory` into every ~/.claude/projects/*watt-wise* there. A non-empty existing
#     memory directory is merged in first and kept as memory.premerge-<time>.
#
# Needs on both machines: jq, sha256sum. On the remote: claude (and orca for skills)
# on PATH or in ~/.local/bin.

# shellcheck disable=SC2016,SC2329  # remote commands are single-quoted on purpose; functions run through mutate
set -euo pipefail

HOST="${AGENT_HOST:-tethys}"
CHECK=0
DRY_RUN=0
while (( $# )); do
  case "$1" in
    --check) CHECK=1 ;;
    --dry-run) DRY_RUN=1 ;;
    --host) HOST="${2:?--host needs a value}"; shift ;;
    -h|--help) sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 2 ;;
  esac
  shift
done

LOCAL_CLAUDE="${LOCAL_CLAUDE_DIR:-$HOME/.claude}"
LOCAL_SETTINGS="$LOCAL_CLAUDE/settings.json"
ORCA_SKILLS=(orca-cli orchestration orca-computer-use)
REMOTE_PATH='$HOME/.local/bin:$HOME/.local/share/mise/shims:$PATH'

# The settings keys that are mirrored. permissions.defaultMode is the only part of
# `permissions` that is.
SUBSET_FILTER='({model, modelSettings, remoteControlAtStartup, agentPushNotifEnabled,
  inputNeededNotifEnabled, crossSessionInbound, worktree, enabledPlugins,
  extraKnownMarketplaces, theme, verbose, timeFormat, editorMode}
  + {permissions: {defaultMode: .permissions.defaultMode}})
  | with_entries(select(.value != null)) | if .permissions.defaultMode == null then del(.permissions) else . end'

log()  { printf '== %s\n' "$*"; }
note() { printf '   %s\n' "$*"; }
warn() { printf '   warning: %s\n' "$*" >&2; WARNINGS=$((WARNINGS + 1)); }
die()  { printf 'error: %s\n' "$*" >&2; exit 1; }
WARNINGS=0

command -v jq >/dev/null 2>&1 || die "jq is required."
[[ -f "$LOCAL_SETTINGS" ]] || die "$LOCAL_SETTINGS not found."

# rsh CMD...: run a command on the remote with the user-level tool paths.
rsh() { ssh -o BatchMode=yes -o ConnectTimeout=10 "$HOST" "export PATH=\"$REMOTE_PATH\"; $*"; }

# mutate DESCRIPTION CMD...: runs a remote-changing command, or only prints it.
mutate() {
  local desc="$1"; shift
  if (( DRY_RUN )); then note "dry-run: $desc"; return 0; fi
  note "$desc"
  "$@"
}

ssh -o BatchMode=yes -o ConnectTimeout=10 "$HOST" true 2>/dev/null || die "cannot reach $HOST over ssh."
rsh 'command -v jq >/dev/null' || die "jq is missing on $HOST (run setup-tethys.sh)."

# The normalized report both machines print. $1 = 1 to leave herdr out of the skill
# list (the remote doesn't have it). Run with `bash -s -- <arg>` on either side.
REPORT_SCRIPT=$(cat <<'REPORT'
c="$HOME/.claude"
s="$c/settings.json"
[ -f "$s" ] || s=/dev/null
echo "CLAUDE.md sha256: $(sha256sum < "$c/CLAUDE.md" 2>/dev/null | cut -d' ' -f1)"
echo "-- marketplaces (settings)"
jq -r '.extraKnownMarketplaces // {} | to_entries[] | "\(.key) \(.value.source | tojson)"' "$s" 2>/dev/null | sort
echo "-- plugins (settings)"
jq -r '.enabledPlugins // {} | to_entries[] | "\(.key)=\(.value)"' "$s" 2>/dev/null | sort
echo "-- plugins (installed, user scope)"
claude plugin list --json 2>/dev/null | jq -r '.[] | select(.scope == "user") | "\(.id) enabled=\(.enabled)"' | sort || true
echo "-- skills (~/.claude/skills; synced/ is managed by claude.ai)"
ls -1 "$c/skills" 2>/dev/null | grep -vx synced | { if [ "${1:-0}" = 1 ]; then grep -vx herdr; else cat; fi; } | sort || true
echo "-- settings keys"
jq -S "$SUBSET" "$s" 2>/dev/null
REPORT
)

local_report() { SUBSET="$SUBSET_FILTER" bash -c "$REPORT_SCRIPT" report "$1"; }
remote_report() {
  # SUBSET is passed on the command line, quoted for the remote shell.
  local q; q=$(printf '%q' "$SUBSET_FILTER")
  ssh -o BatchMode=yes -o ConnectTimeout=10 "$HOST" "export PATH=\"$REMOTE_PATH\" SUBSET=$q; bash -s -- $1" <<<"$REPORT_SCRIPT"
}

remote_has_herdr=0
rsh 'command -v herdr >/dev/null 2>&1' && remote_has_herdr=1 || true

if (( CHECK )); then
  log "Comparing the user config of this machine and $HOST"
  skip_herdr=$(( 1 - remote_has_herdr ))
  if diff -u --label "this machine" --label "$HOST" <(local_report "$skip_herdr") <(remote_report "$skip_herdr"); then
    note "no differences"
    exit 0
  fi
  exit 1
fi

# --- CLAUDE.md --------------------------------------------------------------
copy_claude_md() {
  rsh 'mkdir -p ~/.claude; [ -f ~/.claude/CLAUDE.md ] && cp ~/.claude/CLAUDE.md ~/.claude/CLAUDE.md.bak-$(date +%Y%m%d%H%M%S) || true'
  ssh -o BatchMode=yes "$HOST" 'cat > ~/.claude/CLAUDE.md' < "$LOCAL_CLAUDE/CLAUDE.md"
}

log "CLAUDE.md"
if [[ -f "$LOCAL_CLAUDE/CLAUDE.md" ]]; then
  local_hash=$(sha256sum < "$LOCAL_CLAUDE/CLAUDE.md" | cut -d' ' -f1)
  remote_hash=$(rsh '[ -f "$HOME/.claude/CLAUDE.md" ] && sha256sum < "$HOME/.claude/CLAUDE.md" | cut -d" " -f1 || true')
  if [[ "$local_hash" == "$remote_hash" ]]; then
    note "already identical"
  else
    mutate "copy CLAUDE.md to $HOST (the old one is kept as CLAUDE.md.bak-<time>)" copy_claude_md
  fi
else
  warn "no local CLAUDE.md to copy"
fi

# --- Marketplaces and plugins -----------------------------------------------
if rsh 'command -v claude >/dev/null 2>&1'; then
  log "Marketplaces"
  remote_markets=$(rsh 'claude plugin marketplace list --json 2>/dev/null' | jq -r '.[].name' || true)
  while IFS=$'\t' read -r name src; do
    [[ -n "$name" ]] || continue
    if grep -qx "$name" <<<"$remote_markets"; then note "$name: present"; continue; fi
    mutate "add marketplace $name ($src)" rsh "claude plugin marketplace add '$src'" || warn "could not add marketplace $name"
  done < <(jq -r '.extraKnownMarketplaces // {} | to_entries[]
    | [.key, (.value.source | if .source == "github" then .repo else (.url // .path // .repo) end)] | @tsv' "$LOCAL_SETTINGS")

  log "Plugins"
  remote_plugins=$(rsh 'claude plugin list --json 2>/dev/null' | jq -r '.[] | select(.scope == "user") | "\(.id) \(.enabled)"' || true)
  while IFS=$'\t' read -r id want; do
    [[ -n "$id" ]] || continue
    have=$(awk -v id="$id" '$1 == id {print $2}' <<<"$remote_plugins")
    if [[ "$want" == "true" ]]; then
      if [[ -z "$have" ]]; then
        # -y is needed without a TTY; these are plugins this machine already trusts.
        mutate "install $id" rsh "claude plugin install '$id' --scope user -y" || warn "could not install $id"
      elif [[ "$have" != "true" ]]; then
        mutate "enable $id" rsh "claude plugin enable '$id' --scope user" || warn "could not enable $id"
      else note "$id: enabled"; fi
    else
      if [[ "$have" == "true" ]]; then
        mutate "disable $id" rsh "claude plugin disable '$id' --scope user" || warn "could not disable $id"
      else note "$id: stays disabled"; fi
    fi
  done < <(jq -r '.enabledPlugins // {} | to_entries[] | [.key, (.value | tostring)] | @tsv' "$LOCAL_SETTINGS")
else
  warn "claude is not installed on $HOST; skipping marketplaces and plugins."
fi

# --- Orca skills ------------------------------------------------------------
log "Orca skills"
if rsh 'command -v orca >/dev/null 2>&1'; then
  flags=""
  for sk in "${ORCA_SKILLS[@]}"; do flags+=" --skill $sk"; done
  (( remote_has_herdr )) && flags+=" --skill herdr"
  mutate "orca skills install$flags" rsh "orca skills install$flags" || warn "orca skills install failed"
else
  warn "orca is not installed on $HOST; skipping skills."
fi

# --- settings.json ----------------------------------------------------------
log "settings.json"
subset=$(jq -c "$SUBSET_FILTER" "$LOCAL_SETTINGS")
note "keys: $(jq -r 'keys | join(", ")' <<<"$subset")"

# Runs on the remote: backs the file up, merges the subset into it (every key replaced
# as a whole, except permissions, where only defaultMode changes) and swaps it in.
MERGE_SCRIPT=$(cat <<'REMOTE'
# shellcheck disable=SC2016,SC2329  # remote commands are single-quoted on purpose; functions run through mutate
set -euo pipefail
f="$HOME/.claude/settings.json"
mkdir -p "$HOME/.claude"
[ -f "$f" ] || echo '{}' > "$f"
cp "$f" "$f.bak-$(date +%Y%m%d%H%M%S)"
tmp=$(mktemp "$f.XXXXXX")
jq --argjson s "$SUBSET_JSON" '
  (if $s.permissions.defaultMode then (.permissions //= {}) | .permissions.defaultMode = $s.permissions.defaultMode else . end)
  | . + ($s | del(.permissions))' "$f" > "$tmp"
mv "$tmp" "$f"
REMOTE
)
merge_settings() {
  local q; q=$(printf '%q' "$subset")
  ssh -o BatchMode=yes "$HOST" "export PATH=\"$REMOTE_PATH\" SUBSET_JSON=$q; bash -s" <<<"$MERGE_SCRIPT"
}
mutate "merge the keys into $HOST:~/.claude/settings.json (backup first)" merge_settings

# --- Memory -----------------------------------------------------------------
log "Memory"
MEMORY_SCRIPT=$(cat <<'REMOTE'
# shellcheck disable=SC2016,SC2329  # remote commands are single-quoted on purpose; functions run through mutate
set -euo pipefail
canon="$HOME/.claude/wattwise-memory"
mkdir -p "$canon"
ts=$(date +%Y%m%d%H%M%S)
shopt -s nullglob
for dir in "$HOME"/.claude/projects/*watt-wise*; do
  [ -d "$dir" ] || continue
  m="$dir/memory"
  if [ -L "$m" ]; then
    if [ "$(readlink -f "$m")" = "$(readlink -f "$canon")" ]; then echo "ok: $m"; continue; fi
    rm "$m"
  elif [ -d "$m" ]; then
    if [ -n "$(ls -A "$m")" ]; then
      cp -an "$m"/. "$canon"/
      mv "$m" "$m.premerge-$ts"
      echo "merged $m into the canonical folder (kept as $m.premerge-$ts)"
    else
      rmdir "$m"
    fi
  fi
  ln -s "$canon" "$m"
  echo "linked $m"
done
REMOTE
)
memory_setup() { ssh -o BatchMode=yes "$HOST" 'bash -s' <<<"$MEMORY_SCRIPT"; }
mutate "link ~/.claude/wattwise-memory into every watt-wise project key on $HOST" memory_setup

log "Done"
if (( DRY_RUN )); then
  note "dry run: nothing was changed."
else
  note "Run with --check to confirm the parity."
fi
(( WARNINGS )) && note "$WARNINGS warning(s) above."
exit 0
