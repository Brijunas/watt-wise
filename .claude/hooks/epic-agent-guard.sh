#!/usr/bin/env bash
# PreToolUse hook for the epic agents, registered only in their frontmatter:
#   epic-agent-guard.sh --role orchestrator|coder|runner
# All roles are blocked from: git push to main, gh pr merge into main unless the PR head SHA
# equals the content of ~/.local/state/wattwise/epic-<E>.gate, and editing .claude/**,
# .github/**, .husky/**, .editorconfig, Directory.Build.*, eslint.config.js,
# stryker-config.json and lint-staged.config.js.
#   orchestrator: Edit/Write only under docs/epic-runs/** and docs/epics.md, docs/implemented.md.
#                 The orchestrator is the main session, so its hooks also fire for its subagents'
#                 tool calls. Input from a subagent carries agent_id/agent_type; for those the
#                 path restriction is skipped (only the common rules apply) and the subagent's own
#                 frontmatter guard adds its role rules.
#   coder:        also no edits to docs/epic-runs/**, backend/tests/**, *.test.ts(x), e2e/**.
# Bash commands that write files to protected paths are blocked on a best-effort basis: the
# command is split on whitespace and only the common writers are recognised (>, >>, tee,
# sed -i, cp, mv, rm, install, truncate, dd of=). A determined agent can get around it; the
# Edit/Write guard and the git hooks are the real boundary.
# Paths are made repo-relative against the toplevel of the hook's cwd, because worktrees differ.
# Exit 2 shows stderr to Claude.
set -euo pipefail

role=""
while (($# > 0)); do
  case "$1" in
    --role)
      role=${2:-}
      shift 2
      ;;
    *) shift ;;
  esac
done
case "$role" in
  orchestrator | coder | runner) ;;
  *)
    echo "epic-agent-guard: --role orchestrator|coder|runner is required" >&2
    exit 2
    ;;
esac

input=$(cat)
tool=$(jq -r '.tool_name // empty' <<<"$input")
cwd=$(jq -r '.cwd // empty' <<<"$input")
[[ -n "$cwd" && -d "$cwd" ]] || cwd=$PWD
top=$(git -C "$cwd" rev-parse --show-toplevel 2>/dev/null || echo "$cwd")
agent_id=$(jq -r '.agent_id // empty' <<<"$input")
agent_type=$(jq -r '.agent_type // empty' <<<"$input")
# True when an orchestrator-role hook fires for a subagent's call (see header).
orch_subagent=0
if [[ "$role" == orchestrator && (-n "$agent_id" || (-n "$agent_type" && "$agent_type" != orchestrator)) ]]; then
  orch_subagent=1
fi
state_dir=${WATTWISE_STATE_DIR:-$HOME/.local/state/wattwise}

block() {
  echo "epic-agent-guard ($role): $1" >&2
  exit 2
}

# Absolute, normalised path from a possibly relative one (relative to cwd).
abs_path() {
  local p=$1
  # shellcheck disable=SC2088 # matching a literal "~/" prefix, not expanding it
  [[ "$p" == "~/"* ]] && p="$HOME/${p#\~/}"
  [[ "$p" == /* ]] || p="$cwd/$p"
  realpath -m -- "$p"
}

# Checks one path for the given operation context (edit|bash). Blocks on violation.
check_path() {
  local raw=$1 abs rel base
  abs=$(abs_path "$raw")
  if [[ "$abs" == "$top" || "$abs" == "$top"/* ]]; then
    rel=${abs#"$top"/}
    [[ "$abs" != "$top" ]] || return 0
  else
    # Outside the checkout. Config under ~/.claude and the gate markers are off limits, except
    # the memory folders; everything else (/tmp, build output) is not the repo's business.
    case "$abs" in
      "$HOME"/.claude/projects/*/memory | "$HOME"/.claude/projects/*/memory/* | "$HOME"/.claude/wattwise-memory | "$HOME"/.claude/wattwise-memory/*) return 0 ;;
      "$HOME"/.claude | "$HOME"/.claude/*) block "$raw is Claude configuration and may not be edited" ;;
    esac
    case "$abs" in
      "$state_dir" | "$state_dir"/*)
        [[ "$role" == orchestrator ]] || block "$raw is the epic gate state; only the orchestrator writes it"
        ;;
    esac
    [[ "$role" == orchestrator && "$orch_subagent" == 0 && "$tool" != Bash ]] &&
      block "the orchestrator may edit only docs/epic-runs/**, docs/epics.md and docs/implemented.md ($raw is outside the repository)"
    return 0
  fi
  base=${rel##*/}

  case "/$rel" in
    */.claude/* | */.github/* | */.husky/*) block "$rel is protected (.claude, .github and .husky are not editable by epic agents)" ;;
  esac
  case "$base" in
    .editorconfig | Directory.Build.* | eslint.config.js | stryker-config.json | lint-staged.config.js)
      block "$rel is protected (analyzer, lint and mutation configuration)"
      ;;
  esac

  case "$role" in
    orchestrator)
      ((orch_subagent == 0)) || return 0
      # Edit/Write: only the run records and the epic lists (the finished epic moves from
      # epics.md to implemented.md). Bash targets: same rule, inside the repository.
      [[ "$rel" == docs/epic-runs/* || "$rel" == docs/epics.md || "$rel" == docs/implemented.md ]] ||
        block "the orchestrator may write only under docs/epic-runs/**, docs/epics.md and docs/implemented.md ($rel)"
      ;;
    coder)
      case "/$rel" in
        */docs/epic-runs/*) block "$rel is a run record; only the orchestrator writes those" ;;
        */backend/tests/* | */e2e/*) block "$rel is a test; tests belong to the test writer. If a test is wrong, return needs-decision" ;;
      esac
      case "$base" in
        *.test.ts | *.test.tsx) block "$rel is a test; tests belong to the test writer. If a test is wrong, return needs-decision" ;;
      esac
      ;;
  esac
  return 0
}

# Destination of a push refspec: strips +, src: and refs/heads/.
is_main_ref() {
  local ref=${1#+}
  ref=${ref##*:}
  ref=${ref#refs/heads/}
  [[ "$ref" == main ]]
}

check_push() {
  local -a words=("$@")
  local i push=-1
  for i in "${!words[@]}"; do
    if [[ "${words[$i]}" == push ]]; then
      push=$i
      break
    fi
  done
  ((push >= 0)) || return 0
  local -a positional=()
  local w
  for w in "${words[@]:push+1}"; do
    case "$w" in
      --mirror | --all) block "git push $w could update main" ;;
      -*) ;;
      *) positional+=("$w") ;;
    esac
  done
  local -a refspecs=("${positional[@]:1}")
  local ref
  for ref in "${refspecs[@]}"; do
    if is_main_ref "$ref"; then block "pushing to main is not allowed ($ref); main only changes through a merged pull request"; fi
  done
  if ((${#refspecs[@]} == 0)); then
    local branch
    branch=$(git -C "$cwd" branch --show-current 2>/dev/null || true)
    [[ "$branch" != main ]] || block "pushing the current branch (main) is not allowed"
  fi
}

check_pr_merge() {
  local -a words=("$@")
  local i merge=-1
  for i in "${!words[@]}"; do
    if [[ "${words[$i]}" == merge && "${words[$((i - 1))]:-}" == pr ]]; then
      merge=$i
      break
    fi
  done
  ((merge >= 0)) || return 0
  local selector="" skip=0 w
  for w in "${words[@]:merge+1}"; do
    if ((skip)); then
      skip=0
      continue
    fi
    case "$w" in
      -b | --body | -t | --subject | -F | --body-file | -A | --author-email | --match-head-commit | -R | --repo) skip=1 ;;
      -*) ;;
      *)
        selector=$w
        break
        ;;
    esac
  done
  local json base head oid
  if ! json=$(cd "$cwd" && gh pr view ${selector:+"$selector"} --json headRefOid,baseRefName,headRefName 2>/dev/null); then
    block "could not read the pull request (${selector:-current branch}); merging is blocked when its base is unknown"
  fi
  base=$(jq -r '.baseRefName // empty' <<<"$json")
  head=$(jq -r '.headRefName // empty' <<<"$json")
  oid=$(jq -r '.headRefOid // empty' <<<"$json")
  [[ "$base" == main ]] || return 0
  if [[ "$head" != epic/* ]]; then
    block "only an epic/<E> pull request may merge into main from an epic agent ($head)"
  fi
  local epic=${head#epic/} gate
  gate="$state_dir/epic-$epic.gate"
  if [[ ! -f "$gate" ]]; then
    block "the epic gate has not passed for $epic (no $gate); run the epic gate first"
  fi
  if [[ -z "$oid" || "$(tr -d '[:space:]' <"$gate")" != "$oid" ]]; then
    block "the epic gate passed on a different head than the PR's ($oid); rerun the epic gate on the final head"
  fi
}

check_gh_api() {
  local -a words=("$@")
  local method="" write=0 i w
  for i in "${!words[@]}"; do
    w=${words[$i]}
    case "$w" in
      -X | --method) method=${words[$((i + 1))]:-} ;;
      -X?*) method=${w#-X} ;;
      --method=*) method=${w#--method=} ;;
      -f | -F | --field | --raw-field | --input) write=1 ;;
    esac
  done
  method=${method^^}
  [[ -z "$method" ]] && ((write)) && method=POST
  [[ -n "$method" && "$method" != GET ]] || return 0
  local joined="${words[*]}"
  if [[ "$joined" =~ /pulls/[0-9]+/merge || "$joined" =~ /merges || "$joined" =~ /git/refs ]]; then
    block "merging or moving refs through gh api would bypass the epic gate"
  fi
}

# Best-effort: file targets of Bash writers in one segment.
bash_write_targets() {
  local -a words=("$@")
  local i w cmd="" skip_script=0 target
  # Redirections: > file, >> file, >file (not 2>&1, &>/dev/null is a target too).
  for i in "${!words[@]}"; do
    w=${words[$i]}
    if [[ "$w" =~ ^[0-9]*\>\>?$ ]]; then
      echo "${words[$((i + 1))]:-}"
    elif [[ "$w" =~ ^[0-9]*\>\>?(.+)$ ]]; then
      target=${BASH_REMATCH[1]}
      [[ "$target" == \&* ]] || echo "$target"
    fi
  done
  for i in "${!words[@]}"; do
    w=${words[$i]}
    case "$w" in
      tee | sed | cp | mv | rm | install | truncate | dd | perl) cmd=$w ;;
      *) continue ;;
    esac
    local -a args=("${words[@]:i+1}")
    local -a files=()
    local a inplace=0
    for a in "${args[@]}"; do
      case "$a" in
        -i* | --in-place*) inplace=1 ;;
      esac
    done
    case "$cmd" in
      tee)
        for a in "${args[@]}"; do [[ "$a" == -* ]] || files+=("$a"); done
        ;;
      sed | perl)
        ((inplace)) || continue
        skip_script=1
        for a in "${args[@]}"; do
          [[ "$a" == -* ]] && continue
          if ((skip_script)); then
            skip_script=0
            continue
          fi
          files+=("$a")
        done
        ;;
      cp | install)
        local last=""
        for a in "${args[@]}"; do [[ "$a" == -* ]] || last=$a; done
        [[ -z "$last" ]] || files+=("$last")
        ;;
      mv | rm | truncate)
        for a in "${args[@]}"; do [[ "$a" == -* ]] || files+=("$a"); done
        ;;
      dd)
        for a in "${args[@]}"; do [[ "$a" == of=* ]] && files+=("${a#of=}"); done
        ;;
    esac
    ((${#files[@]} == 0)) || printf '%s\n' "${files[@]}"
  done
}

case "$tool" in
  Edit | Write | MultiEdit)
    file=$(jq -r '.tool_input.file_path // empty' <<<"$input")
    [[ -n "$file" ]] || exit 0
    check_path "$file"
    ;;
  Bash)
    command=$(jq -r '.tool_input.command // empty' <<<"$input")
    [[ -n "$command" ]] || exit 0
    segments=$(sed -E 's/(&&|\|\||;|\|)/\n/g' <<<"$command")
    while IFS= read -r segment; do
      [[ -n "${segment//[[:space:]]/}" ]] || continue
      clean=${segment//\"/}
      clean=${clean//\'/}
      read -ra words <<<"$clean"
      ((${#words[@]} > 0)) || continue

      has_git=0 has_gh=0
      for w in "${words[@]}"; do
        [[ "$w" == git ]] && has_git=1
        [[ "$w" == gh ]] && has_gh=1
      done
      ((has_git)) && check_push "${words[@]}"
      if ((has_gh)); then
        check_pr_merge "${words[@]}"
        for i in "${!words[@]}"; do
          if [[ "${words[$i]}" == api && "${words[$((i - 1))]:-}" == gh ]]; then
            check_gh_api "${words[@]:i+1}"
            break
          fi
        done
      fi

      while IFS= read -r target; do
        [[ -n "$target" && "$target" != /dev/null && "$target" != /dev/std* && "$target" != /dev/fd/* ]] || continue
        # Skip things that are clearly not paths (flags values, substitutions).
        # shellcheck disable=SC2016 # literal $( and backtick, not expansions
        [[ "$target" != *'$('* && "$target" != '`'* ]] || continue
        check_path "$target"
      done < <(bash_write_targets "${words[@]}")
    done <<<"$segments"
    ;;
esac
exit 0
