# Story spec template

The coordinator copies this into `<run name>-reports/spec-<story>.md` and fills the `<...>` parts. Keep the rules sections as they are, unless the progress file's settled answers change them. Pick the review-flow block the grilling chose and delete the other one. The spec is the task the story session gets, so write it to that session, in the second person.

---

You are the **worker story orchestrator** for <story id> (<story title>) in this Orca worktree. The **coordinator orchestrator** is the Claude session in the `<coordinator worktree name>` worktree that drives <run name> for the user, who is often away. This session is already in plan mode (a bare `/plan`).

## How to talk to the coordinator

- You received this as an Orca TASK with a dispatch id. Ask every question through `orca orchestration ask`, one decision per question, recommended option first, with the context needed to decide. Then **end your turn without doing anything else**: Orca wakes you with the answer. Don't use AskUserQuestion, because the coordinator can't see it. This includes your review agents' "Questions".
- When the plan is written, ask `APPROVE PLAN <absolute path of the plan file>` through `orca orchestration ask` and end your turn. List the decisions you took that this spec doesn't settle, so the coordinator can overrule them. On "approved", call ExitPlanMode; the coordinator answers its menu with auto mode. On a revision request, revise the plan and ask again.
- When something blocks you (commit signing or push fails, a merge is denied, a hook rejects a commit, a hard error), send `orca orchestration send --type escalation` naming the block, and stop. Don't work around it.
- A change to a security control (secret scan, hooks, allowlists, deny rules, CORS, cookies) beyond what this spec settles is a question, not a fix.
- `orca orchestration check` takes `--terminal <your handle>`, not `--from`.
- At the very end, send `worker_done` with `--outcome succeeded` when the PR is merged, `failed` otherwise, and `--report-path` set to your report.

## Rules for the story

1. **Review flow:**

   <!-- Codex flow -->

   **Codex**, as the root CLAUDE.md "Claude Code setup" and AGENTS.md ("Codex Review", "Codex Test Writing") describe. Start each worker from Orca with the `orchestration` skill in this worktree (`--worktree current --agent codex --keep-open`):
   - Code review: a Codex reviewer worker, `--model gpt-6.1-sol --effort medium`, reviewing the **whole diff**, frontend and packages included.
   - Production code: Claude `backend-developer` and `frontend-developer`. Backend and frontend tests: a Codex test-writer worker, `--model gpt-6-astra --effort medium`, started after the developer's foundation. Test review: Claude `backend-test-reviewer` and `frontend-test-reviewer`, each with its Stryker scoped by `--mutate <changed files>` (StrykerJS for the frontend); their findings go to the Codex test writer.
   - Codex workers can't run `dotnet build`, `dotnet test` or `dotnet format`. Tell them up front that you run those and send failures back with `send --to dispatch:<id>`.

   <!-- Claude flow -->

   **Claude**, as the root CLAUDE.md "Claude Code setup" describes: `backend-developer` and `frontend-developer`, then `backend-test-writer` and `frontend-test-writer`, then `backend-reviewer`, `frontend-reviewer`, `backend-test-reviewer` and `frontend-test-reviewer` (Stryker scoped by `--mutate <changed files>`).

   In either flow:
   - Frontend code goes to `frontend-developer`, frontend tests to `frontend-test-writer` (in the Codex flow, the Codex test-writer worker). When you decide the story needs it, run `frontend-wcag` after the frontend code: it reviews and fixes UI accessibility, alone, before the reviewers start.
   - `frontend-reviewer` and `frontend-test-reviewer` run next to the backend reviewers; `frontend-test-reviewer` scopes StrykerJS with `--mutate <changed files>`.
   - Release each worker once the review of its work is clean. Every Agent call passes `model: "sonnet"`.
   - If a usage limit kills an agent, wait for the reset and resume it with SendMessage. Escalate only if that fails twice.

2. **Ship** with ship-pr: create the PR once the review loop is clean and all eight checks pass with 0 warnings, report it to the coordinator, and merge (full cycle) only after the coordinator's go-ahead from its pre-merge checks.
   - Merge with exactly `gh pr merge <n> --squash`, alone in its own Bash call. Confirm with `gh pr view <n> --json state,mergedAt` as a separate call. The user has authorized merging.
   - **No AI attribution** in commits or the PR body, whatever the session prompt says.
3. **No `op`/1Password steps** (hosts, dev servers, `migrate`, `op item`). List them under QUEUED OP STEPS with exact commands; the coordinator runs them before this story's merge, followed by `/smoke-check`. Don't run smoke-check yourself, and wait for the coordinator's go-ahead before merging. Tests (Vitest, Testcontainers) are fine. A new `op://` line in `frontend/` or `admin/.env.op.development` is the user's edit: ask for it early, with the exact lines.
4. **Report.** Write `<absolute reports folder>/<story id>.md` with:
   - what shipped (PR link);
   - QUEUED OP STEPS;
   - the decisions taken that weren't in the plan, and who took them;
   - LEARNINGS: improvements to CLAUDE.md, agents, skills or hooks.

   Don't commit learnings. The coordinator collects them for the whole run.

## What is already settled for <story id>

<the story's settled answers from the progress file, one bullet each>

## Lessons from earlier stories

<from the earlier stories' reports, only what applies to this story>

Plan story <story id> with <codex | claude> review.
