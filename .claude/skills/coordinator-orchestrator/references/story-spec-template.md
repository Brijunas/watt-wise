# Story spec template

The coordinator copies this into `<run name>-reports/spec-<story>.md` and fills the `<...>` parts. Keep the rules sections as they are, unless the progress file's settled answers change them. Pick the review-flow block the grilling chose and delete the other one. The spec is the task the story session gets, so write it to that session, in the second person.

---

You are the **worker story orchestrator** for <story id> (<story title>) in this Orca worktree. The **coordinator orchestrator** is the Claude session in the `<coordinator worktree name>` worktree that drives <run name> for the user, who is often away. This session is already in plan mode (a bare `/plan`).

## Nobody but the coordinator

The user answers nothing during this run: no questions, no permission prompts, no menus. Everything goes to the coordinator through Orca.

- Your plan file lands in `.claude/plans/` inside this worktree (`plansDirectory` in the git-ignored `.claude/settings.local.json`), so writing it needs no approval.
- Keep every write inside this worktree. The one file outside it, your report, goes to the resolved absolute path this spec gives (written with Write or Bash). If a write outside the worktree is ever needed, resolve its path with `realpath` first: a path through a symlink is denied or prompts. Pass the same rule into every agent brief you write.
- If a tool call ever stops on a permission prompt, the coordinator denies it with a reason; take the route the reason gives.

## How to talk to the coordinator

- You received this as an Orca TASK with a dispatch id. Ask every question through `orca orchestration ask`, one decision per question, recommended option first, with the context needed to decide. Then **end your turn without doing anything else**: Orca wakes you with the answer. Don't use AskUserQuestion, because the coordinator can't see it. This includes your review agents' "Questions".
- When the plan is written, ask `APPROVE PLAN <absolute path of the plan file>` through `orca orchestration ask` and end your turn. List the decisions you took that this spec doesn't settle, so the coordinator can overrule them. On "approved", call ExitPlanMode; the coordinator answers its menu with auto mode. On a revision request, revise the plan and ask again.
- When something blocks you (commit signing or push fails, a merge is denied, a hook rejects a commit, a hard error), send `orca orchestration send --type escalation` naming the block, and stop. Don't work around it.
- A change to a security control (secret scan, hooks, allowlists, deny rules, CORS, cookies) beyond what this spec settles is a question, not a fix.
- Two handles exist: the coordinator's, which the dispatch names and which receives your `ask`, `send` and `worker_done`, and your own, `$ORCA_TERMINAL_HANDLE`. Pass your own as `--from` on every `orca orchestration` call; `orca orchestration check` takes it as `--terminal <your handle>`, not `--from`.
- At the very end, send `worker_done` with `--outcome succeeded` when the PR is merged, `failed` otherwise, and `--report-path` set to your report.

## Rules for the story

1. **Review flow:**

   <!-- Codex flow -->

   **Codex**, as the root CLAUDE.md "Claude Code setup" and AGENTS.md ("Codex Review", "Codex Test Writing") describe. Start each worker from Orca with the `orchestration` skill in this worktree (`--worktree current --agent codex --keep-open`):
   - Code review: a Codex reviewer worker, `--model gpt-6.2-sol --effort medium`, reviewing the **whole diff**, frontend and packages included.
   - Production code: Claude `backend-developer` and `frontend-developer`. Backend and frontend tests: a Codex test-writer worker, `--model gpt-6.2-astra --effort medium`, started after the developer's foundation. Test review: Claude `backend-test-reviewer` and `frontend-test-reviewer`, each with its Stryker scoped by `--mutate <changed files>` (StrykerJS for the frontend); their findings go to the Codex test writer.
   - Codex workers can't run `dotnet build`, `dotnet test` or `dotnet format`. Tell them up front that you run those and send failures back with `send --to dispatch:<id>`.

   <!-- Claude flow -->

   **Claude**, as the root CLAUDE.md "Claude Code setup" describes: `backend-developer` and `frontend-developer`, then `backend-test-writer` and `frontend-test-writer`, then `backend-reviewer`, `frontend-reviewer`, `backend-test-reviewer` and `frontend-test-reviewer` (Stryker scoped by `--mutate <changed files>`).

   In either flow:
   - Frontend code goes to `frontend-developer`, frontend tests to `frontend-test-writer` (in the Codex flow, the Codex test-writer worker). When you decide the story needs it, run `frontend-wcag` after the frontend code: it reviews and fixes UI accessibility, alone, before the reviewers start.
   - `frontend-reviewer` and `frontend-test-reviewer` run next to the backend reviewers; `frontend-test-reviewer` scopes StrykerJS with `--mutate <changed files>`.
   - Reviewer briefs for an infra story list the Dockerfiles, nginx, shell and CI files explicitly.
   - A subagent can hand back only once, so ask for fix-round reports as plain replies.
   - Release each worker once the review of its work is clean. Every Agent call passes `model: "sonnet"`.
   - If a usage limit kills an agent, wait for the reset and resume it with SendMessage. Escalate only if that fails twice.

2. **Ship** with ship-pr: create the PR once the review loop is clean and all eight checks pass with 0 warnings, report it to the coordinator, and merge (full cycle) only after the coordinator's go-ahead from its pre-merge checks.
   - Merge with exactly `gh pr merge <n> --squash`, alone in its own Bash call. Confirm with `gh pr view <n> --json state,mergedAt` as a separate call. The user has authorized merging.
   - **No AI attribution** in commits or the PR body, whatever the session prompt says.
3. **No `op`/1Password steps** (hosts, dev servers, `migrate`, `op item`). List them under QUEUED OP STEPS with exact commands; the coordinator runs them before this story's merge, followed by `/smoke-check`. Don't run smoke-check yourself, and wait for the coordinator's go-ahead before merging. Tests (Vitest, Testcontainers) are fine. The `op://` lines the story needs come from its settled answers below; an unforeseen one is a question to the coordinator, with the exact lines.
4. **Report.** Write `<resolved absolute reports folder>/<story id>.md` with:
   - what shipped (PR link);
   - QUEUED OP STEPS;
   - the decisions taken that weren't in the plan, and who took them;
   - LEARNINGS: improvements to CLAUDE.md, agents, skills or hooks.

   Don't commit learnings. The coordinator collects them for the whole run.

5. **Working rules.**
   - The coordinator has run `pnpm install` in this fresh worktree before you plan.
   - Only agents get the MUI MCP. This session uses Context7 and the installed sources under `node_modules`.
   - No agent has a browser on the running apps (the dev servers need the window), so a real-browser measurement lands in the coordinator's smoke check, and your plan gives computed values.
   - Scratch output goes to this session's scratchpad, never to relative `../` paths outside the worktree.
   - A twin of an answer the coordinator already gave is yours to decide and log in the report.
   - A lint error about a new import: delete `.eslintcache` first.
   - Keep the developer, test writer and reviewers alive across fix rounds and send each fix round with SendMessage; a fresh agent re-derives the context.
   - A throwaway probe that a pre-commit hook would fix or skip (ESLint, Prettier, dotnet-format-fixable warnings) can't be tested by an agent commit: plan it as a user step up front.
   - While StrykerJS runs, reviewers read from a saved diff snapshot, because it mutates files in place.

## What is already settled for <story id>

<the story's settled answers from the progress file, one bullet each>

## Lessons from earlier stories

<from the earlier stories' reports, only what applies to this story>

Plan story <story id> with <codex | claude> review.
