#!/usr/bin/env bash
#
# Two-way sync of the Watt-Wise Claude project memory between this machine (vidar)
# and tethys, with unison. Conflicts keep both: the newer copy wins and unison
# saves the loser as a conflict copy (-copyonconflict), so nothing is lost.
#
# Usage: deploy/agent-host/sync-memory.sh [--dry-run]
#   --dry-run  prints the unison command and exits.
#
# Exits 0 without output when tethys is unreachable (vidar is often offline or the
# VM is down), so a timer never reports a failure for that.
#
# Overridable by env:
#   LOCAL_MEMORY   default ~/.claude/projects/-home-caleb-code-personal-watt-wise/memory/
#   REMOTE_HOST    default tethys
#   REMOTE_MEMORY  default /home/corvus/.claude/wattwise-memory/
#   UNISON_BIN     default unison
#
# Needs unison of the same version on both machines (compare `unison -version`).

set -euo pipefail

LOCAL_MEMORY="${LOCAL_MEMORY:-$HOME/.claude/projects/-home-caleb-code-personal-watt-wise/memory/}"
REMOTE_HOST="${REMOTE_HOST:-tethys}"
REMOTE_MEMORY="${REMOTE_MEMORY:-/home/corvus/.claude/wattwise-memory/}"
UNISON_BIN="${UNISON_BIN:-unison}"
DRY_RUN=0

for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN=1 ;;
    -h|--help) sed -n '2,/^$/p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $arg" >&2; exit 2 ;;
  esac
done

# Roots without a trailing slash; ssh://host//abs/path is unison's form for an absolute remote path.
local_root="${LOCAL_MEMORY%/}"
remote_root="ssh://${REMOTE_HOST}/${REMOTE_MEMORY%/}"

cmd=("$UNISON_BIN" "$local_root" "$remote_root"
  -batch -prefer newer -copyonconflict -times
  -sshargs "-o BatchMode=yes -o ConnectTimeout=10")

if (( DRY_RUN )); then
  printf 'dry-run:'; printf ' %q' "${cmd[@]}"; printf '\n'
  exit 0
fi

# Unreachable host: quiet success.
ssh -o BatchMode=yes -o ConnectTimeout=5 "$REMOTE_HOST" true >/dev/null 2>&1 || exit 0

command -v "$UNISON_BIN" >/dev/null 2>&1 || { echo "unison is not installed" >&2; exit 1; }

mkdir -p "$local_root"
# A second run while one is going would only contend for unison's archive lock.
exec 9>"${XDG_RUNTIME_DIR:-/tmp}/wattwise-memory-sync.lock"
flock -n 9 || exit 0

# The remote folder must exist or unison refuses to start.
ssh -o BatchMode=yes -o ConnectTimeout=10 "$REMOTE_HOST" "mkdir -p '${REMOTE_MEMORY%/}'"

rc=0
"${cmd[@]}" >/dev/null || rc=$?
# unison: 0 = fully synced, 1 = everything synced except skipped files, 2 = non-fatal failures, 3 = fatal.
case "$rc" in
  0|1) exit 0 ;;
  *) echo "unison failed with exit code $rc" >&2; exit "$rc" ;;
esac
