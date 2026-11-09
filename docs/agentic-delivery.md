# Agentic delivery

How Claude delivers work: a story at a time with you (the story flow), or a whole epic on tethys (the epic flow). The story flow is unchanged and stays available. Security rules for agents are in [security.md](security.md#agents); the tethys setup is in [deploy/docs/agent-host.md](../deploy/docs/agent-host.md).

## Models

Opus plans, reviews and orchestrates. Sonnet implements, writes tests and researches. Haiku runs one script or command and reports. Every Agent call passes its `model`.

Auto permission mode comes from user settings (`permissions.defaultMode`), mirrored to every machine by `deploy/agent-host/sync-user-claude.sh`; project settings can't set it.

## Story flow

Start a session on vidar in the story's worktree, plan with Opus in plan mode, let Sonnet agents implement, review with `backend-reviewer` and the test agents, then ship with `/merge-to-main` (a PR into main, squash-merged). Nothing in the epic flow changes this.

## Epic flow

### Start

1. In Orca on vidar, open a terminal on tethys in any watt-wise checkout. The config is the same on every machine; tethys is where epics run because it's always on.
2. `.claude/scripts/start-epic.sh E4` creates or switches to the epic worktree on `epic/E4` and opens the tmux session `epic-E4` with the orchestrator (`claude --agent orchestrator`). Reattach later with `tmux attach -t epic-E4`.
3. Paste the `/goal` line the script prints.
4. `/plan-epic E4` (optionally a story range, e.g. `S4.3-S4.4`). Opus reads the specs, sends fact-finding to Sonnet agents, and grills you on the decisions only you can make. It writes `docs/epic-runs/E4/` and asks for approval through plan mode.
5. After approval, `/run-epic E4`. You can leave.

### What to expect

- Up to two stories run at once, each as a background Sonnet subagent in its own worktree on `story/E4/<id>`. Stories that change the EF model run one at a time.
- Each story: tests are written first from the acceptance criteria, then implemented. Then the gate (all checks, mutation on changed code, suppression scan) and Opus review plus test review, with findings recorded in `log.md`. A passing story is squash-merged straight into `epic/E4`; there are no PRs into epic branches, only the epic's PR into main.
- Up to two fix rounds. After that, or when a story needs a decision, the orchestrator researches, writes a brief in `log.md`, parks the story, continues with the others and asks you in its session. Answer there.
- Merged stories land on `epic/E4` as one squash commit each. The checks rerun on the epic head after every merge.
- At the end: full mutation run, the report as the epic PR's description, the stack running on tethys. Until the MVP is in Production, the orchestrator merges the epic into main with a merge commit when every gate passes; read the report and try the stack afterwards.

### Run records

`docs/epic-runs/<E>/` on the epic branch: `plan.md`, one `S<id>.md` per story, `state.json` (story status, attempts, PRs, tunables), `log.md` (amendments, escalations, decisions) and `report.md`. Only the orchestrator writes them (besides them it edits only `docs/epics.md` and `docs/implemented.md`, to move the finished epic); they are deleted in the epic's last commit.

### Tunables

In `state.json` → `settings`: `parallel` (default 2) and `fixRounds` (default 2). Edit and commit them on the epic branch, or tell the orchestrator; it rereads them before each dispatch.

### Gates

| Gate       | Where                      | Checks                                                                                                                                                                                                 |
| ---------- | -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Fast gate  | coder's SubagentStop       | build, format, unit tests or JS lint/typecheck/test on what changed; an evidence table against the Goal                                                                                                |
| Story gate | `gate-runner` before merge | the 8 checks, pending model changes, API client in sync, suppression scan, Stryker `--since` on touched projects                                                                                       |
| Review     | before merge               | Opus `backend-reviewer` or `frontend-reviewer` (correctness, security, architecture, conventions), plus the test reviewer for that project. Frontend stories wait until the frontend test agents exist |
| Epic gate  | before the epic PR merges  | all story-gate checks on the epic head, a full Stryker run on touched projects (scores don't drop)                                                                                                     |

Implementers cannot edit tests, run records, `.claude/`, `.github/`, hooks or analyzer and lint configs. A guard blocks pushes to main, force-pushes and `--no-verify`.

## Experiments log

Results of the checks run before and during the pilot, with `claude --version` and `orca --version`.

| #   | Question | Result |
| --- | -------- | ------ |
