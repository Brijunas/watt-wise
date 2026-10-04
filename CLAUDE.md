# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

Watt-Wise is a greenfield project. The root pnpm workspace, the shared lint/format tooling and the git hooks exist; no application code exists yet (`frontend/`, `admin/`, `packages/` and `backend/` are still to be created). `docs/product.md` and `docs/technical.md` are the source of truth and every implementation decision must match them. The documents in `docs/`:

- `docs/product.md` — what the product does (Lithuanian electricity plan comparison from ESO hourly consumption CSVs), MVP scope, non-goals, open questions.
- `docs/technical.md` — every technical decision: repo layout, stacks, architecture, data model, hosting, secrets, code style.
- `docs/epics.md` — the MVP epics and stories, in delivery order. Work is tracked here.
- `docs/setup.md` — machine setup: mise, pnpm install, git hooks, editor, troubleshooting. Update it when a story changes how the project is set up or run.
- `docs/known-issues.md` — upstream problems with temporary workarounds (currently: TypeScript 7 vs typescript-eslint). Check it before changing tool versions.

When a task needs a decision the specs do not cover, ask rather than invent. When a decision changes, update the relevant spec in the same change.

## Repository layout (as specified)

Monorepo with plain top-level folders and no task runner. JS side is a pnpm workspace (`frontend`, `admin`, `packages/*`); `backend/` is a .NET solution built with the `dotnet` CLI and is not in the workspace. pnpm is the only JS package manager.

- `frontend/` — public React + TypeScript Vite PWA.
- `admin/` — internal admin PWA, identical stack to `frontend/`.
- `packages/` — private `@wattwise/<name>` workspace packages consumed from source (`src/index.ts`, no build step, no `dist/`). Framework libs are `peerDependencies`; only apps own React/MUI versions. Initial packages: `api-client` (generated), `ui`, `core`, `i18n`.
- `backend/` — `WattWise.sln` with `src/` (Domain, Application, Infrastructure, Api, Jobs) and `tests/` (Domain.Tests, Application.Tests, Api.IntegrationTests, Jobs.IntegrationTests).
- `deploy/` — Docker Compose files, `.env.example` templates, cloudflared config.
- `.github/workflows/` — CI (PR: build/lint/test all apps) and CD (main: images to GHCR, SSH deploy to saturn).

## Commands

Tools (Node, pnpm, .NET, Betterleaks) come from the root `mise.toml`: run `mise install` first. Run everything from the repo root.

- `pnpm install` — install dependencies and the git hooks. pnpm only; npm and yarn are rejected.
- `pnpm lint` — ESLint over the whole repo; warnings fail.
- `pnpm format` / `pnpm format:check` — Prettier write / check.
- `pnpm test`, `pnpm build` — run the script in every workspace package that has one (none yet).
- `pnpm exec tsc -p <package>` — type-check with TypeScript 7 (`tsc6` for TypeScript 6).

Single-test, app and `dotnet` commands are added by S1.8, S2.11 and E3 as those parts are created.

## Architecture essentials

**Backend (.NET 11, Clean Architecture).** Dependency direction is strictly Domain ← Application ← Infrastructure ← hosts.

- _Domain_ has no external dependencies except NodaTime. The tariff/cost calculation engine lives here as pure code with no I/O.
- _Application_ holds command/query handlers (in-house MediatR-style dispatcher, no MediatR package), FluentValidation validators run as a pipeline behavior, and ports for persistence and external data.
- _Infrastructure_ implements ports: EF Core (code-first, PostgreSQL 18.6), Identity, spot-price adapters (ENTSO-E primary, Nord Pool/Litgrid fallback), per-provider catalog fetchers/scrapers.
- _WattWise.Api_ is a Minimal API host only. It never runs a Hangfire server; it may enqueue jobs through shared Hangfire Postgres storage.
- _WattWise.Jobs_ is the Hangfire server host and dashboard. Job classes are thin wrappers that invoke Application use cases.
- REST under `/api/v1`, admin endpoints under `/api/v1/admin` requiring the Identity `Admin` role. All errors are RFC 9457 ProblemDetails. Auth is JWT access token + rotating refresh token.

**Time handling.** Everything is stored as UTC `timestamptz`. The ESO CSV is interpreted as Europe/Vilnius on import. Time-of-use zone boundaries are evaluated in local time so 23/25-hour DST days work. Use NodaTime, never `DateTime` arithmetic, for this logic.

**Data model rules.** Consumption is hourly rows keyed by `(consumption_object_id, hour_utc)` with upsert on re-upload; the CSV itself is not stored. Plans are a `plan` row plus typed pricing component rows (fixed rate, TOU zone rate, spot margin, monthly fee, ...). The engine sums components, so a new pricing structure is a new component kind, never a schema redesign. Jobs write plans as `draft`; only admin-published plans are visible to users. The model must allow multiple consumption objects per account and OAuth logins later even though MVP exposes neither.

**Frontend/admin.** RTK Query with a client generated by `@rtk-query/codegen-openapi` into `packages/api-client` (regenerate on every API change, commit the output). React Router, react-hook-form + zod, MUI X Charts, react-i18next (LT/EN). Mobile-first: build for the smallest MUI breakpoint, then enhance upward. Light/dark/auto via MUI CSS-variables theme `colorSchemes`, with the choice in `localStorage` and applied before first paint. PWA scope is installable + cached app shell only; API data is always fetched online.

**Secrets.** 1Password is the source of truth, injected as env vars with `op run`/`op inject`. Non-secret config is `appsettings.*.json` and Vite env files. Never commit a secret; `deploy/.env.example` references 1Password item paths.

## Code style

- JS: shared root ESLint + Prettier configs, `tsconfig.base.json` extended by every app and package. Prettier style: single quotes, no semicolons, 100 columns.
- .NET: `.editorconfig`, `dotnet format`, built-in analyzers with warnings as errors.
- Husky pre-commit runs a Betterleaks secret scan, then the root `lint-staged.config.js` (lint/format on staged files); CI enforces the same. Never bypass it with `--no-verify`; if Betterleaks flags something, remove the secret instead.
- A Claude Code `PostToolUse` hook runs Prettier on every file Claude writes or edits, and `.claude/settings.json` denies reading local secret files and build output. These rules replace a `.claudeignore`, which Claude Code doesn't support.
