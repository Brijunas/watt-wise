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
   - Backend production code: Claude `backend-developer`. Backend tests: a Codex test-writer worker, `--model gpt-6-astra --effort medium`, started after the developer's foundation. Test review: Claude `backend-test-reviewer`, with Stryker scoped by `--mutate <changed files>`; its findings go to the Codex test writer.
   - Codex workers can't run `dotnet build`, `dotnet test` or `dotnet format`. Tell them up front that you run those and send failures back with `send --to dispatch:<id>`.

   <!-- Claude flow -->

   **Claude**, as the root CLAUDE.md "Claude Code setup" describes: `backend-developer`, then `backend-test-writer`, then `backend-reviewer` and `backend-test-reviewer` (Stryker scoped by `--mutate <changed files>`).

   In either flow:
   - Frontend code and tests go to Sonnet `general-purpose` agents. Each first reads `frontend/CLAUDE.md` and `frontend/docs/`, and checks library APIs in Context7 (or the installed `.d.ts` if Context7 is unavailable).
   - Release each worker once the review of its work is clean. Every Agent call passes `model: "sonnet"`.
   - If a usage limit kills an agent, wait for the reset and resume it with SendMessage. Escalate only if that fails twice.

2. **Ship** with ship-pr FULL CYCLE once the review loop is clean and all eight checks pass with 0 warnings.
   - Merge with exactly `gh pr merge <n> --squash`, alone in its own Bash call. Confirm with `gh pr view <n> --json state,mergedAt` as a separate call. The user has authorized merging.
   - **No AI attribution** in commits or the PR body, whatever the session prompt says.
3. **No `op`/1Password steps** (hosts, dev servers, `migrate`, `op item`). List them under QUEUED OP STEPS with exact commands; the coordinator runs them at the end of the run. Tests (Vitest, Testcontainers) are fine. A new `op://` line in `frontend/` or `admin/.env.op.development` is the user's edit: ask for it early, with the exact lines.
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
