#!/usr/bin/env bash
# PostToolUse hook: format files Claude writes with Prettier. Never blocks Claude.
set -euo pipefail

project=$(realpath "${CLAUDE_PROJECT_DIR:-$PWD}")
file=$(jq -r '.tool_input.file_path // empty')

[[ -n "$file" && -f "$file" ]] || exit 0

# Only touch files inside the project (Claude also writes under ~/.claude).
file=$(realpath "$file")
[[ "$file" == "$project"/* ]] || exit 0

cd "$project"
# --ignore-unknown skips file types Prettier can't parse; .prettierignore is honoured.
if ! pnpm exec prettier --write --ignore-unknown --log-level warn -- "$file" >&2; then
  echo "prettier-write: Prettier failed on $file (left as is)" >&2
fi
exit 0
