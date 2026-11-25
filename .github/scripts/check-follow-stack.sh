#!/usr/bin/env bash
# Asserts tools/follow-stack.ts and the husky hooks that call it (deploy/docs/development-stack.md,
# "Following git"). Needs git, sh and node: docker and op are stubs on PATH, the repo is a throwaway one.
#   bash .github/scripts/check-follow-stack.sh
set -euo pipefail

root="$(git rev-parse --show-toplevel)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

stubs="$work/stubs"
bare="$work/bare" # a PATH with git, sh and node only: no docker
repo="$work/repo"
mkdir -p "$stubs" "$bare" "$repo/tools" "$repo/.husky"

for tool in git sh; do
  ln -s "$(command -v "$tool")" "$stubs/$tool"
  ln -s "$(command -v "$tool")" "$bare/$tool"
done
# node: a wrapper around the real binary, so a version manager's shim needs nothing else on PATH.
node_real="$(node -p 'process.execPath')"
printf '#!/bin/sh\nexec "%s" "$@"\n' "$node_real" > "$stubs/node"
chmod +x "$stubs/node"
cp "$stubs/node" "$bare/node"

# docker: `inspect` prints STUB_SRC and exits with STUB_INSPECT_EXIT (0 by default).
cat > "$stubs/docker" << 'STUB'
#!/bin/sh
[ "$1" = inspect ] || exit 0
[ "${STUB_INSPECT_EXIT:-0}" = 0 ] || exit "$STUB_INSPECT_EXIT"
printf '%s' "${STUB_SRC-}"
STUB
# op: logs its working directory and arguments, then exits with STUB_OP_EXIT (0 by default).
cat > "$stubs/op" << 'STUB'
#!/bin/sh
printf '%s | %s\n' "$(pwd -P)" "$*" >> "$STUB_LOG"
exit "${STUB_OP_EXIT:-0}"
STUB
chmod +x "$stubs/docker" "$stubs/op"

cp "$root/tools/follow-stack.ts" "$root/tools/follow-stack-rules.ts" "$repo/tools/"
for hook in post-merge post-checkout post-rewrite; do cp "$root/.husky/$hook" "$repo/.husky/"; done

git -C "$repo" init -q -b main
git -C "$repo" config user.email check@example.invalid
git -C "$repo" config user.name check
git -C "$repo" config commit.gpgsign false
git -C "$repo" add -A
git -C "$repo" commit -qm initial
repo_real="$(cd "$repo" && pwd -P)"
ln -s "$repo_real" "$work/link" # a symlinked bind source must still match
other="$work/other"
mkdir -p "$other"

expected_cmd='run --env-file deploy/development/compose.env -- docker compose -f deploy/docker-compose.development.yml up -d --wait'
log="$work/op.log"
out="$work/out.txt"
passed=0
fail() {
  echo "FAIL: $*" >&2
  [ ! -s "$out" ] || sed 's/^/  | /' "$out" >&2
  exit 1
}
pass() {
  passed=$((passed + 1))
  echo "ok: $*"
}

# run_hook <PATH> <hook> [hook args...]: runs the hook the way husky does (sh -e from the toplevel),
# with the stack stubs configured by the caller's STUB_* variables. Sets status; output goes to $out.
run_hook() {
  local path="$1" hook="$2"
  shift 2
  : > "$log"
  status=0
  (cd "$repo" && env -u CLAUDECODE -u CODEX_THREAD_ID PATH="$path" STUB_LOG="$log" "${AGENT_ENV[@]}" sh -e ".husky/$hook" "$@") > "$out" 2>&1 || status=$?
}
expect_rebuild() { # description
  [ "$status" = 0 ] || fail "$1: exit code $status, expected 0"
  [ "$(wc -l < "$log")" = 1 ] || fail "$1: op was called $(wc -l < "$log") times, expected once"
  [ "$(cat "$log")" = "$repo_real | $expected_cmd" ] || fail "$1: op called as '$(cat "$log")'"
  grep -qF 'rebuilding' "$out" || fail "$1: no rebuilding message"
  pass "$1"
}
expect_nothing() { # description
  [ "$status" = 0 ] || fail "$1: exit code $status, expected 0"
  [ ! -s "$log" ] || fail "$1: op was called: $(cat "$log")"
  [ ! -s "$out" ] || fail "$1: printed output"
  pass "$1"
}

export STUB_SRC="$repo_real"
unset STUB_INSPECT_EXIT STUB_OP_EXIT
AGENT_ENV=() # extra environment for run_hook, to play an agent session

# 1. The stack runs from this folder.
run_hook "$stubs" post-merge 0
expect_rebuild "post-merge rebuilds when the stack runs from this checkout"
run_hook "$stubs" post-checkout aaa bbb 1
expect_rebuild "post-checkout rebuilds on a branch switch"
run_hook "$stubs" post-rewrite rebase
expect_rebuild "post-rewrite rebuilds after a rebase"
STUB_SRC="$work/link" run_hook "$stubs" post-merge 0
expect_rebuild "a symlinked bind source counts as this checkout"

# 2. The stack runs from another folder.
STUB_SRC="$other" run_hook "$stubs" post-merge 0
expect_nothing "another folder: nothing runs"

# 3. No stack to follow.
STUB_INSPECT_EXIT=1 run_hook "$stubs" post-merge 0
expect_nothing "no container: nothing runs"
STUB_SRC='' run_hook "$stubs" post-merge 0
expect_nothing "no bind mount: nothing runs"
run_hook "$bare" post-merge 0
expect_nothing "no Docker: nothing runs"

# 4. Hooks that must not act.
run_hook "$stubs" post-checkout aaa bbb 0
expect_nothing "a file checkout does nothing"
run_hook "$stubs" post-checkout aaa aaa 1
expect_nothing "a switch that keeps HEAD does nothing"
run_hook "$stubs" post-rewrite amend
expect_nothing "post-rewrite after an amend does nothing"
mkdir "$repo/.git/rebase-merge"
run_hook "$stubs" post-checkout aaa bbb 1
expect_nothing "post-checkout in the middle of a rebase does nothing"
run_hook "$stubs" post-rewrite rebase
expect_rebuild "post-rewrite still rebuilds at the end of a rebase, whose state is still set"
rmdir "$repo/.git/rebase-merge"

# 5. The rebuild fails: the exact command is printed and git is not blocked.
STUB_OP_EXIT=1 run_hook "$stubs" post-merge 0
[ "$status" = 0 ] || fail "a failed rebuild: hook exit code $status, expected 0"
[ "$(wc -l < "$log")" = 1 ] || fail "a failed rebuild: op was not called once"
grep -qxF 'follow-stack: rebuild failed; run it yourself:' "$out" || fail "a failed rebuild: no hint line"
grep -qxF "  mise run up" "$out" || fail "a failed rebuild: the command is not printed"
pass "a failed rebuild prints the command and the hook exits 0"

# 6. An agent session prints the command and never calls op.
expect_hint_only() { # description
  [ "$status" = 0 ] || fail "$1: exit code $status, expected 0"
  [ ! -s "$log" ] || fail "$1: op was called: $(cat "$log")"
  grep -qxF 'follow-stack: the Development stack runs from this checkout; rebuild it with:' "$out" || fail "$1: no hint line"
  grep -qxF "  mise run up" "$out" || fail "$1: the command is not printed"
  pass "$1"
}
AGENT_ENV=(CLAUDECODE=1)
run_hook "$stubs" post-merge 0
expect_hint_only "Claude Code (CLAUDECODE=1): the command is printed, op is not called"
AGENT_ENV=(CODEX_THREAD_ID=thread)
run_hook "$stubs" post-checkout aaa bbb 1
expect_hint_only "Codex (CODEX_THREAD_ID): the command is printed, op is not called"
AGENT_ENV=(CLAUDECODE=1)
STUB_SRC="$other" run_hook "$stubs" post-merge 0
expect_nothing "an agent session in another folder: nothing is printed"
AGENT_ENV=()

# 7. Real git, wired like husky: hooks under core.hooksPath that run the repo's .husky hooks.
hooks="$work/hooks"
mkdir -p "$hooks"
for hook in post-merge post-checkout post-rewrite; do
  printf '#!/bin/sh\nexec sh -e "%s/.husky/%s" "$@"\n' "$repo_real" "$hook" > "$hooks/$hook"
  chmod +x "$hooks/$hook"
done
git -C "$repo" config core.hooksPath "$hooks"

g_raw() { # real git in the repo, with the stubs on PATH
  (cd "$repo" && env -u CLAUDECODE -u CODEX_THREAD_ID PATH="$stubs" STUB_LOG="$log" git "$@")
}
g() { g_raw "$@" 2>> "$work/setup-stderr.txt"; } # setup steps: the hooks' messages go to a file
real_git() { # description, expected number of op calls, git arguments...
  local what="$1" calls="$2"
  shift 2
  : > "$log"
  g_raw "$@" > "$out" 2>&1 || fail "$what: git $* failed"
  [ "$(wc -l < "$log")" = "$calls" ] || fail "$what: op was called $(wc -l < "$log") times, expected $calls"
  if [ "$calls" = 0 ]; then
    ! grep -qF 'follow-stack:' "$out" || fail "$what: the hook printed a message"
  else
    grep -qF 'follow-stack: the Development stack runs from this checkout; rebuilding' "$out" || fail "$what: no rebuilding message"
  fi
  pass "$what"
}
commit_file() { # file
  echo "$1" > "$repo/$1"
  g add "$1"
  g commit -qm "$1"
}

g switch -qc feat
commit_file feat
g switch -q main
real_git "git switch to another commit rebuilds once" 1 switch -q feat
real_git "git switch back rebuilds once" 1 switch -q main
real_git "git checkout of a file does nothing" 0 checkout -q -- tools/follow-stack.ts
real_git "git switch to the same commit does nothing" 0 switch -q -c same
g switch -q main
real_git "a fast-forward git merge rebuilds once" 1 merge -q --ff-only feat
g switch -qc topic main~1
commit_file topic
real_git "git rebase rebuilds once, at the end" 1 rebase -q main

echo "all $passed checks passed"
