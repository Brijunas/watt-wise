# Backend architecture

How the Watt-Wise backend is built inside. The system-level picture (applications, API contract, data model, hosting, secrets) is in the root [technical.md](../../docs/technical.md); this document covers only what happens inside `backend/`. Testing is in [testing.md](testing.md), build settings and code style in [conventions.md](conventions.md), failures and the error contract in [error-handling.md](error-handling.md).

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

| Project        | Contains                                                                                                                                 | May reference                       |
| -------------- | ---------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------- |
| Domain         | Entities, value objects, domain services, including the tariff/cost calculation engine. No I/O.                                          | Nothing except NodaTime             |
| Application    | Use cases (command and query handlers), ports (interfaces) for persistence and external data, validators, DTOs                           | Domain                              |
| Infrastructure | EF Core persistence, external data adapters (ENTSO-E, Nord Pool, Litgrid, provider catalog fetchers and scrapers), ASP.NET Core Identity | Application (and Domain through it) |
| Api            | Endpoints, HTTP pipeline, composition root                                                                                               | Application, Infrastructure         |
| Jobs           | Hangfire server, recurring job registration, thin job classes, dashboard, composition root                                               | Application, Infrastructure         |
| Cli            | Commands (System.CommandLine) for migrations, one-off scripts and maintenance, composition root                                          | Application, Infrastructure         |

No layer references a host; MSBuild already rejects that as a project cycle. Each layer exposes a marker type (`DomainAssembly`, `ApplicationAssembly`, `InfrastructureAssembly`) so tests can point at its assembly.

## Hosts

Three hosts wire the layers together and are deployed separately:

- **WattWise.Api** runs the Minimal API only. It enqueues jobs through the shared Hangfire storage when a use case needs one, but it never runs a Hangfire server.
- **WattWise.Jobs** runs the Hangfire server: it registers recurring jobs, executes them and serves the Hangfire dashboard. Job implementations are Application use cases; the job classes in this project only call them. It uses the Web SDK because it serves the dashboard over HTTP.
- **WattWise.Cli** is a console app built with System.CommandLine on the .NET Generic Host (configuration, DI and logging like the other hosts). It runs one command and exits with a non-zero code on failure. `migrate` applies pending EF Core migrations through `DatabaseMigrator` (Infrastructure). It is the only host that changes the schema, and it runs as a deploy step before Api and Jobs start. Later commands add one-off scripts and maintenance tasks.

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
- **Registration:** the source generator runs in the startup project, so each host that sends requests calls `AddMediator` (Api today; Jobs and Cli when they first send one) with the Application assembly, the behaviors in the order above, and scoped lifetime (handlers use the scoped `AppDbContext`). The options must be written inline in that call: the generator reads them from the source and generates `MediatorOptions` into the host, so they can't come from a shared helper. `AddApplication()` registers the FluentValidation validators.
- **Endpoints** are grouped in `IEndpointModule` classes in the Api (`Endpoints/`), registered by `AddEndpointModules()` and mapped by `MapEndpointModules()`. Endpoints live in a `/api/v1` route group (S2.7). The OpenAPI document comes from the built-in .NET OpenAPI support, with Scalar UI in non-production environments.
- **Errors:** expected failures are `Result` errors, unexpected ones are exceptions, and the Api turns both into RFC 9457 ProblemDetails. The model, the contract and how to add an error are in [error-handling.md](error-handling.md).

## Persistence

- EF Core, code-first with migrations, Npgsql provider, PostgreSQL 18.6. The database roles, schemas and privileges are in [deploy/docs/postgres.md](../../deploy/docs/postgres.md).
- NodaTime types are mapped through the Npgsql NodaTime plugin; all timestamps are `timestamptz` in UTC.
- Table and column names are snake_case, through EFCore.NamingConventions. See [known-issues.md](../../docs/known-issues.md) for its EF Core 11 version gap.
- Application tables live in the `app` schema (`HasDefaultSchema("app")`), and so does the history table `__ef_migrations_history`.
- In `WattWise.Infrastructure/Persistence/`:
  - `AppDbContext`;
  - `AppDbContextOptions`, the single place that configures Npgsql, NodaTime, the history table and naming, used by both DI and the design-time factory;
  - `AppDbContextFactory`, the design-time factory for `dotnet ef`;
  - the migrations in `Migrations/`.
- `AddInfrastructure()` registers `AppDbContext`. The connection string is built from the `Database` section (`Host`, `Port`, `Name`, `Username`, `Password`, `MaxPoolSize`, optional `Options`) through the validated `DatabaseSettings` options. The settings are read from the built configuration, so sources added after registration (test overrides) count, and `ValidateOnStart` stops the host at startup listing any missing key. `DatabaseSettings` is a class, not a record, so formatting it never prints the password.
- `MaxPoolSize` is the Npgsql pool limit. It isn't secret, so each host sets it in its `appsettings.json`, below its role's connection limit ([postgres.md](../../deploy/docs/postgres.md#hardening)): Api 25, Cli 2.
- Each host connects as its own database role (`api`, `hangfire`, `cli`).
- Each host's `appsettings.json` lists every `Database` key with an empty value, so the file shows where each secret lands. The values come from the host's committed `.env.<environment>` file (e.g. `WattWise.Api/.env.development`), which `op run` resolves at start; blank values count as missing. That file also sets the .NET environment name (`launchSettings.json` doesn't), so starting without it runs as Production and fails on the missing settings. The design-time factory reads the same environment variables. `migrations add` works without them, and commands that connect run through `op run --env-file backend/src/WattWise.Cli/.env.development`. No connection data is in the repo.
- `DatabaseMigrator` (in `Persistence/`) is the one migrate code path: `WattWise.Cli migrate` runs it, and so does the integration-test fixture ([testing.md](testing.md#shared-postgresql-fixture)), so tests migrate exactly like a deployment.
- Migrations are applied only by `WattWise.Cli migrate`, in every environment, before Api and Jobs start. Its connection switches to the `owner` role (`Database:Options` = `-c role=owner`, from the `postgres-cli` item), so the objects it creates belong to `owner`. It runs migrations with no command timeout, since index builds and table rewrites can take long. Api and Jobs never migrate, and their roles have no DDL rights.

## Background jobs

- Hangfire with PostgreSQL storage in the dedicated `hangfire` schema, shared by Api and Jobs.
- Both run Hangfire with `PrepareSchemaIfNecessary = false`. From S2.9, `WattWise.Cli migrate` installs and upgrades Hangfire's tables (`PostgreSqlObjectsInstaller.Install`), so the `hangfire` role needs no DDL rights.
- Recurring cron jobs: spot price ingestion and catalog refresh, with retries.
- The dashboard is served by the Jobs host and restricted to admins.

## Authentication

ASP.NET Core Identity for users, password hashing, lockout and (later) external OAuth logins. The Api issues short-lived JWT access tokens and rotating refresh tokens; the token flow seen by clients is in the root technical.md.

## Logging and observability

Serilog structured logging to the console as JSON, with request logging. OpenTelemetry traces and metrics with an OTLP exporter configured per environment, and log/trace correlation. Health checks at `/health`, including a database check (`AddDbContextCheck<AppDbContext>`, minimal since S2.4; S2.7 extends it).

## Calculation engine

The engine is pure Domain code with no I/O. NodaTime is used in Domain for all date and time logic, so time-of-use zones are evaluated in Europe/Vilnius local time and DST days of 23 and 25 hours come out right. What the engine computes is described in the root technical.md.
