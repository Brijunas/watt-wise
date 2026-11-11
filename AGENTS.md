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
- [Frontend](frontend/AGENTS.md): React app, assets, and shared web specifications.
- [Admin](admin/AGENTS.md): admin app; inherits shared web guidance.
- [Packages](packages/AGENTS.md): shared `@wattwise/*` libraries.
- [Deployment](deploy/AGENTS.md): Compose, PostgreSQL, Grafana, and Mailpit.

There is no root rules directory. Existing path rules live in [backend/.claude/rules](backend/.claude/rules/); read the matching files explicitly. Recheck the affected project's Markdown and rules directories when adding or moving documentation, and update its guide's links.

## Workflows & Contributions

Read the matching skill for [migrations](.agents/skills/ef-migration/SKILL.md), [dependency updates](.agents/skills/update-deps/SKILL.md), [story writing](.agents/skills/write-story/SKILL.md), or [shipping PRs](.agents/skills/ship-pr/SKILL.md). Claude equivalents live in [.claude/skills](.claude/skills/).

Run all eight [development checks](docs/development.md#checks) before committing; keep backend warnings at zero. Notify the user before `op` commands requiring desktop authorization. Preserve secret-scanning hooks.

Use descriptive scope/story commit subjects. PRs explain behavior, reference stories, report validation, and include relevant UI screenshots. Never add AI attribution or Codex session links. Update epics when stories ship; archive completed epics. Give concurrent sessions separate worktrees and delegated workers explicit file ownership.
