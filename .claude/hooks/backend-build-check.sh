#!/usr/bin/env bash
# Stop hook: before Claude finishes, build the backend if C# or project files changed and
# require 0 warnings and 0 errors. Exit 2 shows stderr to Claude.
# It builds into its own artifacts folder outside the repo, because concurrent builds (Stryker,
# another session, a subagent) race on the shared bin/ and obj/ and fail with CS0006. Runs of
# this hook wait for each other on a lock in that folder. The folder's own incremental state
# decides what gets rebuilt; an up-to-date project doesn't re-emit warnings, so this catches
# the warnings of the build right after the edit that caused them.
set -euo pipefail

input=$(cat)
# Claude already got one blocked attempt; let it stop instead of looping.
[[ "$(jq -r '.stop_hook_active // false' <<<"$input")" != true ]] || exit 0

project=$(git -C "${CLAUDE_PROJECT_DIR:-$PWD}" rev-parse --show-toplevel)
cd "$project"

changed=$(git status --porcelain -- backend | grep -E '\.(cs|csproj|props|slnx)$' || true)
[[ -n "$changed" ]] || exit 0

# One folder per checkout (worktrees differ by path), so checkouts don't share state.
hash=$(printf '%s' "$project" | sha1sum | cut -c1-12)
artifacts="${XDG_CACHE_HOME:-$HOME/.cache}/wattwise/build-check/$hash"
mkdir -p "$artifacts"
exec 9>"$artifacts/.lock"
flock 9

status=0
# 9>&-: MSBuild's reused nodes and the compiler server outlive the build; if they inherited
# the lock's fd they would hold the lock for minutes and stall the next run.
output=$(dotnet build backend/WattWise.slnx -nologo -v q --artifacts-path "$artifacts" 2>&1 9>&-) || status=$?
lines=$(grep -E 'warning|error' <<<"$output" | sort -u || true)
if [[ $status -ne 0 || -n "$lines" ]]; then
  {
    echo "backend-build-check: the backend build must end with 0 warnings and 0 errors"
    echo "${lines:-$output}"
  } >&2
  exit 2
fi
exit 0
