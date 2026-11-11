# Backend Guidelines

## Scope & Hierarchy

Follow the [root guide](../AGENTS.md) and its specifications first. This guide covers `backend/src/`, `backend/tests/`, and backend configuration. Backend docs refine system decisions; the path rules below implement them. Consult the [README](README.md) for orientation and [Claude notes](CLAUDE.md) for existing tooling and testing lessons; their summaries remain subordinate to the specifications.

## Required Documentation

| Read before changing                                        | Source                                                                      |
| ----------------------------------------------------------- | --------------------------------------------------------------------------- |
| Any C# implementation or project structure                  | [Architecture](docs/architecture.md), [conventions](docs/conventions.md)    |
| Domain, Application, aggregates, or persistence ports       | [Domain model](docs/domain-model.md)                                        |
| Tests, fixtures, or mutation checks                         | [Testing](docs/testing.md)                                                  |
| Results, exceptions, validation, or HTTP error contracts    | [Error handling](docs/error-handling.md)                                    |
| Logs, traces, metrics, or OTLP                              | [Observability](docs/observability.md)                                      |
| Background jobs, Hangfire, or its dashboard                 | [Jobs](docs/jobs.md)                                                        |
| Email messages, rendering, SMTP, delivery, or retries       | [Email](docs/email.md), [outbox](docs/email-outbox.md)                      |
| Accounts, registration, confirmation, or feature vocabulary | [Accounts](docs/features/accounts.md), root [security](../docs/security.md) |
| Database roles, grants, or bootstrap                        | [PostgreSQL](../deploy/docs/postgres.md)                                    |

Read additional feature docs under [docs/features](docs/features/) when that feature is affected.

## Applicable Path Rules

Read these files explicitly before editing matching code; use their `paths` frontmatter to determine scope. Patterns are relative to `backend/`. The actual directory is `.claude/rules/`, not `.Codex/rules/`.

| Matching work                     | Rule                                                                                                         |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Any C# file                       | [Class design](.claude/rules/class-design.md), [service registration](.claude/rules/service-registration.md) |
| Domain source                     | [Domain model](.claude/rules/domain-model.md)                                                                |
| Application source                | [Use cases](.claude/rules/use-cases.md)                                                                      |
| Infrastructure persistence source | [Persistence](.claude/rules/persistence.md)                                                                  |
| Accounts source or tests          | [Accounts](.claude/rules/accounts.md)                                                                        |

Follow each rule's linked specification for rationale and details; update the owning specification when a decision changes.

## Execution & Review

Use the root [development commands](../docs/development.md) for build, formatting, and tests. Integration tests require Docker; follow [mutation-testing instructions](docs/testing.md#mutation-testing) and run Stryker separately from builds and Docker tests.

Model changes use the [migration skill](../.agents/skills/ef-migration/SKILL.md). Endpoint changes require `pnpm generate:api`; include the OpenAPI document and generated client together.

Use [.codex/agents](../.codex/agents/) briefs when delegating production code, tests, and reviews; [Claude briefs](../.claude/agents/) serve Claude tooling. Run `backend-reviewer` before backend PRs and `backend-test-reviewer` for test reviews; as a Codex review worker, follow [Codex Review](../AGENTS.md#codex-review) instead. Report unavailable roles explicitly. Give workers this guide and exact file ownership; resolve stale brief paths against the verified links above.
