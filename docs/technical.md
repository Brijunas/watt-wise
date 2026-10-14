# Watt-Wise — Technical Specification

Companion to [product.md](product.md). This document records the architecture of the whole Watt-Wise system: the applications, how they talk to each other, the data model, hosting and the rules shared by every project. Technical details that belong to one project live in that project's own docs and are linked from here; for the backend, that is [backend/docs/](../backend/docs/), and for the web apps and shared packages, [frontend/docs/](../frontend/docs/).

## Repository

- **Mono repository.** One repo holds every application and shared package, and must scale to more related applications (e.g. an admin app, a scraper service) without restructuring.
- **Applications in MVP:**
  - `frontend/` — public web client
  - `admin/` — internal admin web client
  - `backend/` — one .NET solution producing two deployable hosts, the HTTP API (`WattWise.Api`) serving both clients and the Hangfire job server (`WattWise.Jobs`), plus the one-shot maintenance tool `WattWise.Cli`
- **Layout:** plain top-level folders per application, no task runner (no Turborepo/Nx). The JavaScript side is a **pnpm workspace** (`pnpm-workspace.yaml` listing `frontend`, `admin`, `packages/*`) so the two web apps can share private packages; pnpm is the only package manager used. `backend/` is built with the `dotnet` CLI and is not part of the workspace. Future applications are added as new top-level folders (and to the workspace if they are JavaScript). Shared, non-app content lives in `docs/` and `deploy/` (Docker Compose, config).

```
watt-wise/
├── docs/                      product.md, technical.md, security.md, epics.md, implemented.md, setup.md, development.md, known-issues.md
├── deploy/                    docker-compose.*.yml, per-environment reference files, docs
├── .github/workflows/         CI/CD
├── pnpm-workspace.yaml        frontend, admin, packages/*
├── package.json               root scripts (lint, test, build all JS packages)
├── tsconfig.base.json         shared TypeScript settings
├── frontend/                  React PWA (Vite), docs for both apps and packages/ in frontend/docs/
├── admin/                     React admin PWA (Vite), follows frontend/docs/
├── packages/
│   ├── api-client/            generated RTK Query client + types
│   ├── ui/                    MUI theme (light/dark color schemes, mode switch), shared components, charts
│   ├── core/                  auth/token handling, zod schemas, utilities
│   └── i18n/                  react-i18next setup and shared resources
├── global.json                .NET SDK pin and test runner for the backend
└── backend/                   .NET solution (WattWise.slnx), layout in backend/docs/architecture.md
```

- **Tool versions:** declared in the root `mise.toml`, which every developer machine and CI uses through mise. Node (latest LTS) and pnpm (latest) float so the project stays current; .NET is pinned to an exact SDK only while the required major is prerelease, then floats too; the root `global.json` mirrors that pin for the `dotnet` CLI (details in [backend/docs/conventions.md](../backend/docs/conventions.md)). There is no `packageManager` field in `package.json`; mise is the only source of the pnpm version. JS dev dependencies also float (caret ranges of the latest release). The one exception is TypeScript: TS 7 provides `tsc`, and TS 6 is installed under the name `typescript` for ESLint and the editor until typescript-eslint supports TS 7 (see [known-issues.md](known-issues.md)).
- **Dependency updates:** done with pnpm's own commands; npm-check-updates is not a project dependency (it can be run ad hoc with `pnpm dlx npm-check-updates` for its `--doctor` or `--target minor` modes). `pnpm-workspace.yaml` sets `minimumReleaseAge: 1440`, so pnpm never installs a version published less than a day ago; this guards against compromised releases that npm pulls within hours. A refresh takes every source (pnpm, NuGet, the .NET SDK pin, Docker images) to its latest version, majors included: each major's release notes are read and whatever it breaks is fixed in the same change, or that one package stays on its old major with the reason recorded in [known-issues.md](known-issues.md) if upstream blocks it. The exceptions in known-issues.md (the `typescript` alias) and PostgreSQL's major ([postgres.md](../deploy/docs/postgres.md#version)) are never bumped this way. Claude runs a refresh with the `update-deps` skill (`.claude/skills/update-deps/`). The commands are in [development.md](development.md#updating-dependencies). Manifests and `pnpm-lock.yaml` are committed together in a dedicated commit after lint, typecheck, test and build pass. Versions shared by `frontend` and `admin` (React, MUI and the like) go in a pnpm catalog in `pnpm-workspace.yaml`, so one update keeps both apps in sync. Once CI exists (E5), Renovate (the Mend-hosted GitHub app, configured by `renovate.json`) opens the update PRs for npm, NuGet, Docker images, GitHub Actions and `mise.toml`, with the same one-day minimum release age and related packages grouped into one PR.
- **Version control hosting:** GitHub.
- **CI/CD:** GitHub Actions. On pull request to `main`: build, lint and test all three applications and build the images. A push to `main` builds and signs the images, pushes them to GitHub Container Registry tagged with the commit SHA and deploys them to Staging. A push to `dev` does the same for Testing; `dev` is used on demand, to try something on Testing first, and feature PRs still go to `main`. Production is released by hand with a swap (see [Hosting and operations](#hosting-and-operations)).

## Frontend

- **System view:** two React + TypeScript web apps built with Vite and delivered as PWAs, `frontend/` for users and `admin/` for internal use, sharing code through the private `@wattwise/*` packages in `packages/`. Both are mobile-first, in Lithuanian and English, with light, dark and auto appearance.
- **API contract with the backend:** the apps call the API only through the RTK Query client generated from its OpenAPI document into `@wattwise/api-client` (see "Backend").
- **Inside the frontend:** stack, appearance, i18n, the shared packages, folder structure, testing and code conventions are documented in [frontend/docs/](../frontend/docs/): [architecture.md](../frontend/docs/architecture.md), [structure.md](../frontend/docs/structure.md), [testing.md](../frontend/docs/testing.md) and [conventions.md](../frontend/docs/conventions.md). They cover `admin/` and `packages/*` too.

## Admin application

- `admin/` — a separate web application in the monorepo for internal use: reviewing and publishing plan catalog entries produced by fetch/scrape jobs, and correcting plan data. Jobs are monitored in the Hangfire dashboard, which is not part of this app and not on the internet ([security.md](security.md#authorization)).
- **Stack:** the same as `frontend/` ([frontend/docs/architecture.md](../frontend/docs/architecture.md#admin-application)), served on its own subdomain.
- **Backend:** the same backend serves it. Admin endpoints live under `/api/v1/admin` and require the Identity `Admin` role; how an account gets it is under "Authorization" in [security.md](security.md#authorization).

## Backend

- **System view:** one .NET 11 solution following Clean Architecture, deployed as two long-running applications and one one-shot tool that share one PostgreSQL 18.6 database:
  - **WattWise.Api** — the HTTP API serving both web clients. It never touches Hangfire; background work belongs to Jobs.
  - **WattWise.Jobs** — the Hangfire server: recurring jobs (spot price ingestion, catalog refresh) with retries, and the Hangfire dashboard.
  - **WattWise.Cli** — the maintenance command line: applies database migrations (EF Core, and Hangfire storage) as a deploy step before Api and Jobs start, and runs one-off scripts and maintenance tasks. It is the only application allowed to change the schema.
- **API contract with the clients:** REST over JSON, versioned under `/api/v1`. The OpenAPI document generated by the API is the contract the web clients' API client is generated from.
- **Authentication flow:** signing in with email and password returns a short-lived JWT access token in the response body and sets a rotating refresh token in a cookie. The clients send the access token as a Bearer header via RTK Query; after a reload or when the access token expires they call refresh, and the browser sends the cookie. Token lifetimes, cookie settings, sessions and account rules are in [security.md](security.md).
- **Error contract:** RFC 9457 ProblemDetails for every error response, including validation errors, with a machine-readable `code` and a `traceId`. Details in [backend/docs/error-handling.md](../backend/docs/error-handling.md).
- **Inside the backend:** layers, hosts, request pipeline, persistence, jobs, logging, testing and build conventions are documented in [backend/docs/](../backend/docs/): [architecture.md](../backend/docs/architecture.md), [testing.md](../backend/docs/testing.md), [conventions.md](../backend/docs/conventions.md), [error-handling.md](../backend/docs/error-handling.md), [observability.md](../backend/docs/observability.md) and [jobs.md](../backend/docs/jobs.md).

## Data

- **Consumption data:** the uploaded CSV is parsed into hourly rows (`consumption_object_id`, `hour_utc`, `kwh`) with a unique index on object + hour. Repeated uploads merge by upsert. The original CSV is not stored. GDPR export is produced from the rows.
- **Spot prices:** hourly Nord Pool LT day-ahead prices (`hour_utc`, `price_eur_mwh`), stored in PostgreSQL, filled by a scheduled job.
- **Plan catalog:** grid plans and supplier plans stored relationally: a `plan` row (provider, kind grid/supplier, name, validity period, status draft/published/retired, allowed grid plans for supplier plans) with many **pricing component** rows. Each component has a typed kind and its parameters, e.g. fixed energy rate (EUR/kWh), time-of-use zone rate (zone definition + EUR/kWh), spot margin (EUR/kWh on top of Nord Pool price), monthly fee (EUR/month), and any future kind. The calculation engine sums all components of a plan; a new pricing structure is a new component kind, not a schema redesign. Fetch/scrape jobs write candidate entries as drafts; an admin reviews and publishes them through the admin application. Only published entries are visible to users.

## Calculation engine

- Pure code with no I/O in the backend Domain layer ([backend/docs/architecture.md](../backend/docs/architecture.md#calculation-engine)). Inputs: hourly consumption for the selected period, the candidate plans, and spot prices for the same hours. Output: cost per plan/combination per month and year, plus the delta against the user's current plans.
- Computed on demand per request. No caching in MVP; a year of hourly data against the full catalog is expected to take milliseconds.
- **Time handling:** all timestamps stored as UTC (`timestamptz`). The ESO CSV is interpreted as Europe/Vilnius local time on import. Time-of-use zone boundaries (day/night, 2- and 4-zone) are evaluated in local time so DST transitions (23- and 25-hour days) are handled correctly.

## Hosting and operations

- **Target:** hundreds of users on a single server; optimize for low cost and simplicity.
- **Hosting platform:** the server "saturn", running Docker. The repo says nothing more about it ([security.md](security.md#deployment)).
- **Deployment:** Docker images for api, jobs, cli (the migration step, run before api and jobs), frontend and admin, orchestrated with Docker Compose alongside PostgreSQL. The frontend and admin images serve their static files with nginx (`nginxinc/nginx-unprivileged`). Compose files and server config live in `deploy/`. Images are built and signed by GitHub Actions and pushed to GitHub Container Registry. Deploys are pulled: a deploy moves an environment's tag in the registry, and an agent on saturn (a script on a systemd timer) notices the change, verifies the image signatures, runs `cli migrate` and brings the stack up, then reports the result to a GitHub deployment status. Nothing connects into saturn ([security.md](security.md#deployment)).
- **Releases:** Production is released by swapping the image versions of Staging and Production (the workflow swaps their tags): Production takes the images Staging runs and Staging takes Production's previous ones, each stack keeping its own database, vault and subdomains, and `cli migrate` running first on each. Swapping back is the rollback, which is why migrations stay compatible with the previous release ([architecture.md](../backend/docs/architecture.md#persistence)).
- **Environments:** four, each with its own database, 1Password vault (`Watt Wise <Environment>`) and .NET environment name (`ASPNETCORE_ENVIRONMENT` / `DOTNET_ENVIRONMENT`):
  - **Development** (`Development`): where code is written and run. It is the developer's own machine, or a server or another PC reached over SSH. Docker Compose runs the dependencies (`deploy/docker-compose.development.yml`) with ports bound to a loopback address; a remote machine is reached through an SSH tunnel.
  - **Testing** (`Testing`): deployed the same way as Production, but used for testing.
  - **Staging** (`Staging`): pre-production, the last check before a release.
  - **Production** (`Production`): the live service.
  - Testing, Staging and Production are Compose stacks on saturn, one Compose project each, with separate databases, volumes, Cloudflare tunnels and subdomains.
- **Database:** one PostgreSQL 18.6 cluster per environment with least-privilege roles: a human-only superuser, a non-login role that owns every object, and one login per service. Schema changes go only through `WattWise.Cli`. Roles, schemas, privileges and hardening are described in [deploy/docs/postgres.md](../deploy/docs/postgres.md).
- **TLS / edge:** Cloudflare terminates public HTTPS. Each environment has one remotely managed Cloudflare Tunnel: a `cloudflared` container in its Compose stack runs it with only the tunnel token, and the routes from the frontend, admin and API subdomains to their containers are set in the Cloudflare dashboard, so no hostname is in the repo. The Jobs host and its Hangfire dashboard get no route ([security.md](security.md#authorization)). No port is open to the network on saturn and no reverse proxy is needed on the box.
- **Observability:** every backend process writes JSON logs to stdout and sends logs, traces and metrics over OTLP to the endpoint its environment configures (1Password, like every URL). Development uses the Grafana LGTM container in the compose stack ([deploy/docs/lgtm.md](../deploy/docs/lgtm.md)). Testing, Staging and Production send to one shared Grafana LGTM stack on saturn, told apart by `deployment.environment.name`; no data leaves saturn. Details in [backend/docs/observability.md](../backend/docs/observability.md).
- **Backups:** each deployed stack (Testing, Staging, Production) runs `pg_dump` once a day as the `backup` role, encrypts the dump and keeps the last 2 in a folder on saturn, because a file copy of a running PostgreSQL data folder would not be a consistent backup. Copying that folder off the server and keeping the longer history is handled outside this repo. Built in E6, described in [deploy/docs/postgres.md](../deploy/docs/postgres.md); who can read the dumps is in [security.md](security.md#elsewhere).

## Cross-cutting

- **Security:** accounts, tokens, sessions, authorization, transport and limits are in [security.md](security.md).
- **Email:** the API sends transactional email (account confirmation, password reset) over SMTP, in the user's language (LT or EN). Development and Testing send to a Mailpit container that catches every message; Staging and Production send through Brevo (free plan, EU-hosted, 300 emails a day). The SMTP host, port and credentials come from 1Password like every other setting, so changing provider is a configuration change.
- **Secrets and configuration:** 1Password is the source of truth for all secrets (database passwords, JWT signing key, SMTP credentials, Cloudflare Tunnel token, external API keys). Non-secret configuration is `appsettings.*.json` and Vite env files committed to git. Secrets are injected as environment variables at runtime with the 1Password CLI. In Development each backend process starts with `op run --env-file <project>/.env.development` through the 1Password desktop integration; that committed reference file maps each setting to a field in the `Watt Wise Development` vault. Testing, Staging and Production have one committed `deploy/<environment>/compose.env` instead, which maps every container's settings to that environment's vault; the compose file passes each service only its own. saturn's deploy agent resolves it at deploy time (`op run --env-file deploy/<environment>/compose.env -- docker compose … up -d`) with that environment's read-only service account, so the values go straight into the containers' environment; one-off tasks on the server use the same wrapper. No secret is ever committed: the reference files hold only 1Password secret references (`op://...`), never values. How the backend hosts read them is under "Configuration" in [backend/docs/architecture.md](../backend/docs/architecture.md#configuration). The pre-commit hook enforces this with a Betterleaks scan of the staged changes (`betterleaks` comes from root `mise.toml`; `.betterleaks.toml` keeps the built-in rules and allows only whole `KEY="op://..."` lines), and Claude Code is denied reading or editing local secret files (`.env*` except `.env.example`, keys and certificates) through `.claude/settings.json`.
- **GDPR:** account delete and data export implemented as backend use cases. Delete removes the Identity user, consumption objects, hourly rows, current-plan entries and refresh tokens in one transaction. Export returns a JSON archive of the same data.
- **External data sources:** spot prices from ENTSO-E Transparency Platform API (free token) as the primary source, with Nord Pool or Litgrid open data as fallbacks behind one interface. Provider catalog sources are decided per provider during implementation (see product.md open questions); each provider gets its own adapter producing draft plans. Where the adapters sit is under "Layers and dependency direction" in [backend/docs/architecture.md](../backend/docs/architecture.md#layers-and-dependency-direction).
- **Code style / linting:** frontend, admin and shared packages use ESLint and Prettier from root-level shared configs.
  - ESLint: the rule set and the import rules are in [frontend/docs/conventions.md](../frontend/docs/conventions.md).
  - Prettier: single quotes, no semicolons, 100-column lines, otherwise defaults.
  - Backend: `.editorconfig`, `dotnet format` and the built-in .NET analyzers; see [backend/docs/conventions.md](../backend/docs/conventions.md).
  - One Husky pre-commit hook first runs a Betterleaks secret scan on the staged changes, then a single root `lint-staged.config.js` that lints and formats staged files (JS/TS and C#).
  - Claude Code runs Prettier on every file it writes or edits (a `PostToolUse` hook in `.claude/settings.json`).
  - All of it is enforced again in CI.
