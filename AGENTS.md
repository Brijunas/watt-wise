# Repository Guidelines

## Documentation Hierarchy

This guide routes work; specifications own decisions. Follow root specifications, then project specifications, then applicable path rules. Project documents refine root decisions; rules implement those decisions. Epics, READMEs, agent notes, and skills remain subordinate to the specifications. Resolve contradictions in the owning specification before implementing.

Keep each fact in one owning document and link to it elsewhere. Read the relevant sources before editing; update them when behavior changes. Keep documents at 200 lines or fewer. Markdown links resolve relative to the containing file; shell commands run from the repository root.

## Root Documentation

| Read when                                                                      | Source                                                                                 |
| ------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------- |
| Understanding the repository                                                   | [README](README.md)                                                                    |
| Deciding product scope or behavior                                             | [Product](docs/product.md)                                                             |
| Changing architecture, dependencies, or shared conventions                     | [Technical specification](docs/technical.md)                                           |
| Touching accounts, permissions, personal data, secrets, or deployment security | [Security](docs/security.md)                                                           |
| Setting up a machine                                                           | [Setup](docs/setup.md)                                                                 |
| Running apps, tests, builds, or commit checks                                  | [Development](docs/development.md)                                                     |
| Changing tools or encountering upstream problems                               | [Known issues](docs/known-issues.md)                                                   |
| Implementing or planning stories                                               | [Epics](docs/epics.md)                                                                 |
| Investigating completed work                                                   | [Implemented epics](docs/implemented.md)                                               |
| Changing Claude tooling                                                        | [Claude notes](CLAUDE.md)                                                              |
| Working on CI/CD                                                               | [Workflow status](.github/workflows/README.md), then technical/security specifications |

## Project Guides & Rules

Read each affected guide, including cross-project changes:

- [Backend](backend/AGENTS.md): .NET source/tests, backend docs, and all six path rules.
- [Frontend](frontend/AGENTS.md): React app, assets, shared web specifications, and its path rules.
- [Admin](admin/AGENTS.md): admin app; inherits shared web guidance.
- [Packages](packages/AGENTS.md): shared `@wattwise/*` libraries.
- [Deployment](deploy/AGENTS.md): Compose, PostgreSQL, Grafana, and Mailpit.

There is no root rules directory. Path rules live in [backend/.claude/rules](backend/.claude/rules/) and [frontend/.claude/rules](frontend/.claude/rules/); read the matching files explicitly. Recheck the affected project's Markdown and rules directories when adding or moving documentation, and update its guide's links.

## Workflows & Contributions

Read the matching skill for [migrations](.agents/skills/ef-migration/SKILL.md), [dependency updates](.agents/skills/update-deps/SKILL.md), [story writing](.agents/skills/write-story/SKILL.md), or [shipping PRs](.agents/skills/ship-pr/SKILL.md). Also [frontend translations](.agents/skills/frontend-translations/SKILL.md) and [smoke checks](.agents/skills/smoke-check/SKILL.md). Claude equivalents live in [.claude/skills](.claude/skills/).

Run all eight [development checks](docs/development.md#checks) before committing; keep backend warnings at zero. Notify the user before `op` commands requiring desktop authorization. Preserve secret-scanning hooks.

Use descriptive scope/story commit subjects. PRs explain behavior, reference stories, report validation, and include relevant UI screenshots. Never add AI attribution or Codex session links. Update epics when stories ship; archive completed epics. Give concurrent sessions separate worktrees and delegated workers explicit file ownership.

## Codex Review

A Claude session may start you as an Orca worker (`gpt-6.1-sol`, medium effort) to review its change (backend, frontend or both; the "Codex review" flow in [Claude notes](CLAUDE.md#claude-code-setup)). In that task:

- Don't try `dotnet build`, `dotnet test` or `dotnet format`: your sandbox blocks their named pipes (`SocketException 13`). Claude runs them and sends you the results.
- For `frontend/`, `admin/` and `packages/`, run the JS checks yourself (`pnpm --filter <workspace> test`, `pnpm lint`, `pnpm typecheck`), but no StrykerJS: Claude's `frontend-test-reviewer` runs it. The `dotnet` limit above is for the backend only.
- Delegate the review to the [`backend-reviewer`](.codex/agents/backend-reviewer.toml) agent for `backend/` and the [`frontend-reviewer`](.codex/agents/frontend-reviewer.toml) agent for `frontend/`, `admin/` and `packages/`; these are the subagents the task asks for. Don't run the test reviewers or write tests, because Claude reviews the tests and the test writer is a separate worker.
- Review `git diff main` in your current worktree plus the untracked files `git status --short` lists under those folders, unless the task names another range.
- Edit, stage and commit nothing, and run no Stryker. Bash is for `git` and the `pnpm` checks; Claude reports the `dotnet` build and test results to you.
- Report through `worker_done` in the reviewer's Output format: findings most severe first, then "Questions", then the build and test line. Claude sends each finding to the agent that owns the file.
- When Claude asks for a re-review after the fixes, review the new diff the same way and report again in the same format. Stay open until Claude releases you.

## Codex Test Writing

A Claude session may start you as an Orca worker (`gpt-6-astra`, medium effort) to write the backend and frontend tests for its story (the "Codex review" flow in [Claude notes](CLAUDE.md#claude-code-setup)). Claude writes the production code and reviews your tests. In that task:

- Don't try `dotnet build`, `dotnet test` or `dotnet format`: your sandbox blocks their named pipes (`SocketException 13`). Claude runs them and sends you failures to fix.
- For frontend tests, run the JS checks yourself (`pnpm --filter <workspace> test`, `pnpm lint`, `pnpm typecheck`) and fix failures; no StrykerJS. The `dotnet` limit above is for the backend only.
- Act as the [`backend-test-writer`](.codex/agents/backend-test-writer.toml) agent for backend tests and the [`frontend-test-writer`](.codex/agents/frontend-test-writer.toml) agent for frontend tests yourself: follow its instructions without starting a subagent, so the task's model does the work.
- Write only tests: under `backend/tests/`, and in `frontend/`, `admin/` and `packages/` the `*.test.*` files and `testing/` folders (MSW handlers, test-utils). Never change production code or docs; report a production bug instead. Stage and commit nothing, and run no Stryker (Claude's `backend-test-reviewer` and `frontend-test-reviewer` run it).
- Report through `worker_done` in the test writer's report format: files added, the case table with its status column, the test output Claude sent you and any production bug found.
- When Claude sends the test reviewer's findings, fix them the same way and report again in the same format. Stay open until Claude releases you.
