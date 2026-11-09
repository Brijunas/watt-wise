# Agentic delivery

How Claude delivers work: a story at a time with you (the story flow), or a whole epic on tethys (the epic flow). The story flow is unchanged and stays available. Security rules for agents are in [security.md](security.md#agents); the tethys setup is in [deploy/docs/agent-host.md](../deploy/docs/agent-host.md).

## Models

Opus plans, reviews and orchestrates. Sonnet implements, writes tests and researches. Haiku runs one script or command and reports. Every Agent call passes its `model`.

Auto permission mode comes from user settings (`permissions.defaultMode`), mirrored to every machine by `deploy/agent-host/sync-user-claude.sh`; project settings can't set it.

## Story flow

Start a session on vidar in the story's worktree, plan with Opus in plan mode, let Sonnet agents implement, review with `backend-reviewer` and the test agents, then ship with `/merge-to-main` (a PR into main, squash-merged). Nothing in the epic flow changes this.

## Epic flow

### Start

1. In Orca on vidar, open a terminal on tethys in the main checkout, `/home/corvus/code/personal/watt-wise`. The config is the same on every machine; tethys is where epics run because it's always on.
2. `.claude/scripts/start-epic.sh E4` creates or switches to the epic worktree on `epic/E4` and opens the tmux session `epic-E4` with the orchestrator, started as `claude --agent orchestrator "/goal …"`, so the goal is already set. It prints the goal line too, to paste if the session was already running. Reattach later with `tmux attach -t epic-E4`. On tethys the script skips the Claude parity check and the memory sync; they run from vidar (the timer).
3. Check the goal with `/goal` in the session.
4. `/plan-epic E4` (optionally a story range, e.g. `S4.3-S4.4`). Opus reads the specs, sends fact-finding to Sonnet agents, and grills you on the decisions only you can make. It writes `docs/epic-runs/E4/` and asks for approval through plan mode.
5. After approval, `/run-epic E4`. You can leave.

### What to expect

- Up to two stories run at once, each as a background Sonnet subagent in its own worktree on `story/E4/<id>`, created under the main checkout's `.claude/worktrees/` (gitignored, and ignored by Prettier and ESLint). Stories that change the EF model run one at a time.
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
| Story gate | `gate-runner` before merge | the 8 checks, pending model changes, API client in sync, suppression scan, Stryker on the changed files of touched projects (`--mutate` globs; an empty run fails)                                     |
| Review     | before merge               | Opus `backend-reviewer` or `frontend-reviewer` (correctness, security, architecture, conventions), plus the test reviewer for that project. Frontend stories wait until the frontend test agents exist |
| Epic gate  | before the epic PR merges  | all story-gate checks on the epic head, a full Stryker run on touched projects (scores don't drop)                                                                                                     |

Implementers cannot edit tests, run records, `.claude/`, `.github/`, hooks or analyzer and lint configs. A guard blocks pushes to main, force-pushes and `--no-verify`.

## Experiments log

Results of the checks run before and during the pilot, with `claude --version` and `orca --version`.

| #   | Question                                      | Result                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| --- | --------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Worktree subagents base on the epic branch    | Pass. With `claude --agent orchestrator`, a `backend-coder` (isolated worktree under the main checkout's `.claude/worktrees/`) saw O's unpushed tip `90e98e8` and `marker.txt` (`baseRef: head` works). The push denial was shown in #3 (the coder first declined the push by itself; a brief saying the denial is the point got it to try).                                                                                                                                                                                |
| 2   | Fast gate blocks, counts, gives up; timeout   | Fail (blocked by #11; #11 fixed, rerun in the pilot). The coder could not write `Probe.cs`: `epic-agent-guard (orchestrator)` denied it. Counter and 20 s timeout untested.                                                                                                                                                                                                                                                                                                                                                 |
| 3   | Auto mode from user settings                  | Pass. `~/.claude/settings.json` has `auto`, no project file sets it; O and background coder ran Bash without prompts. O's `.claude/probe` write: `epic-agent-guard (orchestrator): .claude/probe is protected`. Coder's `git push --dry-run origin HEAD:main`: `epic-agent-guard (coder): pushing to main is not allowed (HEAD:main)`, reported to O.                                                                                                                                                                       |
| 4   | Rulesets and token limits                     | Partial (workflow push fixed: tethys pushes over HTTPS with the PAT, which has no Workflows permission). Both rulesets active, `bypass: []`, `can: never` (pass). Signed push to `epic/X0` accepted, `verified: true` (pass). **Fail:** a push of `.github/workflows/probe.yml` from tethys was accepted (SSH key, no workflow-scope check). Force-push untested: `repo-guard` blocks it in Claude; needs a human in a plain terminal. Status: fixed, `origin` is HTTPS and `gh auth setup-git` sets the credential helper. |
| 5   | `/goal` at launch; notifications              | Launch argument works (`claude --agent orchestrator "/goal …"` set the goal and it was achieved); `start-epic.sh` can pass it. Pasting also works. "Needs you" on Orca/phone: Pending (not observable from the agent).                                                                                                                                                                                                                                                                                                      |
| 6   | Stryker `--since` on Application              | Fail. Full run: 25 s, 75.76 %. `--since:origin/epic/X0` crashed (exit 139) until `openssl3-libs` was installed (Fedora 45 has only OpenSSL 4); then, **in a git worktree it ignores every mutant** ("Removed by since filter", no score, gate passes empty). In a plain clone: only `Probe.cs`, 20.7 s, 0 % (no test), so little time saved on a small project. Status: replaced by `--mutate` globs of the changed files; `openssl3-libs` is in `setup-tethys.sh`.                                                         |
| 7   | Two parallel builds                           | Fail (tests), pass (memory). Peak used 6.4 GB of 8.6 GB total (the 13 GB limit does not fit this VM); no container failed. Both runs: 397 tests, 1-2 failed: `SmtpEmailSenderFailureTests.A_connection_dropped_after_ehlo_is_logged_and_does_not_throw` in both, `HostSettingsTests.A_host_with_blank_database_settings_stops_at_start` in one.                                                                                                                                                                             |
| 8   | 1Password reads per story                     | Pass for what ran: the service account's token read limit is 1000/h; `used` stayed 6 through the whole session (O, coder dispatches, resume). No complete story ran (#9), so the per-story figure is a lower bound of 0 reads.                                                                                                                                                                                                                                                                                              |
| 9   | Resume from `state.json` after a kill         | Partial. Killed O with S0.1 `running`; the restarted O read `state.json`, checked the branch and worktree, found no work, reset S0.1 to `ready` and created no second branch or coder. Not run to merge: the guard bug (#11) blocks every coder.                                                                                                                                                                                                                                                                            |
| 10  | User config and memory                        | Pass. User CLAUDE.md, plugins and symlink (`~/.claude/projects/<epic-X0 key>/memory` -> `/home/corvus/.claude/wattwise-memory`) match; after the first sync (before it the folder was empty and O found no index) `MEMORY.md` and 25 files are on tethys. Both probe files crossed in one sync, deletions propagated, no conflict copies.                                                                                                                                                                                   |
| 11  | (found) Orchestrator guard fires on subagents | Fail. The orchestrator's frontmatter hook (`--role orchestrator`) also runs for the coder's tool calls, so a coder writing code outside `docs/epic-runs/**` is denied. Status: fixed; the guard now ignores subagents' tool calls.                                                                                                                                                                                                                                                                                          |
