# Backend architecture

How the Watt-Wise backend is built inside. The system-level picture (applications, API contract, data model, hosting, secrets) is in the root [technical.md](../../docs/technical.md); this document covers only what happens inside `backend/`. Testing is in [testing.md](testing.md), build settings and code style in [conventions.md](conventions.md).

## Stack

.NET 11, ASP.NET Core Minimal API, EF Core on PostgreSQL 18.6, Hangfire for background jobs.

## Solution layout

```
backend/
├── WattWise.slnx
├── Directory.Build.props         shared build settings, see conventions.md
├── Directory.Packages.props      central NuGet versions
├── .editorconfig                 C# style, layered on the root .editorconfig
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
    └── WattWise.Jobs.IntegrationTests/
```

## Layers and dependency direction

Clean Architecture with three layers and two hosts. References point inward only:

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
- **WattWise.Cli** is a console app built with System.CommandLine on the .NET Generic Host (configuration, DI and logging like the other hosts). It runs one command and exits with a non-zero code on failure. `migrate` applies pending EF Core migrations. It is the only host that changes the schema, and it runs as a deploy step before Api and Jobs start. Later commands add one-off scripts and maintenance tasks.

## Request handling

- Each endpoint maps to one command or query handler in Application through an in-house MediatR-style dispatcher (`IRequest` / `IRequestHandler`, registered through DI, no MediatR licence dependency).
- Cross-cutting concerns (logging, validation, transactions) are pipeline behaviors around the handler.
- Validation uses FluentValidation, run as a pipeline behavior before the handler. Failures are raised as a typed exception.
- An exception handler maps validation, not-found, unauthorized and unexpected errors to RFC 9457 ProblemDetails with a `traceId`. ProblemDetails is the error contract with the clients (see the root technical.md).
- Endpoints live in a `/api/v1` route group. The OpenAPI document comes from the built-in .NET OpenAPI support, with Scalar UI in non-production environments.

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
- `AddInfrastructure(configuration)` registers `AppDbContext`. It builds the connection string from the `Database` section (`Host`, `Port`, `Name`, `Username`, `Password`, optional `Options`) in `DatabaseSettings`, and fails at startup listing any missing key.
- Each host connects as its own database role (`api`, `hangfire`, `cli`).
- Each host's `appsettings.json` lists every `Database` key with an empty value, so the file shows where each secret lands. The values come from the host's committed `.env.<environment>` file (e.g. `WattWise.Api/.env.development`), which `op run` resolves at start; blank values count as missing. That file also sets the .NET environment name (`launchSettings.json` doesn't), so starting without it runs as Production and fails on the missing settings. The design-time factory reads the same environment variables. `migrations add` works without them, and commands that connect run through `op run --env-file backend/src/WattWise.Cli/.env.development`. No connection data is in the repo.
- Migrations are applied only by `WattWise.Cli migrate`, in every environment, before Api and Jobs start. Its connection switches to the `owner` role (`Database:Options` = `-c role=owner`, from the `postgres-cli` item), so the objects it creates belong to `owner`. Api and Jobs never migrate, and their roles have no DDL rights.

## Background jobs

- Hangfire with PostgreSQL storage in the dedicated `hangfire` schema, shared by Api and Jobs.
- Both run Hangfire with `PrepareSchemaIfNecessary = false`. `WattWise.Cli migrate` installs and upgrades Hangfire's tables (`PostgreSqlObjectsInstaller.Install`), so the `hangfire` role needs no DDL rights.
- Recurring cron jobs: spot price ingestion and catalog refresh, with retries.
- The dashboard is served by the Jobs host and restricted to admins.

## Authentication

ASP.NET Core Identity for users, password hashing, lockout and (later) external OAuth logins. The Api issues short-lived JWT access tokens and rotating refresh tokens; the token flow seen by clients is in the root technical.md.

## Logging and observability

Serilog structured logging to the console as JSON, with request logging. OpenTelemetry traces and metrics with an OTLP exporter configured per environment, and log/trace correlation. Health checks at `/health`, including a database check.

## Calculation engine

The engine is pure Domain code with no I/O. NodaTime is used in Domain for all date and time logic, so time-of-use zones are evaluated in Europe/Vilnius local time and DST days of 23 and 25 hours come out right. What the engine computes is described in the root technical.md.
