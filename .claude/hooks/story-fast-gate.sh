#!/usr/bin/env bash
# Stop hook for the coder agents (registered in their frontmatter, where Stop runs as
# SubagentStop). Before a coder may stop, the work on its story branch must pass a fast gate
# against the epic branch:
#   backend changes:  dotnet build (0 warnings), dotnet format --verify-no-changes, the 4 unit
#                     test projects
#   frontend changes (frontend/, admin/, packages/): pnpm lint, then typecheck and test of the
#                     affected packages and the packages that depend on them
# Branch `story/<E>/<id>` gives the base `origin/epic/<E>`. Any other branch passes untouched.
# Attempts are counted per session in ${XDG_RUNTIME_DIR:-/tmp}/wattwise-gate/<session_id>: a
# failing gate blocks (exit 2, short failure tail on stderr) up to 3 times, then lets the agent
# stop with "gate still failing after 3 attempts". The counter restarts whenever the hook input
# says stop_hook_active is false, i.e. on a fresh stop request rather than a retry.
set -euo pipefail

input=$(cat)
cwd=$(jq -r '.cwd // empty' <<<"$input")
session=$(jq -r '.session_id // "unknown"' <<<"$input")
active=$(jq -r '.stop_hook_active // false' <<<"$input")
[[ -n "$cwd" && -d "$cwd" ]] || cwd=$PWD
max_blocks=3

top=$(git -C "$cwd" rev-parse --show-toplevel 2>/dev/null) || exit 0
cd "$top"

branch=$(git branch --show-current)
if [[ ! "$branch" =~ ^story/([^/]+)/[^/]+$ ]]; then
  exit 0
fi
epic=${BASH_REMATCH[1]}
base="origin/epic/$epic"

counter_dir="${XDG_RUNTIME_DIR:-/tmp}/wattwise-gate"
mkdir -p "$counter_dir"
counter="$counter_dir/${session//[^A-Za-z0-9_.-]/_}"
[[ "$active" == true ]] || rm -f "$counter"
attempts=0
[[ ! -f "$counter" ]] || attempts=$(<"$counter")

if ((attempts >= max_blocks)); then
  echo "story-fast-gate: gate still failing after $max_blocks attempts; letting the agent stop. Report the failure in your result." >&2
  rm -f "$counter"
  exit 0
fi

if ! git rev-parse --verify -q "$base" >/dev/null; then
  git fetch -q origin "epic/$epic" 2>/dev/null || true
fi
if ! git rev-parse --verify -q "$base" >/dev/null; then
  echo "story-fast-gate: $base not found; skipping the gate" >&2
  exit 0
fi

# Everything the story changed: commits since the base plus uncommitted work.
changed=$({
  git diff --name-only "$base"...HEAD
  git status --porcelain | cut -c4-
} | sort -u)
backend=0
frontend=0
grep -q '^backend/' <<<"$changed" && backend=1
grep -qE '^(frontend|admin|packages)/' <<<"$changed" && frontend=1
if ((!backend && !frontend)); then
  rm -f "$counter"
  exit 0
fi

failures=""
log=$(mktemp)
trap 'rm -f "$log"' EXIT

# run <label> <command...>: records a failure with the tail of its output.
run() {
  local label=$1
  shift
  # 9>&-: dotnet's reused nodes and the compiler server outlive the build; if they inherited
  # the lock's fd they would hold the lock for minutes and stall the next run.
  if ! "$@" >"$log" 2>&1 9>&-; then
    failures+="--- $label failed ---"$'\n'"$(tail -n 25 "$log")"$'\n'
    return 1
  fi
  return 0
}

if ((backend)); then
  hash=$(printf '%s' "$top" | sha1sum | cut -c1-12)
  lock_dir="${XDG_CACHE_HOME:-$HOME/.cache}/wattwise/story-fast-gate/$hash"
  mkdir -p "$lock_dir"
  exec 9>"$lock_dir/.lock"
  flock 9
  export MSBUILDDISABLENODEREUSE=1
  # A failed build makes format and the tests pointless, so stop at the first failing step.
  ok=1
  run "dotnet build" dotnet build backend/WattWise.slnx -m:1 -nologo -v q || ok=0
  if ((ok)) && grep -qE ' warning ' "$log"; then
    failures+="--- dotnet build must end with 0 warnings ---"$'\n'"$(grep -E ' warning ' "$log" | sort -u | tail -n 25)"$'\n'
    ok=0
  fi
  if ((ok)); then
    run "dotnet format --verify-no-changes" dotnet format backend/WattWise.slnx --verify-no-changes || ok=0
  fi
  if ((ok)); then
    for project in Domain Application Infrastructure Api; do
      run "unit tests WattWise.$project.Tests" dotnet test --project "backend/tests/WattWise.$project.Tests" || true
    done
  fi
fi

if ((frontend)); then
  filters=()
  while IFS= read -r dir; do
    [[ -f "$dir/package.json" ]] || continue
    name=$(jq -r '.name // empty' "$dir/package.json")
    [[ -z "$name" ]] || filters+=(--filter "...$name")
  done < <(grep -E '^(frontend|admin)/|^packages/[^/]+/' <<<"$changed" | sed -E 's#^((frontend|admin)|packages/[^/]+)/.*#\1#' | sort -u)
  run "pnpm lint" pnpm lint || true
  if ((${#filters[@]} > 0)); then
    run "pnpm typecheck" pnpm "${filters[@]}" typecheck || true
    run "pnpm test" pnpm "${filters[@]}" test || true
  fi
fi

if [[ -z "$failures" ]]; then
  rm -f "$counter"
  exit 0
fi

attempts=$((attempts + 1))
echo "$attempts" >"$counter"
{
  echo "story-fast-gate: the fast gate against $base failed (attempt $attempts of $max_blocks). Fix this before you finish:"
  echo "$failures"
} >&2
exit 2
