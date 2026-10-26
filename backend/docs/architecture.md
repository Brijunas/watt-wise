# Backend architecture

How the Watt-Wise backend is built inside. The system-level picture (applications, API contract, data model, hosting, secrets) is in the root [technical.md](../../docs/technical.md); this document covers only what happens inside `backend/`. Testing is in [testing.md](testing.md), the domain model and use cases in [domain-model.md](domain-model.md), build settings and code style in [conventions.md](conventions.md), failures and the error contract in [error-handling.md](error-handling.md), logs, traces and metrics in [observability.md](observability.md), the Jobs host and Hangfire in [jobs.md](jobs.md).

## Stack

.NET 11, ASP.NET Core Minimal API, EF Core on PostgreSQL 18.6, Hangfire for background jobs.

## Solution layout

```
backend/
├── WattWise.slnx
├── Directory.Build.props         shared build settings, see conventions.md
├── Directory.Packages.props      central NuGet versions
├── .editorconfig                 C# style, layered on the root .editorconfig
├── .globalconfig                 analyzer severities without a source location, see conventions.md
├── src/
│   ├── WattWise.Domain/          entities, value objects, domain services
│   ├── WattWise.Application/     use cases, ports, validation, DTOs
│   ├── WattWise.Infrastructure/  EF Core, external adapters, identity
│   ├── WattWise.Api/             Minimal API host
│   ├── WattWise.Jobs/            Hangfire server host
│   └── WattWise.Cli/             maintenance command line: migrations, scripts
└── tests/
    ├── WattWise.Domain.Tests/
    ├── WattWise.Application.Tests/
    ├── WattWise.Infrastructure.IntegrationTests/
    ├── WattWise.Api.IntegrationTests/
    ├── WattWise.Jobs.IntegrationTests/
    └── WattWise.Testing/         shared PostgreSQL fixture for the integration tests
```

## Layers and dependency direction

Clean Architecture with three layers and three hosts. References point inward only:

```
Domain ← Application ← Infrastructure ← Api, Jobs, Cli
```

| Project        | Contains                                                                                                                                                                  | May reference                       |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------- |
| Domain         | Entities, value objects, domain services, including the tariff/cost calculation engine. No I/O.                                                                           | Nothing except NodaTime             |
| Application    | Use cases (command and query handlers), ports (repositories, `IUnitOfWork`, read ports, external data), validators, DTOs. No EF Core.                                     | Domain                              |
| Infrastructure | EF Core persistence, the repositories and read ports, external data adapters (ENTSO-E, Nord Pool, Litgrid, provider catalog fetchers and scrapers), ASP.NET Core Identity | Application (and Domain through it) |
| Api            | Endpoints, HTTP pipeline, composition root                                                                                                                                | Application, Infrastructure         |
| Jobs           | Hangfire server, recurring job registration, thin job classes, dashboard, composition root                                                                                | Application, Infrastructure         |
| Cli            | Commands (System.CommandLine) for migrations, one-off scripts and maintenance, composition root                                                                           | Application, Infrastructure         |

No layer references a host; MSBuild already rejects that as a project cycle. Each layer exposes a marker type (`DomainAssembly`, `ApplicationAssembly`, `InfrastructureAssembly`) so tests can point at its assembly.

How logic is split between Domain and Application, and how it reaches the database, is in [domain-model.md](domain-model.md).

## Hosts

Three hosts wire the layers together and are deployed separately:

- **WattWise.Api** runs the Minimal API only. It never touches Hangfire: it doesn't enqueue jobs and its `api` role has no access to the `hangfire` schema.
- **WattWise.Jobs** runs the Hangfire server, the only process that uses Hangfire: it registers recurring jobs, executes them and serves the Hangfire dashboard. Job implementations are Application use cases; the job classes in this project only call them. Those use cases read and write app data through `AppDbContext`, so the Jobs host's `jobs` role reaches both `app` and `hangfire`. It uses the Web SDK because it serves the dashboard over HTTP. Details in [jobs.md](jobs.md).
- **WattWise.Cli** is a console app built with System.CommandLine on the .NET Generic Host (configuration, DI and logging like the other hosts). It runs one command and exits with a non-zero code on failure, or 130 when cancelled (Ctrl+C or SIGTERM). `migrate` brings every schema up to date (EF Core migrations, Hangfire's tables) through `DatabaseMigrator` (Infrastructure). It is the only host that changes the schema, and it runs as a deploy step before Api and Jobs start. Later commands add one-off scripts and maintenance tasks.

## Configuration

- Each host's `appsettings.json` lists every key a secret lands in with an empty value, so the file shows where each secret goes. `appsettings.<Environment>.json` holds only non-secret per-environment settings. A required setting that is blank counts as missing; what a blank optional setting means (such as `Observability:OtlpEndpoint` or `Cors:AllowedOrigins`) is under its own section.
- The values come from 1Password as environment variables ([technical.md](../../docs/technical.md#cross-cutting)). In Development they come from the host's committed `.env.development` file (e.g. `WattWise.Api/.env.development`), which `op run` resolves at start. That file also sets the .NET environment name (`launchSettings.json` doesn't), so a host started without it runs as Production and stops on the missing settings. Deployed environments get the values from their compose stack.

## Request handling

Every use case is a request object (a query or a command) with exactly one handler in Application. Callers (Api endpoints, and later Hangfire jobs and CLI commands) never call a handler directly: they send the request through the mediator, which runs it through a fixed pipeline first:

```
endpoint / job / CLI command
  → mediator.Send(request)
    → LoggingBehavior      time and outcome of every use case
      → ValidationBehavior FluentValidation; an invalid request stops here
        → handler          the use case itself
```

- **Why a mediator.** Cross-cutting concerns (logging, validation, later transactions, permission checks, metrics) are written once as behaviors and apply to every use case, so handlers hold only business logic. Jobs and CLI commands that send a request get the same pipeline as HTTP calls. Endpoints stay thin: build the request, send it, map the `Result` to HTTP.
- **Library:** [`martinothamar/Mediator`](https://github.com/martinothamar/Mediator), source-generated and MIT-licensed (the choice Ardalis's Clean Architecture template made). The dispatch code is generated at compile time: no runtime reflection, and a request without a handler fails the build. MediatR is not used: from version 13 it needs a commercial licence key.
- **Conventions:** requests implement `IQuery<Result<T>>` or `ICommand<Result<T>>`; handlers implement `IQueryHandler<,>` / `ICommandHandler<,>` and return `ValueTask<Result<T>>`. The behaviors are constrained to `Result` responses, so a request that returns anything else would skip them silently; a unit test in Application.Tests fails if any Application request doesn't return `Result<>`.
- **Registration:** the source generator runs in the startup project, so each host that sends requests calls `AddMediator` (Api today; Jobs and Cli when they first send one) with the Application assembly, the behaviors in the order above, and scoped lifetime (handlers use the scoped `AppDbContext`). The options must be written inline in that call: the generator reads them from the source and generates `MediatorOptions` into the host, so they can't come from a shared helper. The behavior list is therefore written twice, in the Api's `Program.cs` and in Application.Tests' `PipelineHost`, and the two must match. No test guards this yet: the generator registers behaviors per request type at compile time, and the Api has no requests. The first real use case adds a test that resolves its behaviors from the Api host and checks their order. `AddApplication()` registers the FluentValidation validators.
- **Endpoints** are grouped in `IEndpointModule` classes in the Api (`Endpoints/`), registered by `AddEndpointModules()` and mapped by `MapEndpointModules()` inside the `/api/v1` route group, so a module maps relative paths. What else the Api serves is under [HTTP surface](#http-surface).
- **Errors:** expected failures are `Result` errors, unexpected ones are exceptions, and the Api turns both into RFC 9457 ProblemDetails. The model, the contract and how to add an error are in [error-handling.md](error-handling.md).

## HTTP surface

What the Api host serves besides the endpoints, all wired in `Program.cs`:

| Path               | What                                                                              | Environments       |
| ------------------ | --------------------------------------------------------------------------------- | ------------------ |
| `/api/v1/...`      | Every endpoint module, with the CORS policy                                       | All                |
| `/health`          | Health report as JSON                                                             | All                |
| `/openapi/v1.json` | OpenAPI document (built-in `Microsoft.AspNetCore.OpenApi`), title `Watt-Wise API` | All but Production |
| `/scalar`          | Scalar UI over that document; the `http` launch profile opens it                  | All but Production |

- **Build-time document:** `Microsoft.Extensions.ApiDescription.Server` also writes the same document on every build of the Api to the committed `backend/openapi/wattwise-api.json`, which the web clients' API client is generated from ([frontend architecture](../../frontend/docs/architecture.md#api-client)). Commit it with the endpoint change that alters it; a build that leaves it changed means the client needs regenerating. The generator runs `Program.cs` without starting the host and without settings, so while it runs (`OpenApiBuildGeneration.IsRunning`) the `ValidateOnStart` validators are removed; a normal start validates as before. Both documents are OpenAPI 3.0, because the client codegen can't read 3.1.2 or 3.2 ([known-issues.md](../../docs/known-issues.md#the-api-client-codegen-cant-read-openapi-312-or-32)).

- **Versioning:** a plain route group; the prefix lives only in `ApiRoutes.V1Prefix`. No versioning library: a breaking v2 would be a second group with its own OpenAPI document. The document is the contract the web clients' API client is generated from ([technical.md](../../docs/technical.md#backend)).
- **CORS:** one named policy (`CorsPolicy`), required by the `/api/v1` group only; `/health`, OpenAPI and Scalar are same-origin or non-browser. It allows the origins in `Cors:AllowedOrigins`, methods GET, POST, PUT, PATCH and DELETE, headers `Authorization` and `Content-Type`, no credentials (clients send bearer tokens), and caches preflights for 10 minutes. The origins are the frontend and admin URLs, so like every URL they come from 1Password: `.env.<environment>` maps `Cors__AllowedOrigins__0` and `__1` to the `frontend` and `admin` items' `url` fields. `appsettings.json` lists the key as an empty array. An empty list is valid and allows no cross-origin calls; a malformed origin (not absolute http/https, or with a path, query or trailing slash) stops the host at start (`CorsSettingsValidator`, `ValidateOnStart`). The policy is endpoint metadata, so a path under `/api/v1` that matches no endpoint gets its 404 without CORS headers, and a browser reports a CORS error instead of the ProblemDetails body.
- **Health:** `AddDbContextCheck<AppDbContext>("database")`. `HealthResponseWriter` returns `{ "status", "durationMs", "checks": [{ "name", "status", "durationMs", "description" }] }`, 200 when Healthy or Degraded and 503 when Unhealthy. It never includes exception text, since `/health` is public: when a check throws, the framework puts the exception message in `description`, so the writer drops the description of any check that has an exception.

## Persistence

- EF Core, code-first with migrations, Npgsql provider, PostgreSQL 18.6. The database roles, schemas and privileges are in [deploy/docs/postgres.md](../../deploy/docs/postgres.md).
- NodaTime types are mapped through the Npgsql NodaTime plugin; all timestamps are `timestamptz` in UTC.
- Table and column names are snake_case, through EFCore.NamingConventions. See [known-issues.md](../../docs/known-issues.md) for its EF Core 11 version gap.
- Application tables live in the `app` schema (`HasDefaultSchema("app")`). The history table `__ef_migrations_history` lives in its own `migrations` schema, which no app role can reach ([postgres.md](../../deploy/docs/postgres.md#schemas-and-privileges)).
- In `WattWise.Infrastructure/Persistence/`:
  - `AppDbContext`;
  - `AppDbContextOptions`, the single place that configures Npgsql, NodaTime, the history table and naming, used by both DI and the design-time factory;
  - `AppDbContextFactory`, the design-time factory for `dotnet ef`;
  - the migrations in `Migrations/`.
- `AddInfrastructure()` registers `AppDbContext`. The connection string is built from the `Database` section (`Host`, `Port`, `Name`, `Username`, `Password`, optional `Options`) through the validated `DatabaseSettings` options. The settings are read from the built configuration, so sources added after registration (test overrides) count, and `ValidateOnStart` stops the host at startup listing any missing key. `DatabaseSettings` is a class, not a record, so formatting it never prints the password.
- Each host connects as its own database role (`api`, `jobs`, `cli`). Connection pools use Npgsql's defaults; nothing about pooling is configured.
- The `Database` keys are filled like every secret ([Configuration](#configuration)). The design-time factory reads the same environment variables. `migrations add` works without them, and commands that connect run through `op run --env-file backend/src/WattWise.Cli/.env.development`. No connection data is in the repo.
- `DatabaseMigrator` (in `Persistence/Schema/`) is the one migrate code path: `WattWise.Cli migrate` runs it, and so does the integration-test fixture ([testing.md](testing.md#shared-postgresql-fixture)), so tests migrate exactly like a deployment. It only runs the registered schema steps in order; each step owns one schema:
  - `ISchemaStep` has a `Name` and an idempotent `ApplyAsync`. Every step runs on every migrate.
  - `EfCoreMigrationsStep` applies pending EF Core migrations (the `app` schema), then `HangfireStorageStep` installs or upgrades Hangfire's tables (the `hangfire` schema). Both use the migrating connection, so everything they create belongs to `owner`.
  - `AddSchemaSteps()` in Infrastructure's `DependencyInjection` registers them; the registration order is the run order.
  - `DatabaseSchemas` holds every schema name (`App`, `Hangfire`, `Migrations`); code never spells one out.
  - To add or change a schema, add or change its step and register it. `DatabaseMigrator`, the Cli and the fixture stay as they are.
- Migrations are applied only by `WattWise.Cli migrate`, in every environment, before Api and Jobs start. Its connection switches to the `owner` role (`Database:Options` = `-c role=owner`, from the `postgres-cli` item), so the objects it creates belong to `owner`. It runs migrations with no command timeout, since index builds and table rewrites can take long. Api and Jobs never migrate, and their roles have no DDL rights.
- Migrations stay compatible with the previous release. A release swaps the images of Staging and Production, and swapping back runs the previous release's code on the newer schema ([technical.md](../../docs/technical.md#hosting-and-operations)). So a migration only adds: tables, and columns that are nullable or have a default. Dropping or renaming something the previous release still uses waits for a later release (expand, then contract).
- Until the first deploy (E6) no database holds data worth keeping, so the schema is one `Initial` migration. A model change may regenerate it (remove the migration files, `migrations add Initial`) instead of adding a migration; every Development database then resets its `app` tables ([development.md](../../docs/development.md#migrations)). Whether a change regenerates `Initial` or adds a migration is decided per change, until regeneration is declared over; from then on the rule above applies to every migration.

## Background jobs

- Hangfire with PostgreSQL storage in the dedicated `hangfire` schema, used only by the Jobs host.
- Jobs runs Hangfire with `PrepareSchemaIfNecessary = false`. `WattWise.Cli migrate` installs and upgrades Hangfire's tables (`HangfireStorageStep`, see [Persistence](#persistence)), so the `jobs` role needs no DDL rights.
- Recurring cron jobs: spot price ingestion and catalog refresh, with retries.
- The dashboard is served by the Jobs host at `/hangfire`, for local requests only; it is never put on the internet ([security.md](../../docs/security.md#authorization)).
- Storage options, the job-class template and how to add a job are in [jobs.md](jobs.md).

## Authentication

ASP.NET Core Identity in Infrastructure, on `AppDbContext`, handles the email and password identity: password hashing, lockout, email tokens and (later) external OAuth logins. The rules it enforces are in the root [security.md](../../docs/security.md); the token flow seen by clients is in the root [technical.md](../../docs/technical.md#backend).

- **Three groups of tables** in the `app` schema:
  - **Account:** `accounts` (Domain `Account`).
  - **Authentication:** `identity_users` (one per account, `account_id` unique), `identity_user_logins` and `identity_user_tokens` (kept for OAuth, unused), `email_link_tokens` (one per identity and purpose).
  - **Authorization:** `roles`, `account_roles`, `permissions`, `role_permissions` (Domain `Role`, `Permission`).

  `AppDbContext` derives from `IdentityUserContext`, so Identity's own role and claim tables are not in the model.

  `account_roles` and `role_permissions` hold ids: `Account` references its roles and `Role` its permissions by id ([domain-model.md](domain-model.md#aggregates)).

- **Registration.** `AddInfrastructure()` registers Identity (its private `AddIdentity()`), its options and the email-link token provider, so every host has it with the same rules; only the Api uses it today.
- **Store options.** `IdentityOptions.Stores` stays at its defaults: Identity reads it from the app's services while building the model, and the design-time factory and the other hosts don't have them, so changing it would give them a different model.
- **Email links:** `EmailLinkTokenProvider` is Identity's token provider for email confirmation and password reset (`AddDefaultTokenProviders` isn't used, so Data Protection doesn't make these tokens). It stores a SHA-256 hash per identity and purpose in `email_link_tokens`; the lifetimes are in [security.md](../../docs/security.md#accounts). Checking a link marks its row deleted, and Identity saves that in the same `SaveChanges` as the confirmation or the new password. So a handler must not save anything else in the same scope after an Identity call fails; otherwise a rejected password would use up the link.
- **Clock:** code that needs the current time takes NodaTime's `IClock` (`SystemClock.Instance`, registered by `AddInfrastructure()`; `FakeClock` in tests).
- **No Data Protection key ring.** Nothing in the Api protects a payload with Data Protection. If a later feature needs it (cookies, antiforgery), it has to bring a persisted, encrypted key ring with it, decided in security.md first.
- **Email sending:** the Api sends directly over SMTP with MailKit, through an Application port ([technical.md](../../docs/technical.md#cross-cutting)). A failed send is logged and doesn't fail the request, since the user can ask for the email again.

## Logging and observability

Serilog writes JSON to the console, one request line per HTTP request. OpenTelemetry records traces and metrics, and every log event carries its trace id. Logs, traces and metrics go to an OTLP endpoint when one is configured. Every host sets this up with `AddObservability` from Infrastructure; the details are in [observability.md](observability.md). Health checks at `/health` with a database check, see [HTTP surface](#http-surface).

## Calculation engine

The engine is pure Domain code with no I/O. NodaTime is used in Domain for all date and time logic, so time-of-use zones are evaluated in Europe/Vilnius local time and DST days of 23 and 25 hours come out right. What the engine computes is under "Calculation engine" in the root [technical.md](../../docs/technical.md#calculation-engine).
