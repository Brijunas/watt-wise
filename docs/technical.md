# Watt-Wise — Technical Specification

Companion to [product.md](product.md). This document records the technical decisions needed to start writing epics.

## Repository

- **Mono repository.** One repo holds every application and shared package, and must scale to more related applications (e.g. an admin app, a scraper service) without restructuring.
- **Applications in MVP:**
  - `frontend/` — public web client
  - `admin/` — internal admin web client
  - `backend/` — one .NET solution producing two deployable hosts: the HTTP API (`WattWise.Api`) serving both clients, and the Hangfire job server (`WattWise.Jobs`)
- **Layout:** plain top-level folders per application, no task runner (no Turborepo/Nx). The JavaScript side is a **pnpm workspace** (`pnpm-workspace.yaml` listing `frontend`, `admin`, `packages/*`) so the two web apps can share private packages; pnpm is the only package manager used. `backend/` is built with the `dotnet` CLI and is not part of the workspace. Future applications are added as new top-level folders (and to the workspace if they are JavaScript). Shared, non-app content lives in `docs/` and `deploy/` (Docker Compose, config).

```
watt-wise/
├── docs/                      product.md, technical.md
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
└── backend/
    ├── WattWise.sln
    ├── src/
    │   ├── WattWise.Domain/
    │   ├── WattWise.Application/
    │   ├── WattWise.Infrastructure/
    │   ├── WattWise.Api/          Minimal API host
    │   └── WattWise.Jobs/         Hangfire server host
    └── tests/
        ├── WattWise.Domain.Tests/
        ├── WattWise.Application.Tests/
        ├── WattWise.Api.IntegrationTests/
        └── WattWise.Jobs.IntegrationTests/
```
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
- **Consumed from source.** Each package's entry point is `src/index.ts`; there is no build step and no `dist/`. Vite compiles shared code as part of each app build, HMR works across packages, and TypeScript sees live types.
- **Single React/MUI instance.** Packages declare `react`, `react-dom`, `@mui/material` and other framework libraries as `peerDependencies`; only the apps own those versions.
- **Configuration.** One root `tsconfig.base.json` extended by every app and package. ESLint, Prettier and Vitest configs are shared the same way. TypeScript project references are added only if type-checking becomes slow.
- **Initial packages:** `api-client`, `ui`, `core`, `i18n`. New packages are created only when code is genuinely needed by more than one app.

## Admin application

- `admin/` — a separate web application in the monorepo for internal use: reviewing and publishing plan catalog entries produced by fetch/scrape jobs, correcting plan data, and monitoring jobs (link to the Hangfire dashboard hosted by `WattWise.Jobs`).
- **Stack:** identical to `frontend/` (React + TypeScript, Vite PWA with the same manifest and app-shell caching, MUI, RTK Query, generated API client, React Router, react-hook-form with zod, react-i18next, Vitest). Same mobile-first layout rules and light/dark/auto appearance. Served on its own subdomain.
- **Backend:** the same backend serves it. Admin endpoints live under `/api/v1/admin` and require the Identity `Admin` role. Admin users are created by a seed/CLI command, not by public sign-up.

## Backend

- **Stack:** .NET 11, ASP.NET Core Minimal API.
- **Architecture:** Clean Architecture with three layers:
  - **Domain** — entities, value objects, domain services (including the tariff/cost calculation engine), no external dependencies.
  - **Application** — use cases, ports (interfaces) for persistence and external data, validation, DTOs.
  - **Infrastructure** — EF Core persistence, external data adapters (Nord Pool / ENTSO-E / Litgrid, provider catalog fetchers and scrapers), scheduled jobs, identity.
  - Two host projects wire the layers together and are deployed as separate applications:
    - **WattWise.Api** — Minimal API host only. It exposes endpoints and enqueues nothing itself beyond what use cases require; it does not run a Hangfire server.
    - **WattWise.Jobs** — Hangfire server host. It registers recurring jobs and executes them, hosts the Hangfire dashboard, and references the same Domain, Application and Infrastructure projects. Job implementations are Application use cases invoked by thin Hangfire job classes in this project.
- **Persistence:** Entity Framework Core, code-first with migrations, PostgreSQL 18.6.
- **Authentication:** ASP.NET Core Identity for users, password hashing, lockout and (later) external OAuth logins. API issues short-lived JWT access tokens plus rotating refresh tokens; the SPA sends the access token as a Bearer header via RTK Query.
- **Background jobs:** Hangfire with PostgreSQL storage, running in the separate `WattWise.Jobs` host. Recurring cron jobs for spot price ingestion and catalog refresh, with retries. The Hangfire dashboard is served by the Jobs host and restricted to admins. The API can enqueue jobs through the shared Hangfire storage without running a server.
- **API style:** REST over JSON, versioned under `/api/v1`. OpenAPI document generated by the built-in .NET OpenAPI support, browsable via Scalar UI in non-production environments.
- **Request handling:** each endpoint maps to a command or query handler in the Application layer via a MediatR-style pipeline. Cross-cutting concerns (validation, logging, transactions) are pipeline behaviors.
- **Validation:** FluentValidation, executed as a pipeline behavior before the handler.
- **Error format:** RFC 9457 ProblemDetails for all error responses, including validation errors.
- **Logging / observability:** Serilog structured logging (console/JSON), OpenTelemetry traces and metrics, health checks at `/health`.
- **Testing:** xUnit. Unit tests for Domain and Application (calculation engine, CSV parser, plan pricing model). Integration tests against a real PostgreSQL started with Testcontainers, split by host:
  - `WattWise.Api.IntegrationTests` — endpoints, auth, persistence.
  - `WattWise.Jobs.IntegrationTests` — each Hangfire job run end to end against Postgres with stubbed external sources (spot price ingestion writes the expected rows, catalog refresh produces draft plans, retries and idempotent re-runs behave correctly).

## Data

- **Consumption data:** the uploaded CSV is parsed into hourly rows (`consumption_object_id`, `hour_utc`, `kwh`) with a unique index on object + hour. Repeated uploads merge by upsert. The original CSV is not stored. GDPR export is produced from the rows.
- **Spot prices:** hourly Nord Pool LT day-ahead prices (`hour_utc`, `price_eur_mwh`), stored in PostgreSQL, filled by a scheduled job.
- **Plan catalog:** grid plans and supplier plans stored relationally: a `plan` row (provider, kind grid/supplier, name, validity period, status draft/published/retired, allowed grid plans for supplier plans) with many **pricing component** rows. Each component has a typed kind and its parameters, e.g. fixed energy rate (EUR/kWh), time-of-use zone rate (zone definition + EUR/kWh), spot margin (EUR/kWh on top of Nord Pool price), monthly fee (EUR/month), and any future kind. The calculation engine sums all components of a plan; a new pricing structure is a new component kind, not a schema redesign. Fetch/scrape jobs write candidate entries as drafts; an admin reviews and publishes them through the admin application. Only published entries are visible to users.

## Calculation engine

- Lives in the Domain layer as pure code with no I/O. Inputs: hourly consumption for the selected period, the candidate plans, and spot prices for the same hours. Output: cost per plan/combination per month and year, plus the delta against the user's current plans.
- Computed on demand per request. No caching in MVP; a year of hourly data against the full catalog is expected to take milliseconds.
- **Time handling:** all timestamps stored as UTC (`timestamptz`). The ESO CSV is interpreted as Europe/Vilnius local time on import. Time-of-use zone boundaries (day/night, 2- and 4-zone) are evaluated in local time so DST transitions (23- and 25-hour days) are handled correctly. NodaTime is used in the Domain for all date/time logic.

## Hosting and operations

- **Target:** hundreds of users on a single self-hosted server; optimize for low cost and simplicity.
- **Hosting platform:** self-hosted server "saturn", running Docker.
- **Deployment:** Docker images for api, jobs, frontend and admin, orchestrated with Docker Compose alongside PostgreSQL. Compose files and server config live in `deploy/`. Images are built by GitHub Actions and pulled on saturn.
- **Environments:** `local` (Docker Compose on the developer machine), `staging` and `production` (two Compose stacks on saturn, separate databases and subdomains).
- **TLS / edge:** Cloudflare terminates public HTTPS. A `cloudflared` container in each Compose stack opens a Cloudflare Tunnel and routes the frontend, admin, API, and Hangfire dashboard subdomains to their containers. No ports are opened on saturn and no reverse proxy is needed on the box.
- **Backups:** already handled by saturn's existing backup setup; nothing to build.

## Cross-cutting

- **Security:** HTTPS only (Cloudflare edge), Identity password hashing and lockout, CORS restricted to the frontend and admin origins, ASP.NET rate limiting on auth endpoints, refresh-token rotation with revocation on logout and account delete.
- **Secrets and configuration:** 1Password is the source of truth for all secrets (database passwords, JWT signing key, Cloudflare Tunnel token, external API keys). Non-secret configuration is `appsettings.*.json` and Vite env files committed to git. Secrets are injected as environment variables at runtime: on saturn and in GitHub Actions via the 1Password CLI with a service account (`op run` / `op inject` rendering the Compose `.env`), locally via `op run` or the 1Password desktop integration. No secret is ever committed; `deploy/` holds `.env.example` templates referencing 1Password item paths.
- **GDPR:** account delete and data export implemented as backend use cases. Delete removes the Identity user, consumption objects, hourly rows, current-plan entries and refresh tokens in one transaction. Export returns a JSON archive of the same data.
- **External data sources:** spot prices from ENTSO-E Transparency Platform API (free token) as the primary source, with Nord Pool or Litgrid open data as fallback adapters behind the same Application port. Provider catalog sources are decided per provider during implementation (see product.md open questions); each provider gets its own Infrastructure adapter producing draft plans.
- **Code style / linting:** frontend, admin and shared packages use ESLint and Prettier from root-level shared configs. Backend uses `.editorconfig`, `dotnet format`, and the built-in .NET analyzers with warnings treated as errors. Husky pre-commit hooks run lint and format on staged files. All of it is enforced again in CI.
