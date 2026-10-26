#!/usr/bin/env bash
# Stop hook: before Claude finishes, build the backend if C# or project files changed and
# require 0 warnings and 0 errors. Exit 2 shows stderr to Claude.
# An up-to-date project doesn't re-emit warnings, so this catches the warnings of the build
# right after the edit that caused them.
set -euo pipefail

input=$(cat)
# Claude already got one blocked attempt; let it stop instead of looping.
[[ "$(jq -r '.stop_hook_active // false' <<<"$input")" != true ]] || exit 0

project=$(git -C "${CLAUDE_PROJECT_DIR:-$PWD}" rev-parse --show-toplevel)
cd "$project"

changed=$(git status --porcelain -- backend | grep -E '\.(cs|csproj|props|slnx)$' || true)
[[ -n "$changed" ]] || exit 0

status=0
output=$(dotnet build backend/WattWise.slnx -nologo -v q 2>&1) || status=$?
lines=$(grep -E 'warning|error' <<<"$output" | sort -u || true)
if [[ $status -ne 0 || -n "$lines" ]]; then
  {
    echo "backend-build-check: the backend build must end with 0 warnings and 0 errors"
    echo "${lines:-$output}"
  } >&2
  exit 2
fi
exit 0
