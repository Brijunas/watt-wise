# Watt-Wise — MVP Epics

Epics are ordered by dependency. Each one is expected to be delivered end to end (backend, frontend, tests) before the next starts, except where noted. `docs/product.md` and `docs/technical.md` remain the source of truth; an epic never overrides them.

## E1. Repository scaffolding and tooling

Scaffolds the repository structure and the shared JavaScript tooling only. No application is created here: `frontend/`, `admin/` and `backend/` exist as placeholder folders, and the real apps and solution are created in E2 and E3. Delivers the pnpm workspace, root `tsconfig.base.json`, ESLint and Prettier configs, the four shared package skeletons, placeholder `deploy/` and `.github/workflows/` folders and the top-level README. Done when `pnpm install`, `pnpm lint` and `pnpm test` are green on the skeleton and the Commands section of `CLAUDE.md` lists the commands that exist.

### Stories

- **S1.1 Root workspace and git hygiene.** `pnpm-workspace.yaml` listing `frontend`, `admin`, `packages/*`; root `package.json` with `packageManager` pinned and `lint`, `format`, `test`, `build` scripts fanning out with `pnpm -r`; `.gitignore`, `.editorconfig`, Node version file, `.npmrc` enforcing pnpm. Done: `pnpm install` succeeds.
- **S1.2 Shared TypeScript, ESLint and Prettier configs.** Root `tsconfig.base.json` (strict), ESLint flat config with TypeScript, React, React Hooks and Prettier integration, Prettier config. Done: `pnpm lint` and `pnpm format --check` run cleanly.
- **S1.3 Shared packages skeleton.** `packages/ui`, `core`, `i18n`, `api-client` as `@wattwise/<name>`, `private: true`, entry `src/index.ts`, no build step, framework libs as `peerDependencies`, `tsconfig.json` extending the base, shared Vitest config and one trivial passing test each. `api-client` holds a placeholder export until E3 wires codegen. Done: `pnpm -r test` and typecheck pass.
- **S1.4 Frontend folder.** `frontend/` created as an empty placeholder with a short README stating the app is created in E3. No `package.json`, so the workspace ignores it.
- **S1.5 Admin folder.** `admin/` created the same way as S1.4.
- **S1.6 Backend folder.** `backend/` created as an empty placeholder with a short README stating the solution is created in E2.
- **S1.7 Placeholder folders and docs.** `deploy/` and `.github/workflows/` with a short README each; top-level `README.md` describing the layout and how to run the checks.
- **S1.8 Fill in CLAUDE.md Commands.** Replace the placeholder with the pnpm commands that exist after this epic, including how to run a single test in a package. Done: every listed command has been executed and works.

## E2. Backend foundation

Creates `backend/WattWise.sln` with all `src/` and `tests/` projects, `Directory.Build.props` and central package management, `.editorconfig`, analyzers with warnings as errors, and the Husky pre-commit hook for C# formatting. Clean Architecture skeleton with the Domain ← Application ← Infrastructure ← hosts dependency direction enforced. EF Core code-first on PostgreSQL 18.6 with the initial migration. MediatR-style pipeline with the FluentValidation behavior, RFC 9457 ProblemDetails, Serilog + OpenTelemetry, `/health`, OpenAPI + Scalar in non-production. Testcontainers base for integration tests. Hangfire Postgres storage wired into `WattWise.Jobs` with a no-op job and its integration test. Extends the `CLAUDE.md` Commands section with the dotnet commands.

### Stories

- **S2.1 Solution and project skeleton.** The developer runs the `dotnet new` scaffolder for `WattWise.sln`, the five `src/` projects and the four `tests/` projects so the project format matches the current SDK. Claude then wires project references enforcing Domain ← Application ← Infrastructure ← hosts, adds `Directory.Build.props` (.NET 11, nullable, implicit usings, analyzers, `TreatWarningsAsErrors`), `Directory.Packages.props` for central package versions, `.editorconfig` for C# style and a backend `.gitignore`. Api and Jobs are empty hosts that start and stop; one trivial xUnit test per test project. Done: `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test` green.
- **S2.2 Architecture guard tests.** Architecture tests in Application.Tests asserting Domain references nothing but NodaTime, Application does not reference Infrastructure, and no layer references a host. Done: tests fail when a forbidden reference is added.
- **S2.3 Local database in Docker and persistence foundation.** `deploy/docker-compose.local.yml` starting PostgreSQL 18.6 with a named volume, plus the local connection settings in `appsettings.Development.json`. `WattWise.Infrastructure` gets `AppDbContext`, Npgsql provider, NodaTime mapping, snake_case naming, migrations folder and the initial empty migration, and a design-time factory so `dotnet ef` works from the repo. Migrations apply automatically on startup in `local` only; staging and production run them as an explicit deploy step (E5). Done: `docker compose up` then `dotnet run` yields a migrated database.
- **S2.4 Integration test base.** Shared fixture starting PostgreSQL 18.6 via Testcontainers, applying migrations and giving each test class a clean database. `WebApplicationFactory` for Api. One end-to-end test hitting a trivial endpoint proves the wiring. Done: `dotnet test` runs against a real container.
- **S2.5 Request pipeline.** In-house `IRequest`/`IRequestHandler` dispatcher in Application registered through DI (no MediatR licence dependency), pipeline behaviors for logging and FluentValidation validation, validation failures raised as a typed exception. Sample query wired to a sample endpoint with unit and integration tests. Done: an invalid request produces a validation error through the pipeline.
- **S2.6 Error handling and ProblemDetails.** Exception handler mapping validation, not-found, unauthorized and unexpected errors to RFC 9457 ProblemDetails with a `traceId`. Integration tests for each mapping. Done: every error response is ProblemDetails.
- **S2.7 API host plumbing.** `/api/v1` route group and versioning convention, OpenAPI document via built-in support, Scalar UI in non-production, CORS restricted to configured origins, `/health` with a database check. Done: OpenAPI JSON is served and valid, health reports healthy against the test database.
- **S2.8 Logging and observability.** Serilog with console JSON output and request logging, OpenTelemetry traces and metrics with an OTLP exporter configured per environment, log/trace correlation. Done: a request produces one structured log line and one trace.
- **S2.9 Jobs host and Hangfire.** `WattWise.Jobs` with Hangfire server, PostgreSQL storage in a dedicated schema, dashboard endpoint (access control comes in E4), a no-op recurring job as the template for thin job classes, configuration shared with Api. Jobs.IntegrationTests fixture running the job against the container. Done: the no-op job executes and its test passes.
- **S2.10 C# pre-commit hook.** Husky task running `dotnet format --include` on staged `.cs` files. Done: a badly formatted commit is rejected locally.
- **S2.11 CLAUDE.md Commands.** Extend the Commands section with build, format, test, single-test, local database and `dotnet ef` migration commands. Done: each command executed and works.

## E3. Frontend foundation (frontend + admin + shared packages)

Creates the `frontend/` and `admin/` Vite React TypeScript apps in the workspace and the Husky pre-commit hook with lint-staged for JS/TS. Both PWAs booting with the app shell cached. MUI CSS-variables theme with light/dark/auto, choice in `localStorage` and applied before first paint. React Router shell, RTK Query store. Packages `@wattwise/ui`, `@wattwise/core`, `@wattwise/i18n` (LT/EN) and `@wattwise/api-client` with the `@rtk-query/codegen-openapi` pipeline from the API's OpenAPI document. Vitest + React Testing Library set up in every app and package. Extends the `CLAUDE.md` Commands section with the app commands. Runs after E2 because the API client is generated from the real OpenAPI document.

### Stories

- **S3.1 Create the two Vite apps.** The developer runs `pnpm create vite` for `frontend/` and `admin/` with the React TypeScript template so the template matches the current release. Claude then adds them to the workspace, extends `tsconfig.base.json` and the shared ESLint/Prettier/Vitest configs, links the four `@wattwise/*` packages with `workspace:*` and adds one smoke component test each. Done: `pnpm --filter frontend dev/build/test` and the same for admin work.
- **S3.2 JS pre-commit hook.** Husky with lint-staged running ESLint and Prettier on staged JS/TS files, sharing the Husky setup with the C# hook from S2.10. Done: a commit with a lint error is rejected locally.
- **S3.3 MUI theme with light/dark/auto.** `@wattwise/ui` exports the MUI CSS-variables theme with `colorSchemes`, a `ThemeProvider` wrapper and a mode switch using `useColorScheme`. The choice is stored in `localStorage` and an inline `<head>` script applies it before first paint. Both apps use it. Done: no flash on reload in either mode, mode switch covered by RTL tests.
- **S3.4 App shell and routing.** React Router in both apps with a mobile-first layout from `@wattwise/ui`: app bar, bottom navigation on phone and side navigation from the `md` breakpoint up, placeholder routes and a not-found page. Done: shell renders correctly at `xs` and `md` in component tests.
- **S3.5 i18n.** `@wattwise/i18n` sets up react-i18next with LT and EN resources, browser-language detection, a language switch component and a typed key convention. Both apps wrap with it. Done: switching language re-renders the shell, tests cover detection and switching.
- **S3.6 RTK Query store and generated API client.** `@wattwise/api-client` gets the `@rtk-query/codegen-openapi` config pointing at the API's OpenAPI document from S2.7, a `pnpm generate:api` root script and the committed output for the current sample endpoint. `@wattwise/core` provides the base query with the API URL from Vite env and the Redux store factory. Both apps mount the store and call the sample endpoint on a page. Done: regenerating produces no diff, the sample call renders data in both apps.
- **S3.7 Forms foundation.** react-hook-form with zod resolver, shared zod helpers in `@wattwise/core`, MUI-bound form fields in `@wattwise/ui` with error display and localized messages. One demo form under test. Done: validation errors show localized messages.
- **S3.8 PWA.** `vite-plugin-pwa` in both apps with manifest, icons, app-shell precaching and a network-only rule for `/api`. Update prompt when a new service worker is available. Done: Lighthouse reports installable, API requests are never served from cache.
- **S3.9 Vite env and config.** `.env.example` per app and env files for local/staging/production with the API base URL, typed via `import.meta.env` declarations. Done: builds for each mode pick the correct API URL.
- **S3.10 CLAUDE.md Commands.** Extend the Commands section with app dev/build/test, single test and API client regeneration commands. Done: each command executed and works.

## E4. Accounts and authentication

ASP.NET Core Identity with username/password. JWT access token plus rotating refresh token. Register, login, logout and refresh endpoints. `Admin` role guarding `/api/v1/admin`. Frontend auth flow and protected routes; admin app login. The account model leaves room for OAuth logins later without exposing them.

## E5. Deployment and CI/CD

Dockerfiles for api, jobs, frontend and admin. Docker Compose files for local, staging and production on saturn. cloudflared configuration, no open ports. 1Password `op run` / `op inject` for secrets and `deploy/.env.example` referencing item paths. GitHub Actions: PR workflow builds, lints and tests all apps; main workflow pushes images to GHCR and deploys over SSH. From here on every epic ships to staging.

## E6. Observability and hardening

Log queries and traces for Serilog + OpenTelemetry. Hangfire dashboard access control. Rate limiting on auth and upload endpoints, upload size limits, security headers. Backup restore drill documented and performed once.

## E7. Consumption objects and CSV import

Consumption object CRUD, one per account in the UI while the model allows many. ESO CSV parser targeting exactly what Mano ESO exports, Europe/Vilnius interpreted and stored as UTC. Hourly rows keyed by `(consumption_object_id, hour_utc)` with upsert so repeated uploads merge. The CSV itself is discarded after import. Upload UI with clear validation feedback.

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
