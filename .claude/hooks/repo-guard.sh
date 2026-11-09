#!/usr/bin/env bash
# PreToolUse hook (Bash only, every session): hard guards that hold in every permission mode.
# Blocks:
#   - --no-verify anywhere in a command
#   - force pushes (--force, --force-with-lease, -f, +refspec) to main or epic/*
#   - gh api writes to rulesets, branch protection and the repository settings endpoint
# Everything else passes, including what merge-to-main and merge-to-epic run (git push origin
# <branch>, git push -u, gh pr create, gh pr merge --squash/--merge, git merge --squash,
# git commit -S). Exit 2 shows stderr to Claude.
# Parsing is best-effort: the command is split into segments on && || ; | and newlines, and
# each segment into whitespace-separated words with quotes stripped.
set -euo pipefail

input=$(cat)
[[ "$(jq -r '.tool_name // empty' <<<"$input")" == Bash ]] || exit 0
command=$(jq -r '.tool_input.command // empty' <<<"$input")
cwd=$(jq -r '.cwd // empty' <<<"$input")
[[ -n "$command" ]] || exit 0
[[ -n "$cwd" && -d "$cwd" ]] || cwd=$PWD

block() {
  echo "repo-guard: $1" >&2
  exit 2
}

# True when a ref (optionally +src:dst, refs/heads/…) names main or epic/*.
protected_ref() {
  local ref=${1#+}
  ref=${ref##*:}
  ref=${ref#refs/heads/}
  [[ "$ref" == main || "$ref" == epic/* ]]
}

current_branch() {
  git -C "$cwd" branch --show-current 2>/dev/null || true
}

check_push() {
  local -a words=("$@")
  local i push=-1 force=0 plus=0 mirror=0
  for i in "${!words[@]}"; do
    if [[ "${words[$i]}" == push ]]; then
      push=$i
      break
    fi
  done
  ((push >= 0)) || return 0
  local -a rest=("${words[@]:push+1}") positional=()
  local w
  for w in "${rest[@]}"; do
    case "$w" in
      --force | --force-with-lease | --force-with-lease=* | --force-if-includes) force=1 ;;
      --mirror) mirror=1 ;;
      --*) ;;
      -*[f]*) force=1 ;;
      -*) ;;
      +*)
        plus=1
        positional+=("$w")
        ;;
      *) positional+=("$w") ;;
    esac
  done
  ((force || plus)) || return 0
  # positional[0] is the remote; the rest are refspecs.
  local -a refspecs=("${positional[@]:1}")
  if ((mirror)); then
    block "force-pushing with --mirror is not allowed"
  fi
  local ref
  for ref in "${refspecs[@]}"; do
    # HEAD (or +HEAD) pushes the current branch.
    [[ "${ref#+}" != HEAD ]] || ref=$(current_branch)
    if protected_ref "$ref"; then
      block "force-pushing to main or epic/* is not allowed ($ref)"
    fi
  done
  # No refspec: the push goes to the current branch.
  if ((${#refspecs[@]} == 0)); then
    local branch
    branch=$(current_branch)
    if protected_ref "$branch"; then
      block "force-pushing the current branch ($branch) is not allowed"
    fi
  fi
}

check_gh_api() {
  local -a words=("$@")
  local method="" write_field=0 i w
  for i in "${!words[@]}"; do
    w=${words[$i]}
    case "$w" in
      -X | --method) method=${words[$((i + 1))]:-} ;;
      -X?*) method=${w#-X} ;;
      --method=*) method=${w#--method=} ;;
      -f | -F | --field | --raw-field | --input) write_field=1 ;;
      -f?* | -F?*) write_field=1 ;;
      --field=* | --raw-field=* | --input=*) write_field=1 ;;
    esac
  done
  method=${method^^}
  if [[ -z "$method" ]] && ((write_field)); then method=POST; fi
  [[ -n "$method" && "$method" != GET && "$method" != HEAD ]] || return 0
  local joined="${words[*]}"
  if [[ "$joined" == */rulesets* ]]; then
    block "gh api writes to rulesets are not allowed"
  fi
  if [[ "$joined" =~ /branches/[^[:space:]]*protection ]]; then
    block "gh api writes to branch protection are not allowed"
  fi
  # Repository settings endpoint: repos/<owner>/<repo> with nothing after it.
  for w in "${words[@]}"; do
    if [[ "$w" =~ ^/?repos/[^/]+/[^/]+/?$ ]]; then
      block "gh api writes to the repository settings endpoint are not allowed"
    fi
  done
}

# Split into segments, then into words.
segments=$(sed -E 's/(&&|\|\||;|\|)/\n/g' <<<"$command")
while IFS= read -r segment; do
  [[ -n "${segment//[[:space:]]/}" ]] || continue
  clean=${segment//\"/}
  clean=${clean//\'/}
  read -ra words <<<"$clean"
  ((${#words[@]} > 0)) || continue

  for w in "${words[@]}"; do
    [[ "$w" == --no-verify || "$w" == --no-verify=* ]] && block "--no-verify is not allowed; fix what the hook reports instead"
  done

  has_git=0 has_gh=0
  for w in "${words[@]}"; do
    [[ "$w" == git ]] && has_git=1
    [[ "$w" == gh ]] && has_gh=1
  done
  if ((has_git)); then check_push "${words[@]}"; fi
  if ((has_gh)); then
    for i in "${!words[@]}"; do
      if [[ "${words[$i]}" == api && "${words[$((i - 1))]:-}" == gh ]]; then
        check_gh_api "${words[@]:i+1}"
        break
      fi
    done
  fi
done <<<"$segments"
exit 0
