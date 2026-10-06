# Watt-Wise — Technical Specification

Companion to [product.md](product.md). This document records the architecture of the whole Watt-Wise system: the applications, how they talk to each other, the data model, hosting and the rules shared by every project. Technical details that belong to one project live in that project's own docs and are linked from here; for the backend, that is [backend/docs/](../backend/docs/).

## Repository

- **Mono repository.** One repo holds every application and shared package, and must scale to more related applications (e.g. an admin app, a scraper service) without restructuring.
- **Applications in MVP:**
  - `frontend/` — public web client
  - `admin/` — internal admin web client
  - `backend/` — one .NET solution producing two deployable hosts, the HTTP API (`WattWise.Api`) serving both clients and the Hangfire job server (`WattWise.Jobs`), plus the one-shot maintenance tool `WattWise.Cli`
- **Layout:** plain top-level folders per application, no task runner (no Turborepo/Nx). The JavaScript side is a **pnpm workspace** (`pnpm-workspace.yaml` listing `frontend`, `admin`, `packages/*`) so the two web apps can share private packages; pnpm is the only package manager used. `backend/` is built with the `dotnet` CLI and is not part of the workspace. Future applications are added as new top-level folders (and to the workspace if they are JavaScript). Shared, non-app content lives in `docs/` and `deploy/` (Docker Compose, config).

```
watt-wise/
├── docs/                      product.md, technical.md, epics.md, implemented.md, setup.md, development.md, known-issues.md
├── deploy/                    docker-compose.*.yml, .env.example, cloudflared config
├── .github/workflows/         CI/CD
├── pnpm-workspace.yaml        frontend, admin, packages/*
├── package.json               root scripts (lint, test, build all JS packages)
├── tsconfig.base.json         shared TypeScript settings
├── frontend/                  React PWA (Vite)
├── admin/                     React admin PWA (Vite)
├── packages/
│   ├── api-client/            generated RTK Query client + types
│   ├── ui/                    MUI theme (light/dark color schemes, mode switch), shared components, charts
│   ├── core/                  auth/token handling, zod schemas, utilities
│   └── i18n/                  react-i18next setup and shared resources
├── global.json                .NET SDK pin and test runner for the backend
└── backend/                   .NET solution (WattWise.slnx), layout in backend/docs/architecture.md
```

- **Tool versions:** declared in the root `mise.toml`, which every developer machine and CI uses through mise. Node (latest LTS) and pnpm (latest) float so the project stays current; .NET is pinned to an exact SDK only while the required major is prerelease, then floats too; the root `global.json` mirrors that pin for the `dotnet` CLI (details in [backend/docs/conventions.md](../backend/docs/conventions.md)). There is no `packageManager` field in `package.json`; mise is the only source of the pnpm version. JS dev dependencies also float (caret ranges of the latest release). The one exception is TypeScript: TS 7 provides `tsc`, and TS 6 is installed under the name `typescript` for ESLint and the editor until typescript-eslint supports TS 7 (see [known-issues.md](known-issues.md)).
- **Dependency updates:** done with pnpm's own commands; npm-check-updates is not a project dependency (it can be run ad hoc with `pnpm dlx npm-check-updates` for its `--doctor` or `--target minor` modes). `pnpm-workspace.yaml` sets `minimumReleaseAge: 1440`, so pnpm never installs a version published less than a day ago; this guards against compromised releases that npm pulls within hours. Routine refreshes stay within the existing ranges; major bumps are taken one at a time, after reading the changelog and checking [known-issues.md](known-issues.md). The commands are in [development.md](development.md#updating-dependencies). Manifests and `pnpm-lock.yaml` are committed together in a dedicated commit after lint, typecheck, test and build pass. Versions shared by `frontend` and `admin` (React, MUI and the like) go in a pnpm catalog in `pnpm-workspace.yaml`, so one update keeps both apps in sync. Once CI exists (E5), Renovate opens the update PRs for npm, NuGet, Docker images, GitHub Actions and `mise.toml`, with the same one-day minimum release age and related packages grouped into one PR.
- **Version control hosting:** GitHub.
- **CI/CD:** GitHub Actions. On pull request: build, lint and test all three applications. On merge to `main`: build Docker images, push to GitHub Container Registry, then deploy to saturn over SSH (`docker compose pull && docker compose up -d`).

## Frontend

- **Stack:** React + TypeScript, built with Vite, delivered as a PWA.
- **PWA scope:** installable with a manifest; the app shell is cached by a service worker (`vite-plugin-pwa`). API data is always fetched online; no offline data and no push notifications in MVP.
- **Component library:** MUI.
- **Responsive design:** mobile-first. Layouts are built for the smallest MUI breakpoint first and enhanced upward; navigation, tables and charts must be usable on a phone.
- **Appearance:** light, dark and auto modes using MUI's CSS-variables theme with `colorSchemes` and `useColorScheme`. Auto follows `prefers-color-scheme`; the user's choice is stored in `localStorage` and applied before first paint to avoid a flash.
- **Data fetching / server state:** RTK Query (Redux Toolkit).
- **Routing:** React Router.
- **Forms and validation:** react-hook-form with zod schemas.
- **Charts (consumption chart, catalog UI):** MUI X Charts.
- **i18n (Lithuanian and English):** react-i18next; default language picked from browser settings, switchable by the user.
- **API client:** generated from the backend OpenAPI document with `@rtk-query/codegen-openapi` into `packages/api-client`, consumed by both apps. Regenerated whenever the API changes; generated code is committed.
- **Testing:** Vitest with React Testing Library for unit and component tests. Playwright end-to-end tests added once the core flow exists.

## Shared frontend packages

- Shared code lives in `packages/<name>` as private workspace packages (`"private": true`, name `@wattwise/<name>`), consumed by the apps as `"@wattwise/<name>": "workspace:*"`.
- **Consumed from source.** Each package's entry point is `src/index.ts`, exposed through `"exports": { ".": "./src/index.ts" }`; there is no build step and no `dist/`. Vite compiles shared code as part of each app build, HMR works across packages, and TypeScript sees live types.
- **Single React/MUI instance.** Packages declare `react`, `react-dom`, `@mui/material` and other framework libraries as `peerDependencies`; only the apps own those versions. A package adds a peer (plus a matching devDependency for its own tests) when its code first imports the library, not ahead of time.
- **Configuration.** One root `tsconfig.base.json` extended by every app and package. ESLint and Prettier each have one root config (`eslint.config.js`, `.prettierrc.json`) and run once from the root across all apps and packages. Vitest is a root devDependency and its config is shared through the root `vitest.base.js`; each app and package has a `vitest.config.js` that merges it with `mergeConfig(base, defineProject({ ... }))` and adds its own options (name, environment). Every app and package has `test` (`vitest run`) and `typecheck` (`tsc`, TypeScript 7) scripts, fanned out by the root `pnpm test` and `pnpm typecheck`. TypeScript project references are added only if type-checking becomes slow.
- **Initial packages:** `api-client`, `ui`, `core`, `i18n`. New packages are created only when code is genuinely needed by more than one app.

## Admin application

- `admin/` — a separate web application in the monorepo for internal use: reviewing and publishing plan catalog entries produced by fetch/scrape jobs, correcting plan data, and monitoring jobs (link to the Hangfire dashboard hosted by `WattWise.Jobs`).
- **Stack:** identical to `frontend/` (React + TypeScript, Vite PWA with the same manifest and app-shell caching, MUI, RTK Query, generated API client, React Router, react-hook-form with zod, react-i18next, Vitest). Same mobile-first layout rules and light/dark/auto appearance. Served on its own subdomain.
- **Backend:** the same backend serves it. Admin endpoints live under `/api/v1/admin` and require the Identity `Admin` role. Admin users are created by a seed/CLI command, not by public sign-up.

## Backend

- **System view:** one .NET 11 solution following Clean Architecture, deployed as two long-running applications and one one-shot tool that share one PostgreSQL 18.6 database:
  - **WattWise.Api** — the HTTP API serving both web clients. It can enqueue background jobs but never runs them.
  - **WattWise.Jobs** — the Hangfire server: recurring jobs (spot price ingestion, catalog refresh) with retries, and the Hangfire dashboard for admins.
  - **WattWise.Cli** — the maintenance command line: applies database migrations (EF Core, and Hangfire storage from S2.9) as a deploy step before Api and Jobs start, and runs one-off scripts and maintenance tasks. It is the only application allowed to change the schema.
- **API contract with the clients:** REST over JSON, versioned under `/api/v1`. The OpenAPI document generated by the API is the contract the web clients' API client is generated from.
- **Authentication flow:** the API issues short-lived JWT access tokens plus rotating refresh tokens; the clients send the access token as a Bearer header via RTK Query.
- **Error contract:** RFC 9457 ProblemDetails for every error response, including validation errors, with a machine-readable `code` and a `traceId`. Details in [backend/docs/error-handling.md](../backend/docs/error-handling.md).
- **Inside the backend:** layers, hosts, request pipeline, persistence, jobs, logging, testing and build conventions are documented in [backend/docs/](../backend/docs/): [architecture.md](../backend/docs/architecture.md), [testing.md](../backend/docs/testing.md), [conventions.md](../backend/docs/conventions.md) and [error-handling.md](../backend/docs/error-handling.md).

## Data

- **Consumption data:** the uploaded CSV is parsed into hourly rows (`consumption_object_id`, `hour_utc`, `kwh`) with a unique index on object + hour. Repeated uploads merge by upsert. The original CSV is not stored. GDPR export is produced from the rows.
- **Spot prices:** hourly Nord Pool LT day-ahead prices (`hour_utc`, `price_eur_mwh`), stored in PostgreSQL, filled by a scheduled job.
- **Plan catalog:** grid plans and supplier plans stored relationally: a `plan` row (provider, kind grid/supplier, name, validity period, status draft/published/retired, allowed grid plans for supplier plans) with many **pricing component** rows. Each component has a typed kind and its parameters, e.g. fixed energy rate (EUR/kWh), time-of-use zone rate (zone definition + EUR/kWh), spot margin (EUR/kWh on top of Nord Pool price), monthly fee (EUR/month), and any future kind. The calculation engine sums all components of a plan; a new pricing structure is a new component kind, not a schema redesign. Fetch/scrape jobs write candidate entries as drafts; an admin reviews and publishes them through the admin application. Only published entries are visible to users.

## Calculation engine

- Pure code with no I/O in the backend Domain layer ([backend/docs/architecture.md](../backend/docs/architecture.md#calculation-engine)). Inputs: hourly consumption for the selected period, the candidate plans, and spot prices for the same hours. Output: cost per plan/combination per month and year, plus the delta against the user's current plans.
- Computed on demand per request. No caching in MVP; a year of hourly data against the full catalog is expected to take milliseconds.
- **Time handling:** all timestamps stored as UTC (`timestamptz`). The ESO CSV is interpreted as Europe/Vilnius local time on import. Time-of-use zone boundaries (day/night, 2- and 4-zone) are evaluated in local time so DST transitions (23- and 25-hour days) are handled correctly.

## Hosting and operations

- **Target:** hundreds of users on a single self-hosted server; optimize for low cost and simplicity.
- **Hosting platform:** self-hosted server "saturn", running Docker.
- **Deployment:** Docker images for api, jobs, cli (the migration step, run before api and jobs), frontend and admin, orchestrated with Docker Compose alongside PostgreSQL. Compose files and server config live in `deploy/`. Images are built by GitHub Actions and pulled on saturn.
- **Environments:** four, each with its own database, 1Password vault (`Watt Wise <Environment>`) and .NET environment name (`ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT`):
  - **Development** (`Development`): where code is written and run. It is the developer's own machine, or a server or another PC reached over SSH. Docker Compose runs the dependencies (`deploy/docker-compose.development.yml`) with ports bound to a loopback address; a remote machine is reached through an SSH tunnel.
  - **Testing** (`Testing`): deployed the same way as Production, but used for testing.
  - **Staging** (`Staging`): pre-production, the last check before a release.
  - **Production** (`Production`): the live service.
  - Testing, Staging and Production are Compose stacks with separate databases and subdomains; where each one runs is settled in E5.
- **Database:** one PostgreSQL 18.6 cluster per environment with least-privilege roles: a human-only superuser, a non-login role that owns every object, and one login per service. Schema changes go only through `WattWise.Cli`. Roles, schemas, privileges and hardening are described in [deploy/docs/postgres.md](../deploy/docs/postgres.md).
- **TLS / edge:** Cloudflare terminates public HTTPS. A `cloudflared` container in each Compose stack opens a Cloudflare Tunnel and routes the frontend, admin, API, and Hangfire dashboard subdomains to their containers. No ports are opened on saturn and no reverse proxy is needed on the box.
- **Backups:** already handled by saturn's existing backup setup; nothing to build.

## Cross-cutting

- **Security:** HTTPS only (Cloudflare edge), Identity password hashing and lockout, CORS restricted to the frontend and admin origins ([backend/docs/architecture.md](../backend/docs/architecture.md#http-surface)), ASP.NET rate limiting on auth endpoints, refresh-token rotation with revocation on logout and account delete.
- **Secrets and configuration:** 1Password is the source of truth for all secrets (database passwords, JWT signing key, Cloudflare Tunnel token, external API keys). Non-secret configuration is `appsettings.*.json` and Vite env files committed to git. Secrets are injected as environment variables at runtime: on saturn and in GitHub Actions via the 1Password CLI with a service account (`op run` / `op inject` rendering the Compose `.env`), and for each backend process with `op run --env-file <project>/.env.<environment>` (through the 1Password desktop integration on a developer machine, a service account per environment on servers). Each backend project has one committed reference file per environment (`.env.development`, `.env.testing`, `.env.staging`, `.env.production`). It sets the .NET environment name and maps each setting to a field in that environment's vault, so the file you pass picks both the environment and the vault. No secret is ever committed: the reference files and the env files in `deploy/` hold only 1Password secret references (`op://...`), never values. `appsettings.json` lists every key a secret lands in with an empty value, and `appsettings.<Environment>.json` holds only non-secret per-environment settings. The pre-commit hook enforces this with a Betterleaks scan of the staged changes (`betterleaks` comes from root `mise.toml`; `.betterleaks.toml` keeps the built-in rules and allows only whole `KEY="op://..."` lines), and Claude Code is denied reading or editing local secret files (`.env*` except `.env.example`, keys and certificates) through `.claude/settings.json`.
- **GDPR:** account delete and data export implemented as backend use cases. Delete removes the Identity user, consumption objects, hourly rows, current-plan entries and refresh tokens in one transaction. Export returns a JSON archive of the same data.
- **External data sources:** spot prices from ENTSO-E Transparency Platform API (free token) as the primary source, with Nord Pool or Litgrid open data as fallback adapters behind the same Application port. Provider catalog sources are decided per provider during implementation (see product.md open questions); each provider gets its own Infrastructure adapter producing draft plans.
- **Code style / linting:** frontend, admin and shared packages use ESLint and Prettier from root-level shared configs.
  - ESLint: typescript-eslint `strictTypeChecked` + `stylisticTypeChecked`, `@eslint-react` (`eslint-plugin-react` doesn't support ESLint 10), `eslint-plugin-react-hooks`, and `eslint-config-prettier` last.
  - Prettier: single quotes, no semicolons, 100-column lines, otherwise defaults.
  - Backend: `.editorconfig`, `dotnet format` and the built-in .NET analyzers; see [backend/docs/conventions.md](../backend/docs/conventions.md).
  - One Husky pre-commit hook first runs a Betterleaks secret scan on the staged changes, then a single root `lint-staged.config.js` that lints and formats staged files (JS/TS now, C# from S2.10).
  - Claude Code runs Prettier on every file it writes or edits (a `PostToolUse` hook in `.claude/settings.json`).
  - All of it is enforced again in CI.
