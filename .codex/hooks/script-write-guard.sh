#!/usr/bin/env bash
# PreToolUse hook: warn when a Bash command writes a repo source file (a path under src/, or *.ts,
# *.tsx, *.md inside the repo) through python, a redirect, tee or sed -i. Prettier's PostToolUse hook
# runs only on Edit and Write. Never blocks: exit 0 with a note the model reads as context.
set -euo pipefail

input=$(cat)
command=$(jq -r '.tool_input.command // empty' <<<"$input")
[[ -n "$command" ]] || exit 0

# Cheap exit: no write form in the command.
py='open\([^)]*[,[:space:]]["'"'"'][wa]b?\+?["'"'"']|write_text\(|\.write\('
sedi='(^|[[:space:];&|])sed[[:space:]]+(-[a-zA-Z]+[[:space:]]+)*-i'
tee='(^|[[:space:];&|])tee[[:space:]]'
redir='>>?[[:space:]]*[^[:space:]&|;<>]'
grep -qE "$py|$sedi|$tee|$redir" <<<"$command" || exit 0

cwd=$(jq -r '.cwd // empty' <<<"$input")
root=$(git -C "${cwd:-$PWD}" rev-parse --show-toplevel 2>/dev/null) || exit 0

# Paths the command may write: redirect and tee targets, plus every path-like token when python or sed -i is present.
candidates=$(
  {
    grep -oE '>>?[[:space:]]*[^[:space:]&|;<>]+' <<<"$command" | sed -E 's/^>>?[[:space:]]*//' || true
    grep -oE 'tee[[:space:]]+(-[a-zA-Z]+[[:space:]]+)*[^[:space:]|;&<>]+' <<<"$command" | awk '{print $NF}' || true
    if grep -qE "$py|$sedi" <<<"$command"; then
      grep -oE "[^[:space:]'\"=()<>|;,]+" <<<"$command" || true
    fi
  } | tr -d "'\""
)

while IFS= read -r path; do
  [[ -n "$path" && "$path" != /dev/* && "$path" != '$'* && "$path" != '~'* && "$path" != ../* ]] || continue
  [[ "$path" == */src/* || "$path" == src/* || "$path" == *.ts || "$path" == *.tsx || "$path" == *.md ]] || continue
  if [[ "$path" == /* ]]; then
    [[ "$path" == "$root"/* ]] || continue
  fi
  jq -n --arg path "$path" '{hookSpecificOutput: {hookEventName: "PreToolUse", additionalContext: ("This command writes " + $path + " from a Bash script. Write repo files with Edit or Write: the Prettier PostToolUse hook runs only on those two.")}}'
  exit 0
done <<<"$candidates"
exit 0
