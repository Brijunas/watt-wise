#!/usr/bin/env bash
# Start (or reattach to) an epic run:
#   start-epic.sh <E> [--dry-run]
# 1. checks the prerequisites (gh auth, 1Password, docker, tmux; the user-level Claude parity
#    check and the memory sync only warn, and only run from another machine than AGENT_HOST,
#    default tethys, while it is reachable: on tethys itself the timer on vidar does them)
# 2. creates epic/<E> from origin/main if it does not exist, and pushes it
# 3. creates the epic checkout (Orca worktree `epic-<E>`, falling back to `git worktree add`)
# 4. links the checkout's Claude project memory to ~/.claude/wattwise-memory
# 5. starts or attaches the tmux session `epic-<E>` running `claude --agent orchestrator`
# 6. the orchestrator starts with the /goal line as its first prompt; the line is printed too
# --dry-run prints what would happen and changes nothing.
set -euo pipefail

epic=""
dry=0
for arg in "$@"; do
  case "$arg" in
    --dry-run) dry=1 ;;
    -*)
      echo "usage: start-epic.sh <E> [--dry-run]" >&2
      exit 2
      ;;
    *) epic=$arg ;;
  esac
done
if [[ -z "$epic" ]]; then
  echo "usage: start-epic.sh <E> [--dry-run]" >&2
  exit 2
fi
if [[ ! "$epic" =~ ^[A-Za-z0-9._-]+$ ]]; then
  echo "start-epic: '$epic' is not a valid epic id" >&2
  exit 2
fi

top=$(git rev-parse --show-toplevel)
branch="epic/$epic"
session="epic-$epic"
canonical_memory="$HOME/.claude/wattwise-memory"

say() { echo "start-epic: $*"; }
warn() { echo "start-epic: warning: $*" >&2; }
# act <description> <command...>: runs the command, or only prints it in a dry run.
act() {
  local description=$1
  shift
  if ((dry)); then
    say "would: $description"
  else
    say "$description"
    "$@"
  fi
}

# Claude's project key for a folder: the absolute path with / and . replaced by -.
project_key() { sed -E 's#[/.]#-#g' <<<"$1"; }

# --- 1. prerequisites -------------------------------------------------------
if ((dry)); then
  say "would check: gh auth status, op whoami, docker info, tmux"
else
  gh auth status >/dev/null 2>&1 || {
    echo "start-epic: gh is not signed in (gh auth login)" >&2
    exit 1
  }
  op whoami >/dev/null 2>&1 || {
    echo "start-epic: 1Password is not signed in (op whoami)" >&2
    exit 1
  }
  docker info >/dev/null 2>&1 || {
    echo "start-epic: docker is not running" >&2
    exit 1
  }
  command -v tmux >/dev/null || {
    echo "start-epic: tmux is not installed" >&2
    exit 1
  }
fi

# The parity check and the memory sync compare vidar with the agent host; they only make sense
# from another machine while the host is reachable.
agent_host=${AGENT_HOST:-tethys}
local_host=$(hostname -s 2>/dev/null || hostname)
run_sync=0
if [[ "$local_host" == "$agent_host" ]]; then
  say "on $agent_host: the Claude parity check and the memory sync run from vidar (the timer)"
elif ((dry)); then
  run_sync=1
elif ssh -o BatchMode=yes -o ConnectTimeout=5 "$agent_host" true >/dev/null 2>&1; then
  run_sync=1
else
  say "$agent_host is not reachable; the Claude parity check and the memory sync run from vidar (the timer)"
fi
if ((run_sync)); then
  if [[ -x "$top/deploy/agent-host/sync-user-claude.sh" ]]; then
    if ((dry)); then
      say "would run deploy/agent-host/sync-user-claude.sh --check (warn only)"
    else
      "$top/deploy/agent-host/sync-user-claude.sh" --check >&2 ||
        warn "user-level Claude config differs from the other machine (parity check failed)"
    fi
  else
    say "no deploy/agent-host/sync-user-claude.sh; skipping the parity check"
  fi
  if [[ -x "$top/deploy/agent-host/sync-memory.sh" ]]; then
    if ((dry)); then
      say "would run deploy/agent-host/sync-memory.sh (warn only)"
    else
      "$top/deploy/agent-host/sync-memory.sh" >&2 || warn "memory sync failed; continuing"
    fi
  else
    say "no deploy/agent-host/sync-memory.sh; skipping the memory sync"
  fi
fi

# --- 2. the epic branch -----------------------------------------------------
if ((dry)); then
  say "would: git fetch origin; create $branch from origin/main and push it if missing on origin"
else
  git fetch -q origin
  if git ls-remote --exit-code --heads origin "$branch" >/dev/null 2>&1; then
    say "$branch already exists on origin"
  else
    say "creating $branch from origin/main"
    git push origin "origin/main:refs/heads/$branch"
    git fetch -q origin "$branch"
  fi
fi

# --- 3. the epic checkout ---------------------------------------------------
checkout=""
if existing=$(git worktree list --porcelain | awk -v b="refs/heads/$branch" '
  /^worktree / { path = substr($0, 10) }
  $0 == "branch " b { print path; exit }'); [[ -n "$existing" ]]; then
  checkout=$existing
  say "reusing the checkout of $branch at $checkout"
else
  fallback="$(dirname "$top")/$session"
  if ((dry)); then
    say "would: orca worktree create --name $session --branch $branch --base-branch origin/$branch (fallback: git worktree add $fallback $branch)"
    checkout=$fallback
  else
    created=0
    if command -v orca >/dev/null 2>&1; then
      if orca worktree create --repo "path:$top" --name "$session" --branch "$branch" \
        --base-branch "origin/$branch" --setup skip >&2; then
        created=1
      else
        warn "orca worktree create failed; falling back to git worktree add"
      fi
    fi
    if ((!created)); then
      git branch --track "$branch" "origin/$branch" 2>/dev/null || true
      git worktree add "$fallback" "$branch"
    fi
    checkout=$(git worktree list --porcelain | awk -v b="refs/heads/$branch" '
      /^worktree / { path = substr($0, 10) }
      $0 == "branch " b { print path; exit }')
    [[ -n "$checkout" ]] || {
      echo "start-epic: could not find the checkout of $branch" >&2
      exit 1
    }
  fi
fi

# --- 4. memory symlink for the checkout's project key ------------------------
key=$(project_key "$checkout")
memory_link="$HOME/.claude/projects/$key/memory"
if [[ ! -d "$canonical_memory" ]]; then
  say "no $canonical_memory; skipping the memory symlink"
elif [[ -L "$memory_link" ]]; then
  say "memory symlink already in place ($memory_link)"
elif [[ -e "$memory_link" ]]; then
  warn "$memory_link exists and is not a symlink; leaving it alone"
else
  # shellcheck disable=SC2016 # the script is single-quoted on purpose; its args are $1 and $2
  act "linking $memory_link -> $canonical_memory" bash -c \
    'mkdir -p "$(dirname "$1")" && ln -s "$2" "$1"' _ "$memory_link" "$canonical_memory"
fi

# --- 5. the /goal line and the tmux session ---------------------------------
goal="/goal Every story in docs/epic-runs/$epic/state.json is merged or parked with a brief, the epic gate has passed or every remaining story is parked, and report.md exists. Otherwise stop after 200 turns."
if ((dry)); then
  say "would: tmux new-session -d -s $session -c $checkout 'claude --agent orchestrator \"<goal>\"' (if $session does not exist), then attach"
else
  if tmux has-session -t "$session" 2>/dev/null; then
    say "tmux session $session already exists"
  else
    say "starting tmux session $session in $checkout"
    tmux new-session -d -s "$session" -c "$checkout" "claude --agent orchestrator $(printf '%q' "$goal")"
  fi
fi

echo
say "the orchestrator starts with this goal (paste it if the session was already running):"
echo "$goal"
echo

if ((dry)); then
  say "dry run: nothing was changed"
elif [[ -t 0 && -t 1 ]]; then
  if [[ -n "${TMUX:-}" ]]; then
    tmux switch-client -t "$session"
  else
    exec tmux attach -t "$session"
  fi
else
  say "not a terminal; attach with: tmux attach -t $session"
fi
