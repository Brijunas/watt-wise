#!/usr/bin/env bash
# PostToolUse hook: after Claude writes a TypeScript file in frontend/, admin/ or packages/,
# run ESLint on it and report problems back to Claude. Checks only; it changes nothing.
# Exit 2 shows stderr to Claude.
set -euo pipefail

project=$(git -C "${CLAUDE_PROJECT_DIR:-$PWD}" rev-parse --show-toplevel)
file=$(jq -r '.tool_input.file_path // empty')

[[ -n "$file" && -f "$file" ]] || exit 0
file=$(realpath "$file")

# Only .ts/.tsx sources of the two apps and the packages, not generated code or build output.
[[ "$file" == "$project"/frontend/*.ts || "$file" == "$project"/frontend/*.tsx ||
  "$file" == "$project"/admin/*.ts || "$file" == "$project"/admin/*.tsx ||
  "$file" == "$project"/packages/*.ts || "$file" == "$project"/packages/*.tsx ]] || exit 0
[[ "$file" != */generated/* && "$file" != */dist/* && "$file" != */dev-dist/* && "$file" != */node_modules/* ]] || exit 0

cd "$project"
relative=${file#"$project"/}
if ! output=$(pnpm exec eslint --max-warnings=0 -- "$relative" 2>&1); then
  {
    echo "frontend-lint: fix the lint errors in $relative:"
    echo "$output"
  } >&2
  exit 2
fi
exit 0
