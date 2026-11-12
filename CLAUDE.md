# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

Watt-Wise is a greenfield project, built epic by epic ([docs/epics.md](docs/epics.md)). E1–E3 are done. E4 (accounts and authentication) is in progress: S4.1–S4.7 have shipped. What exists today:

- **Backend** (`backend/WattWise.slnx`): the hosts `WattWise.Api` and `WattWise.Jobs` (Hangfire server, dashboard, and the outbox's send and purge jobs; `backend/docs/jobs.md`) and the `WattWise.Cli` tool, whose `migrate` applies the EF Core migrations and installs Hangfire's tables. Requests go through a source-generated Mediator with logging and validation behaviors and `Result<T>`, and failures leave as ProblemDetails (`backend/docs/error-handling.md`). The Api serves the `/api/v1` route group, a JSON `/health` report and, outside Production, the OpenAPI document with Scalar; CORS origins come from 1Password. Serilog JSON logs and OpenTelemetry traces and metrics leave over OTLP (`backend/docs/observability.md`).
- **Accounts** (S4.1): ASP.NET Core Identity on `AppDbContext`: an `Account` with its email and password identity, roles with permissions on the account, and single-use email links stored as hashed tokens (`backend/docs/features/accounts.md`; one `Accounts/` folder per project). Sign-in issues a 15-minute HS256 JWT and a rotating refresh token in an httpOnly cookie, kept as a `Session` with hashed `RefreshToken`s (S4.4); a password reset or change ends every session and replaces the security stamp that refresh checks (S4.5); a daily Jobs job purges expired sessions. The `Initial` migration seeds the `Admin` role and its `admin.access` permission; the `/api/v1/admin` route group requires that permission (S4.7), and `WattWise.Cli admin grant|revoke <email>` gives and takes the role. Until the first deploy the schema is a single `Initial` migration that a model change may regenerate.
- **Email** (S4.2, S4.3): an Application `IEmailSender` port with the three account emails, rendered from LT/EN Fluid + MJML templates and sent over SMTP with MailKit. The Api only records each email in the `app.email_requests` outbox, and the Jobs host sends it with retries; the Jobs host stops at start without its `Smtp` and `Links` settings (`backend/docs/email.md`, `backend/docs/email-outbox.md`).
- **Backend tests**: unit and integration test projects on real PostgreSQL through the shared Testcontainers fixture (`backend/tests/WattWise.Testing`), plus Stryker.NET mutation testing (Domain, Application, Infrastructure, Api) and CsCheck property-based tests (`backend/docs/testing.md`).
- **Web apps**: `frontend/` and `admin/` are Vite React TypeScript PWAs sharing `@wattwise/ui` (MUI theme with light/dark/auto, app shell, form fields, update prompt), `@wattwise/core` (store factory, base query, zod helpers, runtime config and its Vite plugin), `@wattwise/i18n` (LT/EN, typed keys) and `@wattwise/api-client` (RTK Query client generated from the Api's build-time OpenAPI document). Each app reads the API address from a runtime `/config.js`, and its dev server gets its settings from 1Password.
- **Development stack** (`deploy/`): PostgreSQL with least-privilege roles, Grafana LGTM and Mailpit. The Jobs host's `jobs` role reads and writes `app` and `hangfire`, EF's migration history sits in its own `migrations` schema, and connection pools use the defaults (`deploy/docs/postgres.md`).

Not there yet: the Api's endpoints under `/api/v1` are `/about`, S4.3's `/auth/register`, `/auth/confirm-email` and `/auth/resend-confirmation`, and S4.4's `/auth/sign-in`, `/auth/refresh`, `/auth/sign-out` and `/me`, S4.5's `/auth/forgot-password`, `/auth/reset-password` and `/auth/change-password`, and S4.6's `/auth/sessions`, `/auth/sessions/{id}` and `/auth/sign-out-everywhere`, with no real endpoint yet in the `/api/v1/admin` group; the only feature in both apps is `about`. There is no CI before E5.

## Docs

Each doc has one job. Read or update the one that matches the task:

- `docs/product.md`: what the product does, MVP scope, non-goals, open questions. Source of truth.
- `docs/technical.md`: whole-system technical decisions (repo layout, stacks, how the apps talk to each other, data model, time handling, hosting, secrets, shared code style). Source of truth; read the relevant section before implementing, and every implementation decision must match it.
- `docs/security.md`: every security decision (accounts, passwords, tokens, sessions, authorization, transport and limits, deployment, CI and contributions), linking to the docs that own secrets and database hardening. Source of truth; anything that touches security goes here, not in `technical.md` or a project doc.
- `<project>/docs/`: technical decisions that belong to one project. Source of truth for that project, read before implementing in it.
  - `backend/docs/`: the .NET solution (architecture, domain model, testing, conventions, error handling, observability, jobs, email, email outbox).
  - `backend/docs/features/`: one doc per feature, each with the Vocabulary every other doc follows (`accounts.md`).
  - `frontend/docs/`: both web apps and the `@wattwise/*` packages (architecture, structure, testing, conventions). `admin/` and `packages/` have no docs of their own; they follow these.
  - `deploy/docs/`: the Compose stacks, PostgreSQL, Grafana LGTM and Mailpit.
- `<project>/CLAUDE.md`: Claude's notes for one project, loaded when working in it. `backend/CLAUDE.md` for C#, `frontend/CLAUDE.md` for TypeScript; `admin/CLAUDE.md` and `packages/CLAUDE.md` import the frontend one.
- `docs/epics.md`: the epics and stories still to do.
- `docs/implemented.md`: finished epics, kept for history. Don't read it unless asked about past work.
- `docs/setup.md`: first-time machine setup only. Update it only when a story changes how a new machine is set up.
- `docs/development.md`: everyday commands and workflow. Update it when a story adds or changes a command.
- `docs/known-issues.md`: upstream problems with temporary workarounds. Check it before changing tool versions.

## Working rules

- When a task needs a decision the specs don't cover, ask rather than invent. When a decision changes, update the relevant spec in the same change.
- Each fact lives in one doc; other docs link to it instead of repeating it.
- Technical details that belong to one project go in that project's `docs/` folder; for `admin/` and `packages/`, that is `frontend/docs/`. Root `docs/technical.md` keeps only whole-system architecture and links to the project docs.
- Keep every doc at 200 lines or fewer. Split a doc before it goes over.
- When a story ships, remove its bullet from `docs/epics.md` in the same change; its facts live in the docs.
- When an epic's last story ships, move the epic from `docs/epics.md` to `docs/implemented.md` in the same change.

## Claude Code setup

Sessions may start at the repo root or inside a project (`backend/`), and both must work.

- Agents and skills live in the root `.claude/agents/` and `.claude/skills/`, named `<project>-…` when they belong to one project. Claude Code finds them from a subfolder by walking up.
- Codex has a parallel setup derived from this one: `AGENTS.md` in the root and each project, `.codex/` (config, agents, hooks) and `.agents/skills/`. A change to `.claude/agents/*.md`, `.claude/skills/` or `.claude/hooks/` updates its twin (`.codex/agents/*.toml`, `.agents/skills/`, `.codex/hooks/`) in the same change; the Claude file is the source.
- Path-scoped rules and notes for one project go in `<project>/.claude/rules/` and `<project>/CLAUDE.md`.
- `settings.json` is not inherited from the root. A project Claude may be started in has its own `.claude/settings.json`, registering the same hooks, MCP servers and deny rules; change both together. Hook scripts live once, in the root `.claude/hooks/`. Today only `backend/` has one; start frontend sessions at the root.
- Concurrent sessions each work in their own worktree. Don't start a branch in a checkout another session is using: it branches from that session's HEAD and carries its commits into your PR.
- Agent briefs that write code against a library tell the agent to check the library's API in Context7 first. Every Agent call passes `model: "sonnet"`, research and report writers included.
- Parallel agents in one checkout: a foundation agent first lands the contracts, stubs and every DI registration; after it, each agent owns its own files. Tests written ahead may only call members that exist. The build is red between agents and `backend-build-check` says so at each stop; the owning agent fixes it, not the orchestrator. Backend production code goes to the `backend-developer` agent, tests to `backend-test-writer`.
- Code review has two flows, picked in each story's plan. **Claude review** (the default): the `backend-reviewer` and `backend-test-reviewer` agents. **Codex review**: `backend-reviewer` runs in Codex instead, and `backend-test-reviewer` stays in Claude. The session starts a Codex worker in its own worktree through Orca (`orchestration` skill: `worker-start --worktree current --agent codex --keep-open`), so the worker sees the uncommitted diff. One worker per story worktree, kept open until the review is clean, then released. Its report comes back in `worker_done`, and from there it follows the same findings flow as Claude's reviewer. Codex's side is under "Codex Review" in [AGENTS.md](AGENTS.md#codex-review).
- Review findings, from either reviewer in either flow: the orchestrator checks each finding against the docs and drops a wrong one, saying why. Every blocker, should-fix and nit goes to the agent that owns the file (`backend-developer` or `backend-test-writer`), never fixed by the orchestrator. The reviewer's "Questions" go to the user. The same reviewer then reviews the fixes (for Codex, a follow-up to the same worker with `send --to dispatch:<id>`), and this repeats until no finding is left.
- `op` needs an approval on the 1Password desktop app. Tell the user before a step that runs `op` (hosts, `migrate`, `op item`); unattended it fails with `authorization prompt dismissed`.
- Read and edit `backend/src/*/.env.development` with the Read and Edit tools; `cat` on them in Bash is denied.
- An agent that ended while its own background command was running still counts as running. Stop it with TaskStop once its work is in.
- Hook and MCP scripts find the repo with `git rev-parse --show-toplevel`, never with `$CLAUDE_PROJECT_DIR` alone, which is the folder the session started in.
- `prettier-write` (PostToolUse) runs Prettier on every repo file Claude writes or edits, so a Markdown table or JSON may be reformatted right after an edit. That is expected; don't undo it.
- A hook that takes a `flock` closes the lock's fd for `dotnet` children (`9>&-`): MSBuild's reused nodes and the compiler server outlive the build and would hold the lock for minutes.
- MCP servers are in the root `.mcp.json`: `postgres` (read-only `reader` role; disabled in both `settings.json` files so it doesn't ask 1Password at every session start), `grafana` (logs, traces and metrics in Grafana LGTM; its URL comes from the rendered `deploy/.env`, no `op`) and `github` (token from `gh auth token` at start; the GitHub plugin stays off because its server needs a token in the environment). When a new observability or external tool is added, add or update its MCP server in the same change.

## Commands

@docs/development.md
