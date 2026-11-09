# Handoff: epic-run experiments on tethys

Temporary. Run by a fresh Claude session in its own Orca worktree `epic-X0` on tethys, started with `.claude/scripts/start-epic.sh X0`. **Never run it in the setup worktree.** It needs [tethys-setup.md](tethys-setup.md) done (all boxes ticked, parity check clean). Background: [agentic-delivery.md](../agentic-delivery.md), [deploy/docs/agent-host.md](../../deploy/docs/agent-host.md). Deleted at the end (see "Cleanup").

Goal: prove the epic-run machinery works on tethys with a throwaway epic `epic/X0` and a trivial story, record 10 results, and fix what fails. Nothing here touches real epics.

## Sessions

- **O** is the orchestrator: the tmux session `epic-X0` from `start-epic.sh`, running `claude --agent orchestrator` in the `epic-X0` worktree. Its hooks let it edit only `docs/epic-runs/**`, `docs/epics.md` and `docs/implemented.md`; it cannot push to main or edit `.claude/`, `.github/`, other docs or code.
- **P** is a plain terminal as `corvus` in the `epic-X0` worktree, outside Claude, for steps the orchestrator's guards would block (force pushes, edits to `.claude/`, rulesets). Where a step says P, the human or an agent with plain Bash does it.
- **F** is the fixer: a second, plain `claude` session in its own worktree on `chore/epic-experiments` (created in step 0), the only place where the experiments log and fixes are written. Only F commits to that branch.

Fill each **Result** line right after its experiment: `pass`, `fail` or `workaround`, with the numbers asked for. Put raw evidence (command output, session ids) under the experiment, with secrets removed. If an experiment fails, F may fix it on `chore/epic-experiments`; note the fix under the experiment and rerun it once.

## 0. Prepare

1. Record versions: `claude --version`, `orca --version`, `git log -1 --oneline` -> note here: `_______`.
2. Start O: `.claude/scripts/start-epic.sh X0`. It checks gh, op and docker, runs the parity check and a memory sync (warnings only), creates and pushes `epic/X0` from `origin/main`, creates the Orca worktree `epic-X0`, links the memory folder, opens tmux `epic-X0`, and prints the `/goal` line. Attach with `tmux attach -t epic-X0`. Verify: `git branch --show-current` in the worktree is `epic/X0`, `tmux ls` lists `epic-X0`.
3. Create F's worktree from the setup worktree: `orca worktree create --repo path:$PWD --name epic-experiments --branch chore/epic-experiments --base-branch origin/main --setup skip`, then start `claude` there.
4. In O, write the throwaway plan by hand (`/plan-epic` needs an entry in `docs/epics.md`; X0 has none). Tell O: "Create `docs/epic-runs/X0/` with `plan.md`, `S0.1.md`, `S0.2.md`, `state.json` and `log.md` from `.claude/skills/plan-epic/references/story-plan.md` and `state.schema.json`. Two independent backend-routed stories, no model change. S0.1: append one sentence to `docs/experiments-sandbox.md` (create it); acceptance: the file ends with that sentence; no tests (docs only). S0.2: reserved for experiment 2. Commit signed and push to `epic/X0`." Say `approved`. Verify: `jq . docs/epic-runs/X0/state.json` parses and `git log origin/epic/X0 -1 --show-signature` shows a good signature.

## Experiments

### 1. Worktree subagents base on the epic branch

Goal: `worktree.baseRef: head` makes a worktree subagent start from the orchestrator's current commit (`epic/X0`), with the repo rules and frontmatter hooks active.

Steps:

1. In O, have it write `docs/epic-runs/X0/marker.txt` and commit it **without pushing**.
2. Tell O to dispatch a `backend-coder` (model `sonnet`, background) with the brief: "Do not implement. Report: `git log -1 --oneline`, whether `docs/epic-runs/X0/marker.txt` exists, the first heading of `CLAUDE.md` under 'Working rules', and the exact output of `git push origin HEAD:main`." The hook-blocked push is the point.

Pass: the report's HEAD equals `epic/X0`'s unpushed tip and `marker.txt` exists (the subagent branched from HEAD, not from origin), the CLAUDE.md rule is quoted, and the push is denied with an `epic-agent-guard (coder)` message.

Result: `_______`

### 2. Fast gate blocks, counts, gives up; timeout behaves

Goal: `story-fast-gate` blocks a failing coder up to 3 times, then lets it stop. (`onFailure` does not apply to Stop hooks, so it is not tested; the 3-attempt counter and the timeout are.)

Steps:

1. In O, create `story/X0/S0.2` from `epic/X0`, push it, and dispatch a `backend-coder` on it with the brief: "Add `backend/src/WattWise.Domain/Probe.cs` containing a badly indented class and an unused `using System.Linq;`. Do not fix formatting. Finish with `STATUS: done` and an evidence table." Set S0.2 `running` in `state.json`.
2. In P while it runs: `ls ${XDG_RUNTIME_DIR:-/tmp}/wattwise-gate/` and `cat` the file for the session id.
3. Timeout: in P, on `epic/X0`, edit the Stop hook in `.claude/agents/backend-coder.md`: set the `story-fast-gate.sh` command to `sleep 60; exit 2` and its `timeout` to `20`; commit and push to `epic/X0` (this branch is deleted later). Dispatch a new coder on `story/X0/S0.1` with the brief "append the sentence, then stop".

After the check, **revert that edit** (`git revert` in P, push), or experiment 9's coder runs the slow hook.

Pass: in step 1 the counter reaches 3 (the file holds `3` or is removed at the end), the coder is blocked with the format failure at most 3 times, then stops with the "gate still failing after 3 attempts" text, and O receives its final message. In step 3 the coder ends about 20 s after trying to stop (not 60 s, not a hang), and O still receives a report.

Result: `_______`

### 3. Auto mode, from user settings, in the orchestrator and background subagents

Goal: `permissions.defaultMode: auto` works only from user settings; verify the mirrored user-level value applies to `claude --agent orchestrator` and to background subagents, and that their frontmatter hooks fire.

Steps:

1. In P: `jq -r .permissions.defaultMode ~/.claude/settings.json` (expect `auto`, mirrored by `sync-user-claude.sh`) and `grep -n defaultMode .claude/settings.json backend/.claude/settings.json` (expect no match).
2. In O, run a Bash command and write a file under `docs/epic-runs/X0/`; check the status line / `/status` shows auto mode and that no permission prompt appeared.
3. Ask O to write a file `.claude/probe` (must be denied by `epic-agent-guard --role orchestrator`; quote the message).
4. Dispatch a background `backend-coder` (brief: "run `ls`, then `git push origin HEAD:main`, report both results verbatim; do not implement"). It must not stall waiting for a prompt.

Pass: user-level auto is set and project settings do not set it; O and the coder run Bash without prompts; the orchestrator's `.claude` write and the coder's push to main are both denied by `epic-agent-guard`, and the coder's denial text appears in its report to O. Fallback if auto does not apply: add `permissionMode: auto` to the epic agents' frontmatter and document `--permission-mode auto` for interactive sessions (F fixes this).

Result: `_______`

### 4. Rulesets and token limits

Goal: the GitHub rulesets hold against the owner, the agents' token and force pushes.

Steps (P, outside Claude; the repo-guard hook blocks force pushes made through Claude):

1. Owner does not bypass. As the owner on vidar (the tethys token has no admin rights): `gh api repos/Brijunas/watt-wise/rulesets --jq '.[].id'`, then for each id `gh api repos/Brijunas/watt-wise/rulesets/<id> --jq '{name,enforcement,bypass:.bypass_actors,can:.current_user_can_bypass}'`.
2. A token push touching workflows is rejected: in P, `git switch -c story/X0/ci-test`, add `.github/workflows/probe.yml` (any valid YAML), `git commit -S -m probe`, `git push origin story/X0/ci-test`.
3. Direct signed push to the epic branch works: on `epic/X0` make a trivial signed commit in `docs/epic-runs/X0/log.md`, `git push origin epic/X0`, then `gh api repos/Brijunas/watt-wise/commits/$(git rev-parse HEAD) --jq .commit.verification.verified`.
4. Force-push is rejected: `git push --force origin HEAD~1:epic/X0`.

Pass: `bypass` is `[]` and `can` is `never` for every ruleset; step 2 is rejected naming the workflow permission; step 3 pushes and prints `true`; step 4 is rejected by the ruleset.

Result: `_______`

### 5. `/goal` at launch, and "needs you" notifications

Goal: know whether `/goal` can be given at launch (the current script prints it to paste), and whether Orca on vidar or the phone flags an orchestrator question.

Steps:

1. In a throwaway tmux session in P: `tmux new -d -s goal-probe -c "$PWD" 'claude --agent orchestrator "/goal Reply done and stop after one turn."'`, attach, and see whether the goal is set (`/goal` shows it) or the text was just a prompt. Kill the session afterwards.
2. In O, paste the `/goal` line from `start-epic.sh` and confirm `/goal` shows it.
3. Tell O: "Ask me one trivial question with AskUserQuestion." Watch Orca on vidar (the tethys target) and the phone.

Pass: record which of launch argument or paste works (paste is the current behavior; if the argument works, F changes `start-epic.sh` to pass it), and whether Orca and the phone each show "needs you" within a minute.

Result: `_______`

### 6. Stryker `--since` on Application

Goal: the time and score of a story-mode mutation run against a full run.

Steps (P): `git switch -c story/X0/mutation epic/X0`; add `backend/src/WattWise.Application/Probe.cs` with `public static class Probe { public static int Next(int a) => a + 1; }`; commit. Then:

```bash
time dotnet stryker -f backend/stryker-config.json -O backend/StrykerOutput/Application -p WattWise.Application.csproj -tp backend/tests/WattWise.Application.Tests/WattWise.Application.Tests.csproj --since:origin/epic/X0
time dotnet stryker -f backend/stryker-config.json -O backend/StrykerOutput/Application-full -p WattWise.Application.csproj -tp backend/tests/WattWise.Application.Tests/WattWise.Application.Tests.csproj
```

Pass: the `--since` run mutates only `Probe.cs` and finishes much faster than the full run; the full run meets the project's score target. Record both durations and both scores (the `--since` score is low: the probe has no test).

Result: `_______`

### 7. Testcontainers and the Compose stack, two parallel builds

Goal: two parallel story gates fit in tethys's memory.

Steps (P): with the Development stack up, create a second worktree `git worktree add ../epic-X0-b epic/X0 --detach`. Start sampling in P: `(while sleep 5; do free -m | awk '/Mem:/{print $3, $7}'; done > "$TMPDIR/mem-x0.log") &`. In both worktrees at once run `dotnet test --solution backend/WattWise.slnx`. Stop the sampler and read the peak used memory (first column, MB).

Pass: both runs pass, no container fails to start, and peak used memory is below 13 GB. If it is above, a human adds zram (sudo) and the experiment is rerun; record both peaks.

Result: `_______`

### 8. 1Password reads per story

Goal: a story's reads stay far below the service account's rate limits.

Steps (P, with `~/.config/wattwise/env` sourced): run `op service-account ratelimit` before and after the whole of experiment 9's S0.1 run (dispatch to merge). Compute the read difference.

Pass: reads per story are recorded, and (reads per story x 2 parallel x stories per hour for a 10-story epic) is below the hourly read limit shown.

Result: `_______`

### 9. Resume from `state.json` after a kill

Goal: a killed orchestrator picks up where it left off, and the happy path works end to end (also the data for 8).

Steps:

1. In O, run `/run-epic X0` with only S0.1 left (S0.2 `parked`, with the experiment-2 brief). Let it dispatch S0.1; while the coder is `running`, in P: `tmux kill-session -t epic-X0`, and `pkill -f 'claude --agent orchestrator'` if it still runs.
2. `.claude/scripts/start-epic.sh X0`, paste the `/goal` line, run `/run-epic X0`.
3. Let it finish the story: gate (`gate-runner`), reviewers, `/merge-to-epic X0 S0.1` through `merge-runner`.

Pass: after the restart O reads `state.json`, finds S0.1 `running`, checks its branch and worktree and resumes (no second `story/X0/S0.1`, no duplicate coder), and S0.1 ends `merged` with one signed squash commit on `origin/epic/X0` (`git log origin/epic/X0 -3 --show-signature`). Do not let O run step 6 of `run-epic` (the epic end): stop it after the merge and say so, since X0 must never reach main.

Result: `_______`

### 10. User config and memory on tethys

Goal: a session in the epic worktree sees the same user-level setup as vidar, and memory flows both ways.

Steps:

1. In O: "Quote the first line of your user CLAUDE.md, list the enabled plugins, and list the entries of the project memory index." Compare with vidar's `~/.claude/CLAUDE.md`, `claude plugin list` and `MEMORY.md`. Check the link: `readlink ~/.claude/projects/$(pwd | sed -E 's#[/.]#-#g')/memory` -> `/home/corvus/.claude/wattwise-memory`.
2. Tethys to vidar: on tethys `echo probe > ~/.claude/wattwise-memory/_x0-from-tethys.md`; on vidar `deploy/agent-host/sync-memory.sh`, then `ls` vidar's memory folder.
3. Vidar to tethys: on vidar create `_x0-from-vidar.md` in `~/.claude/projects/-home-caleb-code-personal-watt-wise/memory/`, run `sync-memory.sh`, `ls` tethys's folder.
4. Delete both probe files on one side and sync again; confirm they disappear on the other.

Pass: CLAUDE.md, plugins and memory entries match vidar's; the symlink is in place for the epic worktree; both probe files appear on the other machine after one sync; deletions propagate; no `*.conflict.md` appears.

Result: `_______`

## Record and ship

F, in the `chore/epic-experiments` worktree:

1. Add one row per experiment to the "Experiments log" table in `docs/agentic-delivery.md` (`#`, question, result with numbers), with the versions from step 0 above the table. Put any fix (hooks, agents, scripts, docs, `start-epic.sh`) on the same branch, and rerun `.claude/hooks/tests/run.sh` if a hook changed.
2. Do the cleanup below in the same branch, then commit signed (no `--no-verify`) and push.
3. **Announce to the user that you are about to ship**, then `/merge-to-main chore/epic-experiments full`.

## Cleanup

Run these (P, or F where it edits files) after the results are written:

1. Stop O: `tmux kill-session -t epic-X0`. Remove the worktrees: `git worktree remove --force` for `epic-X0`, `epic-X0-b`, any `story/X0/*` worktree (`git worktree list`), or `orca worktree rm`.
2. Delete the branches locally and on origin: `git branch -D epic/X0 $(git branch --list 'story/X0/*')`, then `git push origin --delete epic/X0 story/X0/S0.1 story/X0/S0.2 story/X0/ci-test story/X0/mutation` (skip any that does not exist). If a ruleset rejects a deletion, the human deletes it with their own login. Also delete `chore/tethys-setup-done` locally and on origin.
3. Remove the gate marker and counters: `rm -f ~/.local/state/wattwise/epic-X0.gate`, `rm -rf ${XDG_RUNTIME_DIR:-/tmp}/wattwise-gate`, `rm -rf backend/StrykerOutput`. Remove the memory probe files from experiment 10.
4. In F's branch: `git rm docs/handoffs/tethys-setup.md docs/handoffs/tethys-experiments.md` and delete any mention of them in `CLAUDE.md`.
5. Verify: `git worktree list`, `git branch -a | grep X0` and `git ls-remote --heads origin '*X0*'` show nothing; `ls docs/handoffs` fails after the merge.
