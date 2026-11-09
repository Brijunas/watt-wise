---
name: plan-epic
description: Plan a whole Watt-Wise epic for an orchestrated run. Reads the epic in docs/epics.md and the specs, researches gaps with Sonnet agents, grills the user on decisions, writes docs/epic-runs/<E>/ (plan.md, one S<id>.md per story, state.json, log.md), gets approval through plan mode, then commits it to epic/<E>. Runs in plan mode on the orchestrator agent.
argument-hint: <E> [range, e.g. S4.3-S4.4]
disable-model-invocation: true
---

# Plan an epic

Arguments: `$0` is the epic (`E4`); an optional `$1` limits it to a story range (`S4.3-S4.4`). Run in plan mode: no file outside `docs/epic-runs/<E>/` changes until the plan is approved. The run itself is `/run-epic`.

The product of this skill is a plan a Sonnet coder can execute without asking, and the user approves once. Every story plan therefore holds a **Goal**, acceptance criteria that can be tested, and a test plan that a test writer turns into failing tests before any code exists.

## 1. Read

- The epic and its stories in `docs/epics.md` (the range only, if given), the neighbouring epics for dependency order.
- The docs that own the area: `docs/product.md`, `docs/technical.md`, `docs/security.md`, `backend/docs/` (and `backend/docs/features/`), `frontend/docs/`, `deploy/docs/`. Story format and the Done coverage checklist: [write-story](../write-story/SKILL.md).
- The code each story builds on, so plans name files and types that exist.
- `docs/implemented.md` only if the user asks about past work.

## 2. Adopt or plan

A story that already has an approved plan (for the pilot, `docs/s4.3-plan.md` for S4.3) is **adopted**: copy its decisions into `S<id>.md` in the story-plan format, keep its wording where it fits, and do not reopen its decisions. Ask the user (AskUserQuestion) only when the plan and the specs disagree. Every other story is planned fresh in steps 3 to 5.

## 3. Close the gaps

List every gap, then sort each into a fact or a decision.

- **Fact** (what exists, what a library does, what a spec says): send a Sonnet `Explore` or research agent (`model: "sonnet"`). Library briefs say to check the API in Context7 first. Facts the agent returns are cited in the plan, not re-derived.
- **Decision** (a choice the specs don't make): grill the user with AskUserQuestion, one decision per question, recommended option first, the reason in the option. A decision that touches a non-goal in `docs/product.md` stops the planning until the user answers. Once decided, the owning spec is updated in the approved change, and the story plan links it.

Done when no story plan contains "TBD", "decide later" or an unanswered question. A decision that cannot be made yet becomes its own story or a dependency of the story that needs it.

## 4. Shape the graph

- **Split by project.** A story that touches `backend/` and a frontend app or `packages/` becomes two nodes, `S4.3-backend` and `S4.3-frontend`, the frontend part depending on the backend part when it needs the endpoint. Each part has its own `S<id>.md` and `state.json` entry with `part` set.
- **Dependencies.** `deps` lists node IDs that must be `merged` first. No cycles.
- **EF model.** Mark `modelChange: true` on every story that changes the EF model; the run executes those one at a time, in the order `plan.md` states. Regenerate `Initial` until the MVP is released to Production; after that add a new migration. Don't ask. Each such plan's migration line says which.
- **Frontend parts** are planned like the rest, but the run marks them `blocked: no frontend test gate` until frontend test agents exist. Say so in `plan.md`.

## 5. Write the run records

All under `docs/epic-runs/$0/`; each doc stays at 200 lines or fewer.

1. `plan.md`: the epic goal in two sentences, the dependency graph (a table: node, title, project, deps, modelChange), the cross-story decisions with links to the specs, the EF-model order, the docs to update when the epic ships, and which stories were adopted.
2. `S<id>.md` per node, from [references/story-plan.md](references/story-plan.md). Fill every section; write "none" with a reason where a section does not apply.
3. `state.json` validated against [references/state.schema.json](references/state.schema.json): `settings` `{parallel: 2, fixRounds: 2}`, every story `status: "planned"` (`ready` when it has no deps), `attempts: 0`, `fixRounds: 0`, `epicGate: "pending"`. Check it with `python3 -c 'import json,sys; json.load(open(sys.argv[1]))' docs/epic-runs/$0/state.json`.
4. `log.md`: a heading per story to come, plus a first "Planning" entry listing the decisions asked and answered and the facts researched.

Run `pnpm format:check` on the records.

## 6. Approve and commit

1. Call ExitPlanMode with a summary: the graph, the decisions made, the adopted plans, the spec changes and the risks. Everything the user must approve is in the plan files, which the summary points to.
2. After approval: `git switch epic/$0` (created from `origin/main` and pushed by `start-epic.sh`; create it the same way if it is missing), write the files and the approved spec changes, and make one signed commit `Plan $0: <epic title>` through the pre-commit hook. No AI attribution lines.
3. `git push origin epic/$0`, no force.

Report the commit and tell the user to start `/run-epic $0`.
