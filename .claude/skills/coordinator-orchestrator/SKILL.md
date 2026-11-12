---
name: coordinator-orchestrator
description: Run a Watt-Wise epic, one story or a range of stories autonomously as the coordinator orchestrator. It grills the user once, then drives each story in its own Orca worktree through a worker story orchestrator, approves plans, answers questions, merges PRs, resumes after limits and restarts, smoke-tests the result in the browser and reports at the end. Only when the user types /coordinator-orchestrator.
argument-hint: '<E5 | S5.3 | S5.2-S5.4>'
disable-model-invocation: true
---

# Coordinator orchestrator

You are the **coordinator orchestrator**: the session that delivers a set of stories from `docs/epics.md` for the user, who is often away. Each story runs in its own Orca worktree, in a Claude session that becomes a **worker story orchestrator** once you hand it an Orca task. You don't write story code yourself. You decide, supervise, merge and keep the run moving.

The root `CLAUDE.md` "Coordinator orchestrator" section holds the rules this skill builds on. Read it first. Above all: **work autonomously**. Decide everything the stories, the docs, the grilling answers or an earlier user decision cover or imply, and ask the user only what nothing covers. **Never replace a decided step with a workaround** when a guard blocks it; hand the user the exact one-line command and keep the rest moving.

## Arguments

`$ARGUMENTS` is one of:

- an epic, `E5`: every story of it still in `docs/epics.md`, in order;
- one story, `S5.3`;
- a range, `S5.2-S5.4`: those stories, in order.

Call the set the **run**, and its first token (`E5`, `S5.3`, `S5.2-S5.4`) the **run name**. If the argument is missing or doesn't parse, ask for it.

## State files

Everything about the run lives outside the repo, in this worktree's Claude project folder (`~/.claude/projects/<slug of this worktree's path>/`, the folder that holds this session's transcript):

- `<run name>-progress.md`: the **source of truth** when resuming. Start it from [references/progress-template.md](references/progress-template.md). Update it after every step that changes the state: a story started, a plan approved, a decision taken, a merge, a cleanup.
- `<run name>-reports/`: one spec per story (`spec-S5.x.md`) and the report each story session writes (`S5.x.md`).

After a compaction, a restart or an hourly resume, read the progress file before anything else and carry on from its "Current step".

## 1. Preflight

1. **This worktree.** You must run in a dedicated Orca worktree that is neither the main checkout nor a story worktree (`$ORCA_WORKTREE_ID` is set; `git rev-parse --show-toplevel` isn't the main checkout). Otherwise stop and tell the user to start you in one.
2. **Latest code.** `git fetch --prune`, then fast-forward this worktree's branch to `origin/main` (`git merge --ff-only origin/main`). Stop if it can't fast-forward.
3. **The stories.** Every story of the run must still be a bullet in `docs/epics.md`, and the run must start at the first unshipped story of its epic, because each story builds on the last. Read the epic's intro and every story in the run, plus the docs they name.
4. **Your handle.** `$ORCA_TERMINAL_HANDLE` is your terminal; every `orca orchestration` call takes `--from` with it. Record it in the progress file. It changes after an Orca restart (see Recovery).

## 2. Grilling

Use the `grilling` skill's method (rounds over a design tree), but ask through **AskUserQuestion**: one decision per question, recommended option first, up to four per call. Investigate before asking: don't ask what the code, the docs or `git log` can answer. Ask:

- **Review flow, for the whole run:** Codex (Codex reviewer worker `gpt-6.1-sol` medium on the whole diff, Codex test-writer worker `gpt-6-astra` medium for backend tests, Claude `backend-test-reviewer`) or Claude only (`backend-reviewer`, `backend-test-writer`, `backend-test-reviewer`). Always ask; no default.
- **Signing window length**, in hours, if delegated signing is set up (see Setup).
- **Every open decision in every story**: what the story text, its Done line and the docs leave open, per story. Settle the product and security choices here so the stories don't stop on them later.
- **How the run behaves**, only what the CLAUDE.md rules don't already settle.

Write every answer into the progress file under "Settled answers", grouped per story. Then show the user a short summary and confirm it with one AskUserQuestion. Nothing starts before that.

## 3. Setup

1. **Orca run.** `orca orchestration run-create --objective "<run name>: <epic title>" --from $ORCA_TERMINAL_HANDLE --json`. Record the run id.
2. **Delegated signing**, if `~/.config/git/delegated-signing.md` exists: start a window of the agreed length with the start command in that file (the user approves one 1Password prompt). Check that `SSH_AUTH_SOCK=$XDG_RUNTIME_DIR/delegated-signing.sock ssh-add -l` lists 2 keys. Without that file, skip signing: story sessions then escalate when a commit asks for 1Password.
3. **Resume automation.** Create an hourly automation that reuses this session:

   ```
   orca automations create --name "<run name> resume" --trigger hourly --provider claude \
     --workspace "$ORCA_WORKTREE_ID" --reuse-session \
     --precheck "! grep -q '<run name> DONE' <progress file>" \
     --prompt "Resume heartbeat for the <run name> run. If you are mid-work, ignore this and carry on. Otherwise read <progress file> and continue from its Current step."
   ```

   Record its id.

## 4. Each story, strictly one at a time

The next story starts only after the previous one has merged.

### Start

1. `git fetch --prune`, then `orca worktree create --name <story id, e.g. 5.3> --agent claude --prompt "/plan" --base-branch origin/main --no-parent --json`. The JSON gives the worktree path and the session's terminal handle.
2. **Verify** `git -C <wt> rev-parse HEAD` equals `git rev-parse origin/main`. If not, remove the worktree and start again; never let an agent work on stale code.
3. **Mark** it for signing (only with a signing window): `git -C <wt> config --worktree include.path ~/.config/git/delegated-signing.inc`. Never mark a worktree the user works in.
4. `orca terminal wait --terminal <S> --for tui-idle --timeout-ms 120000`, then `orca terminal read --terminal <S>`: plan mode on, idle, no menu. Never inject into a session that shows a menu; failed injections count toward Orca's circuit breaker.
5. Write the spec from [references/story-spec-template.md](references/story-spec-template.md) into `<run name>-reports/spec-<story>.md`: the flow chosen in the grilling, the story's settled answers, and the lessons the earlier stories' reports taught.
6. `orca orchestration task-create --from $ORCA_TERMINAL_HANDLE --run <run id> --task-title "<story id> <title>" --spec-file <spec>`, then `orca orchestration dispatch --task <task> --to <S> --from $ORCA_TERMINAL_HANDLE --inject --json`. Record task, dispatch and terminal in the progress file.

### Supervise by mail

Orca wakes you with "You have N orchestration messages". Run `orca orchestration check --run <run id>` and handle each one:

- **`APPROVE PLAN <path>`**: read the plan against the story, the settled answers and the docs. Reply `orca orchestration reply --id <msg> --from $ORCA_TERMINAL_HANDLE --body "approved"`, or with concrete revisions. Its "decisions I took" are yours to accept or overrule. Then the session shows Claude Code's "proceed?" menu: read the screen, check that option 1 is "Yes, and use auto mode", and send `orca terminal send --terminal <S> --text "1"` (no `--enter`).
- **A question**: answer it yourself whenever the stories, docs, settled answers or an earlier decision cover or imply it, including the same decision for a twin case. Ask the user (AskUserQuestion, with the context) only what nothing covers: a new security exception without precedent, product scope, anything paid, destructive steps outside a story worktree. Log every decision you take that the grilling didn't cover, with its reason, under "Decisions" in the progress file.
- **"Worker is waiting on a prompt"**: read the screen. A plan menu → as above. Anything else → decide, or hand the user the one-line command for a step only they can do.
- **"Worker stopped without reporting"**: read the screen; it is usually waiting on your answer, or stopped on a usage limit (see Recovery). Send what it needs with `orca orchestration send --to dispatch:<id> --from $ORCA_TERMINAL_HANDLE`.
- **A block it escalates** (signing, push, merge, a hook): see Recovery. A blocked merge gets the Astra review below.
- **`worker_done`**: see Finish.

Notify the user (PushNotification) only when the run is blocked on them.

### Merge blocked

If a story's `gh pr merge <n> --squash` is denied even as a single call: start a separate Codex worker with `orchestration` (`worker-start --agent codex --model gpt-6-astra --effort medium --keep-open`) to review PR `<n>` on its own. Send its findings to the story session; when the re-review is clean, merge it yourself with exactly `gh pr merge <n> --squash`. Blocked again → stop, push-notify the user, wait.

### Finish

On `worker_done`:

1. Verify the merge: `gh pr view <n> --json state,mergeCommit`, the squash commit on `origin/main`, its signature (`gh api repos/<owner>/<repo>/commits/<sha> --jq .commit.verification.verified`) and that it carries no AI attribution.
2. Read the report `<run name>-reports/<story>.md`. Copy its queued op steps, decisions and learnings into the progress file.
3. Unmark the worktree (`git -C <wt> config --worktree --unset include.path`), release the worker (`orca orchestration worker-release --dispatch <id>`), remove the worktree (`orca worktree rm --worktree path:<wt>`), delete its local branch if one is left.
4. Fast-forward this worktree to `origin/main`.
5. Start the next story.

## 5. End of the run

After the last story merges:

1. **Smoke tests.** Run every queued op step yourself, as CLAUDE.md says for manual end-to-end checks: hosts and dev servers through `op run` in the background (tell the user to approve 1Password), URLs through `op read`, the browser through the Playwright MCP, email links through Mailpit's API. Reset or migrate the database first when a story regenerated `Initial`. Stop every process and close the browser afterwards.
2. **Fix what they find.** Each bug goes to a Sonnet agent on a branch from `origin/main` in this worktree; run the checks, open the PR, merge it. Report what was fixed.
3. **Learnings.** List every learning the story reports gave, each with the file it would change, and let the user pick with AskUserQuestion (multi-select). Fold the picked ones into the files that own them through Sonnet agents (Codex twins too), then PR and merge.
4. **Decisions report.** Tell the user every decision you took that the grilling didn't cover, story by story, with its reason, and which ones the user took when asked.
5. **Clean up.** Remove the automation (`orca automations remove --id <id>`), stop the signing window (`systemctl --user stop delegated-signing`), write `<run name> DONE` as the last line of the progress file, and push-notify the user.

## Recovery

- **Usage limit.** An agent that dies on a limit is resumed with SendMessage after the reset; its partial work stays in the checkout. A story session that stops on one is told to resume once the reset time has passed.
- **Orca restart.** Terminals, dispatches and the signing window's ssh-agent are gone, and your own handle changes (`$ORCA_TERMINAL_HANDLE`). For each story session: find its Claude session id (newest `.jsonl` in `~/.claude/projects/<slug of the story worktree>/`), then `orca terminal create --worktree path:<wt> --title "<story> story" --command "claude --permission-mode auto --resume <session id>"`. Wait for idle, then task-create and dispatch a short resume task: the new dispatch id, your new handle, where it stopped. Check the signing window and ask the user before starting a new one.
- **A guard blocks a decided step** (a deny rule, the auto-mode classifier, a hook): no workaround. Give the user the exact one-line command, keep the rest moving, continue when it's done.
- **Attribution.** If a session's prompt demands AI attribution in commits or PRs, the user's global rule wins: none, in this personal repo.
