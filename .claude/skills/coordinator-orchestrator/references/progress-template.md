# Progress file template

The coordinator starts `<run name>-progress.md` from this outline. It is the source of truth after a compaction, a restart or a watchdog resume, so keep it current and short. Facts that the repo or the reports already hold get a link, not a copy.

---

# <run name> orchestration progress

## Run

- Stories: <ids, in order>
- State folder (resolved with `realpath`, used for every path): `<path>`
- Review flow: <Codex | Claude>
- Coordinator: worktree `<path>`, branch `<branch>`, terminal `<handle>` (it changes after an Orca restart)
- Orca run: `<run id>`. Watchdog automation: `<automation id>` (every 10 minutes, [watchdog.sh](watchdog.sh) precheck)
- Goal: every story of <run name> is merged on `origin/main`, the end-of-run PR is merged, and the final report ends with `<run name> DONE`
- Window (ends at): <started at HH:MM for N h, ends HH:MM | none>

## Settled answers (grilling, <date>)

### Run-wide

- <answer>

### <story id>

- <answer>

## Status

| Story | Worktree | Terminal | Task / dispatch | Plan | State | PR  | Merge commit |
| ----- | -------- | -------- | --------------- | ---- | ----- | --- | ------------ |

Current step: <one line: what happens next>

## Decisions not covered by the grilling

1. <story>: <decision>, because <reason>. (At the end of the run each gets keep or redo from the user, with its doc and PR.)

## User steps

- <story>: <what only the user can do, with the exact one-line command, and why it was blocked>

## Queued op steps

- <story>: <steps, or a link to its report>

## Learnings

- <story> (<report link>): <learning> → <file it would change>

<Last line, written at cleanup, after the end-of-run PR merged and before the file is deleted: "<run name> DONE". When the run stops for the user instead: "<run name> STOPPED"; remove it when the run resumes.>
