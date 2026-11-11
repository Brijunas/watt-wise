# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

Watt-Wise is a greenfield project, built epic by epic ([docs/epics.md](docs/epics.md)). E1–E3 are done. E4 (accounts and authentication) is in progress: S4.1–S4.3 have shipped. What exists today:

- **Backend** (`backend/WattWise.slnx`): the hosts `WattWise.Api` and `WattWise.Jobs` (Hangfire server, dashboard, and the outbox's send and purge jobs; `backend/docs/jobs.md`) and the `WattWise.Cli` tool, whose `migrate` applies the EF Core migrations and installs Hangfire's tables. Requests go through a source-generated Mediator with logging and validation behaviors and `Result<T>`, and failures leave as ProblemDetails (`backend/docs/error-handling.md`). The Api serves the `/api/v1` route group, a JSON `/health` report and, outside Production, the OpenAPI document with Scalar; CORS origins come from 1Password. Serilog JSON logs and OpenTelemetry traces and metrics leave over OTLP (`backend/docs/observability.md`).
- **Accounts** (S4.1): ASP.NET Core Identity on `AppDbContext`: an `Account` with its email and password identity, roles with permissions on the account, and single-use email links stored as hashed tokens (`backend/docs/features/accounts.md`; one `Accounts/` folder per project). Until the first deploy the schema is a single `Initial` migration that a model change may regenerate.
- **Email** (S4.2, S4.3): an Application `IEmailSender` port with the three account emails, rendered from LT/EN Fluid + MJML templates and sent over SMTP with MailKit. The Api only records each email in the `app.email_requests` outbox, and the Jobs host sends it with retries; the Jobs host stops at start without its `Smtp` and `Links` settings (`backend/docs/email.md`, `backend/docs/email-outbox.md`).
- **Backend tests**: unit and integration test projects on real PostgreSQL through the shared Testcontainers fixture (`backend/tests/WattWise.Testing`), plus Stryker.NET mutation testing (Domain, Application, Infrastructure, Api) and CsCheck property-based tests (`backend/docs/testing.md`).
- **Web apps**: `frontend/` and `admin/` are Vite React TypeScript PWAs sharing `@wattwise/ui` (MUI theme with light/dark/auto, app shell, form fields, update prompt), `@wattwise/core` (store factory, base query, zod helpers, runtime config and its Vite plugin), `@wattwise/i18n` (LT/EN, typed keys) and `@wattwise/api-client` (RTK Query client generated from the Api's build-time OpenAPI document). Each app reads the API address from a runtime `/config.js`, and its dev server gets its settings from 1Password.
- **Development stack** (`deploy/`): PostgreSQL with least-privilege roles, Grafana LGTM and Mailpit. The Jobs host's `jobs` role reads and writes `app` and `hangfire`, EF's migration history sits in its own `migrations` schema, and connection pools use the defaults (`deploy/docs/postgres.md`).

Not there yet: the Api's endpoints under `/api/v1` are `/about` and S4.3's `/auth/register`, `/auth/confirm-email` and `/auth/resend-confirmation`; the only feature in both apps is `about`. There is no CI before E5.

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
- When an epic's last story ships, move the epic from `docs/epics.md` to `docs/implemented.md` in the same change.

## Claude Code setup

Sessions may start at the repo root or inside a project (`backend/`), and both must work.

- Agents and skills live in the root `.claude/agents/` and `.claude/skills/`, named `<project>-…` when they belong to one project. Claude Code finds them from a subfolder by walking up.
- Path-scoped rules and notes for one project go in `<project>/.claude/rules/` and `<project>/CLAUDE.md`.
- `settings.json` is not inherited from the root. A project Claude may be started in has its own `.claude/settings.json`, registering the same hooks, MCP servers and deny rules; change both together. Hook scripts live once, in the root `.claude/hooks/`. Today only `backend/` has one; start frontend sessions at the root.
- Concurrent sessions each work in their own worktree. Don't start a branch in a checkout another session is using: it branches from that session's HEAD and carries its commits into your PR.
- Agent briefs that write code against a library tell the agent to check the library's API in Context7 first. Every Agent call passes `model: "sonnet"`, research and report writers included.
- Hook and MCP scripts find the repo with `git rev-parse --show-toplevel`, never with `$CLAUDE_PROJECT_DIR` alone, which is the folder the session started in.
- `prettier-write` (PostToolUse) runs Prettier on every repo file Claude writes or edits, so a Markdown table or JSON may be reformatted right after an edit. That is expected; don't undo it.
- A hook that takes a `flock` closes the lock's fd for `dotnet` children (`9>&-`): MSBuild's reused nodes and the compiler server outlive the build and would hold the lock for minutes.
- MCP servers are in the root `.mcp.json`: `postgres` (read-only `reader` role; disabled in both `settings.json` files so it doesn't ask 1Password at every session start), `grafana` (logs, traces and metrics in Grafana LGTM; its URL comes from the rendered `deploy/.env`, no `op`) and `github` (token from `gh auth token` at start; the GitHub plugin stays off because its server needs a token in the environment). When a new observability or external tool is added, add or update its MCP server in the same change.

## Commands

@docs/development.md
