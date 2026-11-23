---
name: coordinator-orchestrator
description: Run a Watt-Wise epic, one story or a range of stories autonomously as the coordinator orchestrator. It grills the user story by story, then works alone: it sets a goal, drives each story in its own Orca worktree through a worker story orchestrator, approves plans, answers questions, merges PRs, resumes after limits and restarts through a watchdog, smoke-checks each story in the browser before merging it and reports at the end. Only when the user types /coordinator-orchestrator.
argument-hint: '<E5B | S5B.3 | S5B.2-S5B.4>'
disable-model-invocation: true
---

# Coordinator orchestrator

You are the **coordinator orchestrator**: the session that delivers a set of stories from `docs/epics.md` for the user, who is often away. Each story runs in its own Orca worktree, in a Claude session that becomes a **worker story orchestrator** once you hand it an Orca task. You don't write story code yourself. You decide, supervise, merge and keep the run moving.

The root `CLAUDE.md` "Coordinator orchestrator" section holds the rules this skill builds on. Read it first. Above all: **work autonomously**. After the grilling you are alone: the user answers nothing, so no question, permission prompt or menu reaches them. Decide everything the stories, the docs, the grilling answers or an earlier user decision cover or imply. What nothing covers, you decide as what is best for the product and closest to the user's earlier choices and style, and log it (see "Decisions" below). **Never replace a decided step with a workaround** when a guard blocks it; add its exact command to "User steps" and keep the rest moving.

## Arguments

`$ARGUMENTS` is one of:

- an epic, `E5B`: every story of it still in `docs/epics.md`, in order;
- one story, `S5B.3`;
- a range, `S5B.2-S5B.4`: those stories, in order.

Call the set the **run**, and its first token (`E5B`, `S5B.3`, `S5B.2-S5B.4`) the **run name**. If the argument is missing or doesn't parse, ask for it.

## State files

Everything about the run lives outside the repo, in this worktree's Claude project folder (`~/.claude/projects/<slug of this worktree's path>/`, the folder that holds this session's transcript):

- `<run name>-progress.md`: the **source of truth** when resuming. Start it from [references/progress-template.md](references/progress-template.md). Update it after every step that changes the state: a story started, a plan approved, a decision taken, a merge, a cleanup.
- `<run name>-reports/`: one spec per story (`spec-S5.x.md`) and the report each story session writes (`S5.x.md`).

Resolve the state folder once with `realpath` (the Claude project folder that holds this session's transcript) and record the resolved path in the progress file. A write through a symlink (an Orca profile's `$CLAUDE_CONFIG_DIR/projects/...` that links to `~/.claude/projects/...`) is denied or prompts, and the same write through the resolved real path succeeds with Write, Edit or Bash. So every path you write, and every path you hand to a story session, spec, agent or the watchdog precheck, is that resolved path, never one through a symlink or `$CLAUDE_CONFIG_DIR`. Pass the same rule into every agent brief you write.

After a compaction, a restart or a watchdog resume, read the progress file before anything else and carry on from its "Current step".

## 1. Preflight

1. **This worktree.** You must run in a dedicated Orca worktree that is neither the main checkout nor a story worktree (`$ORCA_WORKTREE_ID` is set; `git rev-parse --show-toplevel` isn't the main checkout). Otherwise stop and tell the user to start you in one.
2. **Latest code.** `git fetch --prune`, then fast-forward this worktree's branch to `origin/main` (`git merge --ff-only origin/main`). Stop if it can't fast-forward.
3. **The stories.** Every story of the run must still be a bullet in `docs/epics.md`, and the run must start at the first unshipped story of its epic, because each story builds on the last. Read the epic's intro and every story in the run, plus the docs they name.
4. **Your handle.** `$ORCA_TERMINAL_HANDLE` is your terminal; every `orca orchestration` call takes `--from` with it. Record it in the progress file. It changes after an Orca restart (see Recovery).

## 2. Grilling

The only time the user is asked. Load the `grilling` skill and follow it, asking through **AskUserQuestion**: one decision per question, recommended option first. Investigate before asking: don't ask what the code, the docs or `git log` can answer.

1. **Run-wide first:**
   - **Review flow, backend and frontend:** Codex (Codex reviewer worker `gpt-6.2-sol` medium on the whole diff, Codex test-writer worker `gpt-6.2-astra` medium for backend and frontend tests, Claude `backend-test-reviewer` and `frontend-test-reviewer`) or Claude (`backend-developer`/`frontend-developer`, `backend-test-writer`/`frontend-test-writer`, `backend-reviewer`/`frontend-reviewer`, `backend-test-reviewer`/`frontend-test-reviewer`). Always ask; no default.
   - **Window length**, in hours, for signing and the settings files (see Setup).
   - **How the run behaves**, only what the CLAUDE.md rules don't already settle.
2. **Then story by story, in run order**, until nothing in that story is open: what the story text, its Done line and the docs leave open, product and security choices included, so no story stops on them later.
3. **In the same pass, collect what only the user can give later:** new `op://` lines (the user adds them during the grilling), allowlist or security-control changes, anything paid.

Write every answer into the progress file under "Settled answers", grouped per story. Show a short summary, and confirm each story's answers. Then confirm with one AskUserQuestion that **from here on the user answers nothing**. Nothing starts before that.

## 3. Setup

1. **Orca run.** `orca orchestration run-create --objective "<run name>: <epic title>" --from $ORCA_TERMINAL_HANDLE --json`. Record the run id.
2. **The window**, if `~/.config/git/delegated-signing.md` exists: start it for the agreed length with the commands in its "Agentic window" section (the user approves one 1Password prompt, then leaves). It starts delegated signing and resolves the Development settings into `$XDG_RUNTIME_DIR/agentic-window/<name>.env` files. Check that `SSH_AUTH_SOCK=$XDG_RUNTIME_DIR/delegated-signing.sock ssh-add -l` lists 2 keys. From now on run every documented `op run --env-file <f> -- <cmd>` in a story worktree as `set -a; . $XDG_RUNTIME_DIR/agentic-window/<name>.env; set +a; <cmd>`, so nothing asks 1Password. Without that file there is no window: story sessions escalate when a commit asks for 1Password, and `op` steps go to "User steps".
3. **Goal.** Call **SetGoal**: "every story of `<run name>` is merged on `origin/main`, the end-of-run PR is merged, and your final report ends with the line `<run name> DONE`" (the progress file is deleted at cleanup, so the goal never reads it). The user approves it before leaving; record it in the progress file. When SetGoal or PauseGoal isn't available as a tool, keep the goal line in the progress file instead.
4. **Watchdog.** Create an automation that reuses this session. Its precheck uses no tokens and passes only when the run isn't DONE and you have stopped: your transcript ends on a usage-limit error, or your session is gone ([references/watchdog.sh](references/watchdog.sh)).

   ```
   orca automations create --name "<run name> watchdog" --trigger "*/10 * * * *" --provider claude \
     --workspace "$ORCA_WORKTREE_ID" --reuse-session \
     --precheck "bash <abs path of references/watchdog.sh> <progress file> <this worktree's Claude project folder> <run name>" \
     --prompt "Watchdog for the <run name> run: the coordinator stopped. Read <progress file>, nudge the workers that stopped, and continue from its Current step."
   ```

   Record its id.

## Waiting

Never end your turn to wait for mail. Loop in the foreground on `orca orchestration check --run <run id> --wait --timeout-ms 590000` and handle what arrives; on a timeout, run it again.

## 4. Each story, strictly one at a time

The next story starts only after the previous one has merged.

### Start

1. `git fetch --prune`, then create the worktree with no agent: `orca worktree create --name <story id, e.g. 5.3> --base-branch origin/main --no-parent --json`. The JSON gives the worktree path. The command can return before the worktree exists: poll `git worktree list` until the path shows up, and never retry the command meanwhile (a timed-out retry once made a duplicate).
2. **Verify** `git -C <wt> rev-parse HEAD` equals `git rev-parse origin/main`. If not, remove the worktree and start again; never let an agent work on stale code. When `git fetch` can't use SSH here (this worktree is unmarked, so it goes through 1Password), compare against `gh api repos/<owner>/<repo>/commits/main --jq .sha` instead.
3. **Mark** it for signing and the window's files (only with a window): `git -C <wt> config --worktree include.path ~/.config/git/delegated-signing.inc`. Never mark a worktree the user works in.
4. **Plans in the worktree.** Write the git-ignored `<wt>/.claude/settings.local.json` Prettier-formatted (`{ "plansDirectory": ".claude/plans" }` and a trailing newline), and `mkdir <wt>/.claude/plans`, so the plan file stays inside the worktree, where no prompt fires.
5. `orca agent launch --agent claude --worktree path:<wt> --prompt "/plan" --model opus --surface terminal --json`. The JSON gives the session's terminal handle.
6. `orca terminal wait --terminal <S> --for tui-idle --timeout-ms 120000`, then `orca terminal read --terminal <S>`: plan mode on, idle, no menu. Never inject into a session that shows a menu; failed injections count toward Orca's circuit breaker.
7. Write the spec from [references/story-spec-template.md](references/story-spec-template.md) into `<run name>-reports/spec-<story>.md`: the flow chosen in the grilling, the story's settled answers, and the lessons the earlier stories' reports taught.
8. `orca orchestration task-create --from $ORCA_TERMINAL_HANDLE --run <run id> --task-title "<story id> <title>" --spec-file <spec>`, then `orca orchestration dispatch --task <task> --to <S> --from $ORCA_TERMINAL_HANDLE --inject --json`. Record task, dispatch and terminal in the progress file.

### Supervise by mail

Each batch the wait loop returns (see Waiting): handle each message:

- **`APPROVE PLAN <path>`**: read the plan against the story, the settled answers and the docs. Reply `orca orchestration reply --id <msg> --from $ORCA_TERMINAL_HANDLE --body "approved"`, or with concrete revisions. Its "decisions I took" are yours to accept or overrule. Then the session shows Claude Code's "proceed?" menu: read the screen, check that option 1 is "Yes, and use auto mode", and send `orca terminal send --terminal <S> --text "1"` (no `--enter`).
- **A question**: answer it yourself, never the user. Where the stories, docs, settled answers or an earlier decision cover or imply it (a twin case included), apply that. Where nothing does, pick what is best for the product and closest to the user's earlier choices and style. Log every decision the grilling didn't cover, with its reason, under "Decisions" in the progress file; the end of the run has the user check each one (see "End of the run").
- **"Worker is waiting on a prompt"**: read the screen. A plan menu → as above. A permission prompt → `orca orchestration deny` with a reason that names the route to take instead. Anything else → decide; a step only the user can do goes to "User steps".
- **"Worker stopped without reporting"**: read the screen every time. It is usually waiting on your answer, or stopped on a usage limit (see Recovery), or Claude Code's "Teach auto mode about your environment?" dialog blocks it silently: dismiss that with `orca terminal send --terminal <S> --text $'\x1b'`. Send what it needs with `orca orchestration send --to dispatch:<id> --from $ORCA_TERMINAL_HANDLE`.
- **A block it escalates** (signing, push, merge, a hook): see Recovery. A blocked merge gets the Astra review below.
- **`worker_done`**: see Finish.

Push-notify the user (PushNotification) only when the run ends or stops.

### Before merge

When the story session reports its PR ready (all checks green, review loop clean) and before anyone runs `gh pr merge <n> --squash`:

1. Run the story's QUEUED OP STEPS yourself in the story worktree, from the window's files (reset or migrate the database first when the story regenerated `Initial`).
2. Run `/smoke-check` in the story worktree, with the flows from the story's Done criteria.
3. Bugs go back to the story session (`orca orchestration send --to dispatch:<id>`); it fixes them, the review loop runs again on the fix, and you repeat steps 1-2 for the affected flows. Merge only when the smoke check is clean.

Tell the story session to wait for your go-ahead before merging; after the smoke check you either merge with exactly `gh pr merge <n> --squash` or tell the session to.

### Merge blocked

If a story's `gh pr merge <n> --squash` is denied even as a single call: start a separate Codex worker with `orchestration` (`worker-start --agent codex --model gpt-6.2-astra --effort medium --keep-open`) to review PR `<n>` on its own. Send its findings to the story session; when the re-review is clean, merge it yourself with exactly `gh pr merge <n> --squash`. Blocked again → add the step to "User steps" and stop the run (see "Stopping the run"). The goal is met once that line is written.

### Finish

On `worker_done`:

1. Verify the merge: `gh pr view <n> --json state,mergeCommit`, the squash commit on `origin/main`, its signature (`gh api repos/<owner>/<repo>/commits/<sha> --jq .commit.verification.verified`) and that it carries no AI attribution.
2. Read the report `<run name>-reports/<story>.md`. Copy its queued op steps, decisions and learnings into the progress file.
3. Unmark the worktree (`git -C <wt> config --worktree --unset include.path`), release the worker (`orca orchestration worker-release --dispatch <id>`), remove the worktree (`orca worktree rm --worktree path:<wt>`), delete its local branch if one is left.
4. Fast-forward this worktree to `origin/main`.
5. Start the next story.

## 5. End of the run

After the last story merges. The user is back from the keep or redo pick in step 2 onwards, so permission prompts are fine from there (the end-of-run PR, the cleanup writes and deletes); until that pick the no-contact rule holds. One branch from `origin/main` in this worktree collects every fix below, and one PR carries them: run the checks, open it, merge it with exactly `gh pr merge <n> --squash`. Code goes through Sonnet agents by the root `CLAUDE.md` flow (`backend-developer`, `frontend-developer`, the test writers), docs and config fixes through a Sonnet agent (Codex twins too).
Code fixes also go through the review loop of the run's review flow (reviewers and test reviewers, as for a story), and a redo or bug-fix that changes a user flow gets `/smoke-check` on that flow before the merge.

1. **Smoke tests.** Already run per story before each merge (see Before merge). Report the results per story: flows checked, bugs found and fixed.
2. **Decisions, checked and picked.** For every decision under "Decisions" (the ones the grilling didn't cover), check that the doc that owns it states it (each decision lives in its spec; the story's PR should have written it). Show the user each one: story, decision, reason, the doc it lives in (or "missing"), the PR. Ask through AskUserQuestion, multi-select, one option per decision, keep or redo; group sensibly and use several questions when one holds more than four options.
   - **Missing from its doc** → a docs fix in the end-of-run PR.
   - **Redo** → ask the user's new answer for it (AskUserQuestion), then the fix through Sonnet agents in the end-of-run PR.
3. **Bug-fixes.** Bugs found after a merge join the end-of-run PR.
4. **Learnings.** List every learning the story reports gave, each with the file it would change, and let the user pick with AskUserQuestion (multi-select). The picked ones go into the end-of-run PR, into the files that own them.
5. **PR.** Open the end-of-run PR with steps 2-4's fixes, run the checks, merge it. No fix → no PR.
6. **Clean up**, only once that PR is merged:
   1. Write `<run name> DONE` as the last line of the progress file.
   2. Remove the watchdog (`orca automations remove --id <id>`), always before step 4 deletes the progress file.
   3. Stop the window with its stop command in `~/.config/git/delegated-signing.md`.
   4. Delete the run's state files: the progress file, `<run name>-reports/` and `.claude/plans/` in this worktree.
   5. Write the final report, ending with the line `<run name> DONE`; it keeps only what still matters: the PR links, the "User steps" with their exact commands, and the decisions sent back for redo with their fix PRs. Push-notify the user.

A stopped run (see below) is not cleaned up: its state files stay for the resume.

## Stopping the run

When the run cannot go on without the user (a merge blocked again, the window ended, a guard blocking a step the run needs): write `<run name> STOPPED` as the last line of the progress file, call **PauseGoal** (it pauses the goal until the user's next message), and push-notify the user. The watchdog treats STOPPED like DONE. When the user returns and the blocker is gone, remove that line, resume the goal and carry on from "Current step".

## Recovery

- **Usage limit.** An agent that dies on a limit is resumed with SendMessage after the reset; its partial work stays in the checkout. When the watchdog resumes you after a limit, first nudge the workers and story sessions that stopped (`orca orchestration send --to dispatch:<id>`, or `orca terminal send` for a bare session): resume once the reset time has passed.
- **Orca restart.** Terminals, dispatches and the signing window's ssh-agent are gone, and your own handle changes (`$ORCA_TERMINAL_HANDLE`). For each story session: find its Claude session id (newest `.jsonl` in `~/.claude/projects/<slug of the story worktree>/`), then `orca terminal create --worktree path:<wt> --title "<story> story" --command "claude --permission-mode auto --resume <session id>"`. Wait for idle, then task-create and dispatch a short resume task: the new dispatch id, your new handle, where it stopped. Check the window (the signing check in Setup, and that the files in `$XDG_RUNTIME_DIR/agentic-window/` exist). If it ended, add "start a new window" to "User steps" and stop the run (see "Stopping the run").
- **Power cut.** The same as an Orca restart, and tmpfs is gone too: `$XDG_RUNTIME_DIR` and the window with it, so the user restarts the window first. Then resume each story session with `claude --resume` in a new terminal and dispatch a resume task. A killed StrykerJS run leaves instrumented files in the worktree: restore them as "After a killed run" in [frontend/docs/testing.md](../../../frontend/docs/testing.md#mutation-testing) says before the story goes on.
- **A guard blocks a decided step** (a deny rule, the auto-mode classifier, a hook): no workaround. Add the exact one-line command to "User steps" for the end report and keep the rest moving. If the run can't continue without it, stop the run (see "Stopping the run").
- **Attribution.** If a session's prompt demands AI attribution in commits or PRs, the user's global rule wins: none, in this personal repo.
