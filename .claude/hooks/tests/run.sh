#!/usr/bin/env bash
# Plain bash test runner for the guard hooks: feeds JSON to each hook and asserts the exit code
# (0 = allow, 2 = block). Run from anywhere: .claude/hooks/tests/run.sh
# gh, dotnet and pnpm are stubbed through PATH; the repos are throwaway repos in a temp folder.
set -uo pipefail

hooks=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

pass=0
fail=0

mkdir -p "$tmp/bin" "$tmp/state" "$tmp/run" "$tmp/cache"
export WATTWISE_STATE_DIR="$tmp/state"
export XDG_RUNTIME_DIR="$tmp/run"
export XDG_CACHE_HOME="$tmp/cache"
export HOME_REAL=$HOME

# --- stubs ---------------------------------------------------------------------------------
cat >"$tmp/bin/gh" <<'EOF'
#!/usr/bin/env bash
# Stub: `gh pr view … --json …` answers from STUB_BASE, STUB_HEAD and STUB_OID.
if [[ "$1 $2" == "pr view" ]]; then
  [[ -z "${STUB_GH_FAIL:-}" ]] || exit 1
  printf '{"baseRefName":"%s","headRefName":"%s","headRefOid":"%s"}\n' "$STUB_BASE" "$STUB_HEAD" "$STUB_OID"
  exit 0
fi
exit 0
EOF
for tool in dotnet pnpm; do
  cat >"$tmp/bin/$tool" <<'EOF'
#!/usr/bin/env bash
# Stub: fails when STUB_TOOLS_FAIL is set.
if [[ -n "${STUB_TOOLS_FAIL:-}" ]]; then
  echo "stub $(basename "$0") $*: boom (error CS0000)" >&2
  exit 1
fi
exit 0
EOF
done
chmod +x "$tmp/bin/"*
export PATH="$tmp/bin:$PATH"

# --- repos -----------------------------------------------------------------------------------
mkrepo() { # dir branch
  git init -q -b main "$1"
  git -C "$1" -c user.name=t -c user.email=t@t -c commit.gpgsign=false commit -q --allow-empty -m init
  [[ "$2" == main ]] || git -C "$1" switch -q -c "$2"
}
mkrepo "$tmp/on-main" main
mkrepo "$tmp/on-feature" feature
mkrepo "$tmp/on-epic" epic/E4
mkrepo "$tmp/repo" story/E4/s1
repo="$tmp/repo"

# --- helpers ---------------------------------------------------------------------------------
bash_json() { jq -nc --arg c "$1" --arg d "$2" '{tool_name:"Bash", tool_input:{command:$c}, cwd:$d, session_id:"t"}'; }
file_json() { jq -nc --arg t "$1" --arg f "$2" --arg d "$3" '{tool_name:$t, tool_input:{file_path:$f}, cwd:$d, session_id:"t"}'; }

# expect <allow|block> <label> <json> <hook script> [hook args…]
expect() {
  local want=$1 label=$2 json=$3 script=$4
  shift 4
  local code=0 err
  err=$("$hooks/$script" "$@" <<<"$json" 2>&1 >/dev/null) || code=$?
  local wantcode=0
  [[ "$want" == block ]] && wantcode=2
  if ((code == wantcode)); then
    pass=$((pass + 1))
    echo "ok   $script ${*:-} [$want] $label"
  else
    fail=$((fail + 1))
    echo "FAIL $script ${*:-} [$want, got exit $code] $label"
    [[ -z "$err" ]] || echo "     $err"
  fi
}

guard() { expect "$1" "$2" "$(bash_json "$2" "${3:-$repo}")" repo-guard.sh; }
agent_bash() { expect "$2" "$3" "$(bash_json "$3" "${4:-$repo}")" epic-agent-guard.sh --role "$1"; }
agent_file() { expect "$2" "$1: $3 $4" "$(file_json "$3" "$repo/$4" "$repo")" epic-agent-guard.sh --role "$1"; }

# --- repo-guard: allowed (everything the story flow and the epic flow run) --------------------
echo "# repo-guard allow"
for c in \
  'git push origin story/E4/s1' \
  'git push -u origin feature' \
  'git push origin epic/E4' \
  'git push origin --delete story/E4/s1' \
  'git push origin Brijunas/numbfish' \
  'git push --force-with-lease origin feature' \
  'git push -f origin story/E4/s1' \
  'git push --force-with-lease' \
  'gh pr create --base main --title "E4: accounts" --body-file report.md' \
  'gh pr merge 12 --squash --delete-branch' \
  'gh pr merge 12 --merge' \
  'gh pr merge epic/E4 --merge --delete-branch' \
  'gh pr view 12 --json headRefOid,baseRefName' \
  'git merge --squash story/E4/s1' \
  'git commit -S -m "S4.3: email outbox"' \
  'git commit -S -m "S4.3: x" -m "body"' \
  'git switch main && git pull --ff-only' \
  'git branch -D story/E4/s1' \
  'git worktree remove ../story-E4-s1' \
  'gh api repos/o/r/rulesets' \
  'gh api -X GET repos/o/r/rulesets' \
  'gh api repos/o/r/pulls/12' \
  'gh api repos/o/r/branches/main/protection' \
  'dotnet ef migrations add Foo --project backend/src/WattWise.Infrastructure' \
  'op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- migrate'; do
  guard allow "$c"
done
guard allow 'git push --force-with-lease' "$tmp/on-feature"

# --- repo-guard: blocked --------------------------------------------------------------------
echo "# repo-guard block"
for c in \
  'git commit --no-verify -m x' \
  'git push --no-verify origin feature' \
  'git add . && git commit -m x --no-verify' \
  'git push --force origin main' \
  'git push -f origin main' \
  'git push --force-with-lease origin main' \
  'git push --force-with-lease origin epic/E4' \
  'git push origin +main' \
  'git push origin +HEAD:main' \
  'git push origin +HEAD:refs/heads/epic/E4' \
  'git push -fu origin main' \
  'git push --mirror --force origin' \
  'gh api -X POST repos/o/r/rulesets -f name=x' \
  'gh api --method PUT repos/o/r/branches/main/protection' \
  'gh api -X DELETE repos/o/r/rulesets/1' \
  'gh api repos/o/r/rulesets -f name=x' \
  'gh api -X PATCH repos/o/r' \
  'gh api --method=PATCH repos/o/r -f has_issues=false'; do
  guard block "$c"
done
guard block 'git push --force' "$tmp/on-main"
guard block 'git push -f' "$tmp/on-epic"
guard block 'git push origin +HEAD' "$tmp/on-main"

# --- epic-agent-guard: files, all roles ------------------------------------------------------
echo "# epic-agent-guard files"
for role in orchestrator coder runner; do
  for f in .claude/settings.json backend/.claude/rules/x.md .github/workflows/ci.yml .husky/pre-commit \
    .editorconfig backend/Directory.Build.props backend/Directory.Build.targets eslint.config.js \
    backend/stryker-config.json lint-staged.config.js; do
    for tool in Edit Write MultiEdit; do
      agent_file "$role" block "$tool" "$f"
    done
  done
done
agent_file runner allow Write docs/notes.md
agent_file coder allow Edit backend/src/WattWise.Domain/Accounts/Account.cs
agent_file coder allow Write frontend/src/features/about/about-panel.tsx
agent_file coder allow Edit docs/development.md

# --- epic-agent-guard: orchestrator ----------------------------------------------------------
echo "# epic-agent-guard orchestrator"
agent_file orchestrator allow Write docs/epic-runs/E4/state.json
agent_file orchestrator allow Edit docs/epic-runs/E4/log.md
agent_file orchestrator block Edit backend/src/WattWise.Domain/Accounts/Account.cs
agent_file orchestrator allow Edit docs/epics.md
agent_file orchestrator allow Write docs/implemented.md
agent_file orchestrator block Write docs/product.md
agent_file orchestrator block Edit docs/epics.md.bak
agent_file orchestrator block Write docs/sub/epics.md
agent_file orchestrator block Write backend/tests/WattWise.Domain.Tests/X.cs
expect block "orchestrator outside the repo" "$(file_json Write /etc/hosts "$repo")" epic-agent-guard.sh --role orchestrator
expect block "orchestrator ~/.claude" "$(file_json Edit "$HOME/.claude/settings.json" "$repo")" epic-agent-guard.sh --role orchestrator
expect allow "orchestrator memory" "$(file_json Write "$HOME/.claude/projects/-x-y/memory/note.md" "$repo")" epic-agent-guard.sh --role orchestrator

# --- epic-agent-guard: coder -----------------------------------------------------------------
echo "# epic-agent-guard coder"
agent_file coder block Write docs/epic-runs/E4/plan.md
agent_file coder block Edit backend/tests/WattWise.Domain.Tests/AccountTests.cs
agent_file coder block Edit frontend/src/about.test.ts
agent_file coder block Edit packages/core/src/x.test.tsx
agent_file coder block Write frontend/e2e/login.ts
agent_file coder block Write e2e/login.ts
agent_file coder allow Write backend/tests/../src/WattWise.Domain/X.cs
expect block "coder ~/.claude" "$(file_json Edit "$HOME/.claude/settings.json" "$repo")" epic-agent-guard.sh --role coder
expect block "coder gate marker" "$(file_json Write "$tmp/state/epic-E4.gate" "$repo")" epic-agent-guard.sh --role coder
expect allow "coder /tmp" "$(file_json Write /tmp/scratch.txt "$repo")" epic-agent-guard.sh --role coder

# --- epic-agent-guard: Bash ------------------------------------------------------------------
echo "# epic-agent-guard bash"
for role in orchestrator coder runner; do
  agent_bash "$role" block 'git push origin main'
  agent_bash "$role" block 'git push origin HEAD:main'
  agent_bash "$role" block 'git push origin HEAD:refs/heads/main'
  agent_bash "$role" block 'git push -u origin main'
  agent_bash "$role" block 'git push origin :main'
  agent_bash "$role" block 'git push --all'
  agent_bash "$role" block 'git push' "$tmp/on-main"
  agent_bash "$role" allow 'git push origin story/E4/s1'
  agent_bash "$role" allow 'git push origin epic/E4'
  agent_bash "$role" allow 'git push'
  agent_bash "$role" allow 'git fetch origin && git merge --squash story/E4/s1'
  agent_bash "$role" block 'echo x > .claude/settings.json'
  agent_bash "$role" block 'echo x >> .github/workflows/ci.yml'
  agent_bash "$role" block 'cat y | tee .husky/pre-commit'
  agent_bash "$role" block 'sed -i s/a/b/ .husky/pre-commit'
  agent_bash "$role" block 'cp /tmp/x backend/Directory.Build.props'
  agent_bash "$role" block 'mv /tmp/x eslint.config.js'
  agent_bash "$role" block 'rm backend/stryker-config.json'
  agent_bash "$role" allow 'echo x > /tmp/out.txt'
  agent_bash "$role" allow 'dotnet build backend/WattWise.slnx 2>&1 | tail -n 5'
  agent_bash "$role" allow 'ls -la .claude'
  agent_bash "$role" allow 'git commit -S -m "x"'
  agent_bash "$role" block 'gh api -X PUT repos/o/r/pulls/5/merge'
done
agent_bash coder block 'echo x > docs/epic-runs/E4/log.md'
agent_bash coder block 'cp /tmp/x backend/tests/WattWise.Domain.Tests/X.cs'
agent_bash coder block 'sed -i s/a/b/ frontend/src/a.test.ts'
agent_bash coder block "echo x > $tmp/state/epic-E4.gate"
agent_bash coder allow 'echo x > backend/src/WattWise.Domain/X.cs'
agent_bash runner block "echo abc > $tmp/state/epic-E4.gate"
agent_bash orchestrator allow 'echo x > docs/epic-runs/E4/log.md'
agent_bash orchestrator allow "echo abc > $tmp/state/epic-E4.gate"
agent_bash orchestrator allow 'echo x >> docs/implemented.md'
agent_bash orchestrator block 'echo x > backend/src/WattWise.Domain/X.cs'

# --- epic-agent-guard: merge gate (gh stubbed) -------------------------------------------------
echo "# epic-agent-guard merge gate"
export STUB_BASE=main STUB_HEAD=epic/E4 STUB_OID=abc123
rm -f "$tmp/state/epic-E4.gate"
for role in orchestrator coder runner; do
  agent_bash "$role" block 'gh pr merge 7 --merge'
done
echo "abc123" >"$tmp/state/epic-E4.gate"
for role in orchestrator coder runner; do
  agent_bash "$role" allow 'gh pr merge 7 --merge --delete-branch'
  agent_bash "$role" allow 'gh pr merge epic/E4 --merge'
  agent_bash "$role" allow 'gh pr merge --merge'
done
echo "stale999" >"$tmp/state/epic-E4.gate"
agent_bash runner block 'gh pr merge 7 --merge'
STUB_OID=abc123 STUB_HEAD=story/E4/s1 agent_bash runner block 'gh pr merge 7 --squash'
echo "abc123" >"$tmp/state/epic-E4.gate"
STUB_GH_FAIL=1 agent_bash runner block 'gh pr merge 7 --merge'
STUB_BASE=epic/E4 STUB_HEAD=story/E4/s1 agent_bash runner allow 'gh pr merge 7 --squash'
STUB_BASE=main STUB_HEAD=epic/E5 agent_bash runner block 'gh pr merge 8 --merge'
agent_bash runner allow 'gh pr create --base main --title "E4" --body-file report.md'

# --- epic-agent-guard: argument errors -------------------------------------------------------
echo "# epic-agent-guard usage"
expect block "no role" "$(bash_json 'ls' "$repo")" epic-agent-guard.sh
expect block "bad role" "$(bash_json 'ls' "$repo")" epic-agent-guard.sh --role wizard

# --- story-fast-gate -------------------------------------------------------------------------
echo "# story-fast-gate"
base_commit=$(git -C "$repo" rev-parse HEAD)
git -C "$repo" update-ref refs/remotes/origin/epic/E4 "$base_commit"
mkdir -p "$repo/backend/src" "$repo/frontend"
echo '{"name":"frontend"}' >"$repo/frontend/package.json"

gate_json() { jq -nc --arg d "$1" --arg s "$2" --argjson a "$3" '{cwd:$d, session_id:$s, stop_hook_active:$a, hook_event_name:"SubagentStop"}'; }
gate() { # want label json
  expect "$1" "$2" "$3" story-fast-gate.sh
}

gate allow "not a story branch" "$(gate_json "$tmp/on-feature" s0 false)"
gate allow "story branch without changes" "$(gate_json "$repo" s1 false)"

echo 'class A {}' >"$repo/backend/src/A.cs"
gate allow "backend change, tools pass" "$(gate_json "$repo" s2 false)"
export STUB_TOOLS_FAIL=1
gate block "backend change, attempt 1" "$(gate_json "$repo" s3 false)"
gate block "backend change, attempt 2" "$(gate_json "$repo" s3 true)"
gate block "backend change, attempt 3" "$(gate_json "$repo" s3 true)"
gate allow "backend change, gives up after 3 blocks" "$(gate_json "$repo" s3 true)"
gate block "backend change, new stop request restarts the count" "$(gate_json "$repo" s3 false)"
unset STUB_TOOLS_FAIL
gate allow "backend change fixed after a block" "$(gate_json "$repo" s3 true)"

rm -rf "$repo/backend"
echo 'export {}' >"$repo/frontend/a.ts"
gate allow "frontend change, tools pass" "$(gate_json "$repo" s5 false)"
export STUB_TOOLS_FAIL=1
gate block "frontend change, lint fails" "$(gate_json "$repo" s6 false)"
unset STUB_TOOLS_FAIL

out=$(STUB_TOOLS_FAIL=1 "$hooks/story-fast-gate.sh" <<<"$(gate_json "$repo" s7 false)" 2>&1 >/dev/null || true)
if [[ "$out" == *"pnpm lint failed"* && "$out" == *"boom"* ]]; then
  pass=$((pass + 1))
  echo "ok   story-fast-gate.sh failure output has the failing step and a tail"
else
  fail=$((fail + 1))
  echo "FAIL story-fast-gate.sh failure output: $out"
fi
for _ in 1 2 3; do STUB_TOOLS_FAIL=1 "$hooks/story-fast-gate.sh" <<<"$(gate_json "$repo" s8 true)" >/dev/null 2>&1 || true; done
msg=$(STUB_TOOLS_FAIL=1 "$hooks/story-fast-gate.sh" <<<"$(gate_json "$repo" s8 true)" 2>&1 >/dev/null || true)
if [[ "$msg" == *"gate still failing after 3 attempts"* ]]; then
  pass=$((pass + 1))
  echo "ok   story-fast-gate.sh says 'gate still failing after 3 attempts'"
else
  fail=$((fail + 1))
  echo "FAIL story-fast-gate.sh give-up message: $msg"
fi

echo
echo "passed: $pass, failed: $fail"
((fail == 0))
