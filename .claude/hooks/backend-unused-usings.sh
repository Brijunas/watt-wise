#!/usr/bin/env bash
# PostToolUse hook: after Claude writes a backend .cs file, report unused using directives
# (IDE0005) back to Claude. Checks only; it changes nothing. Exit 2 shows stderr to Claude.
set -euo pipefail

project=$(realpath "${CLAUDE_PROJECT_DIR:-$PWD}")
file=$(jq -r '.tool_input.file_path // empty')

[[ -n "$file" && -f "$file" ]] || exit 0
file=$(realpath "$file")

# Only C# sources under backend/, not generated migrations or build output.
[[ "$file" == "$project"/backend/*.cs ]] || exit 0
[[ "$file" != */Migrations/* && "$file" != */bin/* && "$file" != */obj/* ]] || exit 0

# Nearest project file above the edited file.
dir=$(dirname "$file")
csproj=""
while [[ "$dir" == "$project"/backend/* ]]; do
  csproj=$(find "$dir" -maxdepth 1 -name '*.csproj' -print -quit)
  [[ -n "$csproj" ]] && break
  dir=$(dirname "$dir")
done
[[ -n "$csproj" ]] || exit 0

# --include matches paths relative to the working directory; an absolute path silently matches nothing.
cd "$project"
relative=${file#"$project"/}
if ! output=$(dotnet format style "$csproj" --include "$relative" --diagnostics IDE0005 --verify-no-changes 2>&1); then
  {
    echo "backend-unused-usings: remove the unused using directives in $relative:"
    grep 'IDE0005' <<<"$output" || echo "$output"
  } >&2
  exit 2
fi
exit 0
