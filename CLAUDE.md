# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

Watt-Wise is a greenfield project. E1 is done: the repo has the specifications plus the root JS tooling, git hooks, the four placeholder `@wattwise/*` packages and placeholder app folders. E2 is done: S2.1 created the backend solution skeleton (`backend/WattWise.slnx`, empty hosts, one trivial test per test project). S2.3 added the Development PostgreSQL stack with least-privilege roles (`deploy/`), `AppDbContext` with an empty initial migration, and `WattWise.Cli` for migrations. S2.4 added the shared Testcontainers fixture (`backend/tests/WattWise.Testing`), `DatabaseMigrator` and a minimal `/health`. S2.5 and S2.6 added the request pipeline (source-generated Mediator, logging and validation behaviors, `Result<T>`) and the ProblemDetails error contract (`backend/docs/error-handling.md`). S2.7 added the `/api/v1` route group, the OpenAPI document with Scalar outside Production, CORS from 1Password-configured origins and a JSON `/health` report. S2.8 added Serilog JSON logging and OpenTelemetry traces and metrics with OTLP export (`backend/docs/observability.md`), and Grafana LGTM in the Development stack. S2.9 added the `WattWise.Jobs` host with a Hangfire server, dashboard and a no-op recurring job, with Hangfire's tables installed by `WattWise.Cli migrate` (`backend/docs/jobs.md`). S2.10 added `dotnet format` on staged C# files to the pre-commit hook. S2.11 documented the backend, Development database and migration commands in `docs/development.md`. S2.12 settled database access: the Jobs host's `jobs` role reads and writes `app` and `hangfire`, EF's migration history sits in its own `migrations` schema, and connection pools use the defaults (`deploy/docs/postgres.md`). No feature code exists yet.

## Docs

Each doc has one job. Read or update the one that matches the task:

- `docs/product.md`: what the product does, MVP scope, non-goals, open questions. Source of truth.
- `docs/technical.md`: whole-system technical decisions (repo layout, stacks, how the apps talk to each other, data model, time handling, hosting, secrets, shared code style). Source of truth; read the relevant section before implementing, and every implementation decision must match it.
- `docs/security.md`: every security decision (accounts, passwords, tokens, sessions, authorization, transport and limits), linking to the docs that own secrets and database hardening. Source of truth; anything that touches security goes here, not in `technical.md` or a project doc.
- `docs/frontend-structure.md`: how code is laid out inside `frontend/` and `admin/` (folders, import direction, store, naming, tests). Read before adding code to either app.
- `<project>/docs/`: technical decisions that belong to one project, e.g. `backend/docs/` (architecture, testing, conventions). Source of truth for that project, read before implementing in it.
- `docs/epics.md`: the epics and stories still to do.
- `docs/implemented.md`: finished epics, kept for history. Don't read it unless asked about past work.
- `docs/setup.md`: first-time machine setup only. Update it only when a story changes how a new machine is set up.
- `docs/development.md`: everyday commands and workflow. Update it when a story adds or changes a command.
- `docs/known-issues.md`: upstream problems with temporary workarounds. Check it before changing tool versions.

## Working rules

- When a task needs a decision the specs don't cover, ask rather than invent. When a decision changes, update the relevant spec in the same change.
- Each fact lives in one doc; other docs link to it instead of repeating it.
- Technical details that belong to one project go in that project's `docs/` folder. Root `docs/technical.md` keeps only whole-system architecture and links to the project docs.
- Keep every doc at 200 lines or fewer. Split a doc before it goes over.
- When an epic's last story ships, move the epic from `docs/epics.md` to `docs/implemented.md` in the same change.

## Commands

@docs/development.md
