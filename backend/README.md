# backend

The Watt-Wise .NET 11 backend: the `WattWise.slnx` solution, following Clean Architecture, with six projects under `src/` (Domain, Application, Infrastructure, and the Api, Jobs and Cli hosts) and five test projects under `tests/`. It is built with the `dotnet` CLI and is never part of the pnpm workspace.

## Docs

- [architecture.md](docs/architecture.md): solution layout, layers and their dependency direction, hosts, configuration, request pipeline, persistence, jobs, logging.
- [testing.md](docs/testing.md): what each layer tests and the test projects.
- [conventions.md](docs/conventions.md): SDK and `global.json`, shared build settings, central package management, code style.
- [error-handling.md](docs/error-handling.md): the failure model, `Result<T>` and the ProblemDetails contract clients see.
- [observability.md](docs/observability.md): Serilog JSON logs, OpenTelemetry traces and metrics, OTLP export.
- [jobs.md](docs/jobs.md): the Jobs host, Hangfire storage, job classes and the dashboard.

How the backend fits into the whole system (API contract, data model, hosting, secrets) is in the root [technical.md](../docs/technical.md).
