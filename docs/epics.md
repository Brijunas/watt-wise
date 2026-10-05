# Watt-Wise — MVP Epics

Epics are ordered by dependency. Each one is expected to be delivered end to end (backend, frontend, tests) before the next starts, except where noted. `docs/product.md`, `docs/technical.md` and each project's own docs (e.g. `backend/docs/`) remain the source of truth; an epic never overrides them. When an epic's last story ships, the epic moves to [implemented.md](implemented.md).

## E2. Backend foundation

Creates `backend/WattWise.slnx` with all `src/` and `tests/` projects, `Directory.Build.props` and central package management, `.editorconfig`, the built-in analyzers (warnings reported, not fatal), and C# formatting added to the existing pre-commit hook. Clean Architecture skeleton with the Domain ← Application ← Infrastructure ← hosts dependency direction enforced. EF Core code-first on PostgreSQL 18.6 with the initial migration. MediatR-style pipeline with the FluentValidation behavior, RFC 9457 ProblemDetails, Serilog + OpenTelemetry, `/health`, OpenAPI + Scalar in non-production. Testcontainers base for integration tests. Hangfire Postgres storage wired into `WattWise.Jobs` with a no-op job and its integration test. Extends `docs/development.md` with the dotnet commands.

### Stories

- **S2.1 Solution and project skeleton.** The developer runs the `dotnet new` scaffolder for `WattWise.slnx`, the five `src/` projects and the five `tests/` projects (Domain.Tests, Application.Tests, Infrastructure.IntegrationTests, Api.IntegrationTests, Jobs.IntegrationTests) so the project format matches the current SDK. Claude then wires project references enforcing Domain ← Application ← Infrastructure ← hosts, adds `Directory.Build.props` (.NET 11, nullable, implicit usings, analyzers, `TreatWarningsAsErrors` off), `Directory.Packages.props` for central package versions, a `global.json` that agrees with the dotnet version in root `mise.toml` (or rolls forward), `.editorconfig` for C# style and a backend `.gitignore`. Api and Jobs are empty hosts that start and stop; one trivial xUnit test per test project. Done: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` green.
- **S2.3 Development database, roles and persistence foundation.**
  - `deploy/docker-compose.development.yml` runs PostgreSQL 18.6 (named volume; bind addresses, ports and credentials from 1Password) and pgAdmin.
  - `deploy/postgres/bootstrap.sql`, run by hand once as `admin` (documented in setup.md), creates the least-privilege roles (`admin`, `owner`, `cli`, `api`, `hangfire`, `backup`), the `wattwise` database, the `app` and `hangfire` schemas, default privileges and hardening, as described in [deploy/docs/postgres.md](../deploy/docs/postgres.md).
  - Every connection field (host, port, database, user, password, options) lives in the role's item in the `Watt Wise Development` 1Password vault, and so do the stack's bind addresses and ports and the Api listen URL. Nothing about connections is in the repo. An admin sets role passwords once with psql `\password`, and each app gets only its own role's fields through `op run` and its committed `.env.<environment>` reference file next to the project.
  - `WattWise.Infrastructure` gets `AppDbContext` (schema `app`), the Npgsql provider, NodaTime mapping, snake_case naming, the initial empty migration, and a design-time factory so `dotnet ef` works from the repo (`dotnet-ef` in the root tool manifest).
  - The new `WattWise.Cli` console app (System.CommandLine) has a `migrate` command and is the only thing that applies migrations; the Api never migrates.
  - Done: following the setup.md steps (`docker compose up`, bootstrap, `\password` per role, `WattWise.Cli migrate`), then the Api starts as `api`, giving a migrated database whose objects belong to `owner` and where each role can do only what it should.
- **S2.4 Integration test base.** Shared fixture starting PostgreSQL 18.6 via Testcontainers, running `deploy/postgres/bootstrap.sql`, applying migrations through the `WattWise.Cli` migrate code as `cli` and giving each test class a clean database, used by Infrastructure.IntegrationTests, Api.IntegrationTests and Jobs.IntegrationTests. One Infrastructure test proves the database round trip. `WebApplicationFactory` for Api. One end-to-end test hitting a trivial endpoint proves the wiring. Done: `dotnet test` runs against a real container.
- **S2.5 Request pipeline.** In-house `IRequest`/`IRequestHandler` dispatcher in Application registered through DI (no MediatR licence dependency), pipeline behaviors for logging and FluentValidation validation, validation failures raised as a typed exception. Sample query wired to a sample endpoint with unit and integration tests. Done: an invalid request produces a validation error through the pipeline.
- **S2.6 Error handling and ProblemDetails.** Exception handler mapping validation, not-found, unauthorized and unexpected errors to RFC 9457 ProblemDetails with a `traceId`. Integration tests for each mapping. Done: every error response is ProblemDetails.
- **S2.7 API host plumbing.** `/api/v1` route group and versioning convention, OpenAPI document via built-in support, Scalar UI in non-production, CORS restricted to configured origins, `/health` with a database check. Done: OpenAPI JSON is served and valid, health reports healthy against the test database.
- **S2.8 Logging and observability.** Serilog with console JSON output and request logging, OpenTelemetry traces and metrics with an OTLP exporter configured per environment, log/trace correlation. Done: a request produces one structured log line and one trace.
- **S2.9 Jobs host and Hangfire.** `WattWise.Jobs` with Hangfire server, PostgreSQL storage in the `hangfire` schema, installed by `WattWise.Cli migrate` as described under "Background jobs" in [backend/docs/architecture.md](../backend/docs/architecture.md#background-jobs), a `WattWise.Jobs/.env.development` reference file for the `hangfire` role, dashboard endpoint (access control comes in E4), the Jobs listen URL from a 1Password item like the Api's (`applicationUrl` removed from its launchSettings), a no-op recurring job as the template for thin job classes, configuration shared with Api. Jobs.IntegrationTests fixture running the job against the container. Done: the no-op job executes and its test passes.
- **S2.10 C# pre-commit hook.** Add a `*.cs` entry to the root `lint-staged.config.js` running `dotnet format --include` on the staged files. The Husky hook from S1.2 already runs lint-staged, so no new hook is needed. Done: a badly formatted commit is rejected locally.
- **S2.11 Backend commands in development.md.** Extend `docs/development.md` with build, format, test, single-test, Development database (compose up/down through `op run`, psql as `admin`, pgAdmin), `WattWise.Cli migrate` and `dotnet ef` migration commands. Done: each command executed and works.

## E3. Frontend foundation (frontend + admin + shared packages)

Creates the `frontend/` and `admin/` Vite React TypeScript apps in the workspace. Both PWAs booting with the app shell cached. MUI CSS-variables theme with light/dark/auto, choice in `localStorage` and applied before first paint. React Router shell, RTK Query store. Packages `@wattwise/ui`, `@wattwise/core`, `@wattwise/i18n` (LT/EN) and `@wattwise/api-client` with the `@rtk-query/codegen-openapi` pipeline from the API's OpenAPI document. Vitest + React Testing Library set up in every app and package. Extends `docs/development.md` with the app commands. Runs after E2 because the API client is generated from the real OpenAPI document.

### Stories

- **S3.1 Create the two Vite apps.** The developer runs `pnpm create vite` for `frontend/` and `admin/` with the React TypeScript template so the template matches the current release. Claude then adds them to the workspace, extends `tsconfig.base.json` and the shared ESLint/Prettier/Vitest configs, links the four `@wattwise/*` packages with `workspace:*` and adds one smoke component test each. Versions both apps share (React, MUI, Redux, i18next) go in a pnpm catalog in `pnpm-workspace.yaml` and are referenced as `catalog:`. Whenever an E3 story makes a package import a framework library (React, MUI, i18next, Redux), it adds that library to the package's `peerDependencies` and `devDependencies`; the apps own the real versions. Done: `pnpm --filter frontend dev/build/test` and the same for admin work.
- **S3.2 Pre-commit hook covers the apps.** Confirm the S1.2 hook (Husky + root `lint-staged.config.js`) lints and formats staged files in `frontend/` and `admin/`. Adjust the lint-staged globs or ESLint config only if app files are missed. Done: a commit with a lint error in either app is rejected locally.
- **S3.3 MUI theme with light/dark/auto.** `@wattwise/ui` exports the MUI CSS-variables theme with `colorSchemes`, a `ThemeProvider` wrapper and a mode switch using `useColorScheme`. The choice is stored in `localStorage` and an inline `<head>` script applies it before first paint. Both apps use it. Done: no flash on reload in either mode, mode switch covered by RTL tests.
- **S3.4 App shell and routing.** React Router in both apps with a mobile-first layout from `@wattwise/ui`: app bar, bottom navigation on phone and side navigation from the `md` breakpoint up, placeholder routes and a not-found page. Done: shell renders correctly at `xs` and `md` in component tests.
- **S3.5 i18n.** `@wattwise/i18n` sets up react-i18next with LT and EN resources, browser-language detection, a language switch component and a typed key convention. Both apps wrap with it. Done: switching language re-renders the shell, tests cover detection and switching.
- **S3.6 RTK Query store and generated API client.** `@wattwise/api-client` gets the `@rtk-query/codegen-openapi` config pointing at the API's OpenAPI document from S2.7, a `pnpm generate:api` root script and the committed output for the current sample endpoint. `@wattwise/core` provides the base query with the API URL from Vite env and the Redux store factory. Both apps mount the store and call the sample endpoint on a page. Done: regenerating produces no diff, the sample call renders data in both apps.
- **S3.7 Forms foundation.** react-hook-form with zod resolver, shared zod helpers in `@wattwise/core`, MUI-bound form fields in `@wattwise/ui` with error display and localized messages. One demo form under test. Done: validation errors show localized messages.
- **S3.8 PWA.** `vite-plugin-pwa` in both apps with manifest, icons, app-shell precaching and a network-only rule for `/api`. Update prompt when a new service worker is available. Done: Lighthouse reports installable, API requests are never served from cache.
- **S3.9 Vite env and config.** `.env.example` per app and env files for development/testing/staging/production with the API base URL, typed via `import.meta.env` declarations. Done: builds for each mode pick the correct API URL.
- **S3.10 App commands in development.md.** Extend `docs/development.md` with app dev/build/test, single test and API client regeneration commands. Done: each command executed and works.

## E4. Accounts and authentication

ASP.NET Core Identity with username/password. JWT access token plus rotating refresh token. Register, login, logout and refresh endpoints. `Admin` role guarding `/api/v1/admin`. Frontend auth flow and protected routes; admin app login. The account model leaves room for OAuth logins later without exposing them.

## E5. Deployment and CI/CD

Dockerfiles for api, jobs, cli, frontend and admin. Docker Compose files for Testing, Staging and Production, with a one-shot `cli migrate` service that api and jobs wait for (`depends_on: condition: service_completed_successfully`), and an admin-run `bootstrap.sql` step per database. 1Password vaults `Watt Wise Testing`, `Watt Wise Staging` and `Watt Wise Production`, one service account per environment. `.env.testing`, `.env.staging` and `.env.production` reference files per backend project. cloudflared configuration, no open ports. 1Password `op run` / `op inject` for secrets and `deploy/.env.example` referencing item paths. GitHub Actions: PR workflow builds, lints and tests all apps and fails on `dotnet ef migrations has-pending-model-changes`; main workflow pushes images to GHCR and deploys over SSH. Renovate opens dependency update PRs (npm, NuGet, Docker images, GitHub Actions, `mise.toml`) with a one-day minimum release age and grouped related packages, checked by the PR workflow. From here on every epic ships to staging.

## E6. Observability and hardening

Log queries and traces for Serilog + OpenTelemetry. Hangfire dashboard access control. Rate limiting on auth and upload endpoints, upload size limits, security headers. Backup restore drill documented and performed once.

## E7. Consumption objects and CSV import

Consumption object CRUD, one per account in the UI while the model allows many. ESO CSV parser targeting exactly what Mano ESO exports, Europe/Vilnius interpreted and stored as UTC; how DST days appear in the export is still open, see [known-issues.md](known-issues.md#eso-csv-format-around-dst-changes-is-unknown). Hourly rows keyed by `(consumption_object_id, hour_utc)` with upsert so repeated uploads merge. The CSV itself is discarded after import. Upload UI with clear validation feedback.

## E8. Consumption visualization

Rich, filterable chart of consumption at hourly, daily and monthly resolution using MUI X Charts. Period selection and summary statistics. Mobile-first.

## E9. Grid plan domain

ESO grid plan model with its pricing components. Household parameters (contracted power, phases) if the ESO tariff structure requires them; this epic resolves that open question in `docs/product.md`. Draft/published lifecycle. Grid plan ingestion job from the ESO source writing drafts. Admin UI to create, edit, publish and retire grid plans. Public catalog page for grid plans.

## E10. Provider plan domain

Supplier plan model built from typed pricing components: fixed rate, time-of-use 2- and 4-zone, spot margin, monthly fee, hybrid. Compatibility rules with grid plans so only valid pairs are offered. Draft/published lifecycle. Per-provider fetchers or scrapers, source chosen per provider, writing drafts with change detection so re-runs do not duplicate. Admin review, edit and publish UI. Public catalog page for supplier plans.

## E11. Spot price ingestion

Hangfire job fetching Nord Pool LT day-ahead hourly prices, ENTSO-E primary with Nord Pool or Litgrid open data as fallback. Storage as `(hour_utc, price_eur_mwh)`. Backfill covering at least the range users can upload, gap detection and refill. Admin visibility of price coverage.

## E12. Current plans baseline

User selects their current grid plan and current supplier plan from the catalog, or enters custom pricing when their plan is not listed. Stored per consumption object and editable.

## E13. Estimated cost engine

Pure Domain engine that estimates what a plan would have cost over a period from hourly consumption, spot prices and pricing components. It is explicitly an estimate, not actual billing: no generation, net metering or invoice reconciliation in MVP, but the inputs are shaped so those can be added later. Time-of-use zones evaluated in local time with correct 23- and 25-hour DST days. Monthly and yearly aggregation. Golden-case unit tests in `Domain.Tests`.

## E14. Comparison and ranking

Query that runs the engine over all published grid plans, supplier plans and valid grid+supplier combinations for the selected period and cost basis, ranks by estimated cost and computes estimated savings versus the baseline. Results UI with period and basis controls, defaulting to the last year and monthly basis, with the note that spot results are a backtest estimate, not a forecast.

## E15. GDPR: export and delete

Full data export bundle for an account. Full account deletion cascading through consumption objects, consumption rows and baselines. Confirmation flows in the UI.

## Out of scope for MVP

Prosumer support, multiple consumption objects in the UI, OAuth sign-in and price alerts are non-goals per `docs/product.md` and have no epics.
