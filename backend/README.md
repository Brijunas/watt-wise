# backend

The Watt-Wise .NET 11 backend: the `WattWise.slnx` solution, following Clean Architecture, with five projects under `src/` (Domain, Application, Infrastructure, and the Api and Jobs hosts) and five test projects under `tests/`. It is built with the `dotnet` CLI and is never part of the pnpm workspace.

## Docs

- [architecture.md](docs/architecture.md): solution layout, layers and their dependency direction, hosts, request pipeline, persistence, jobs, logging.
- [testing.md](docs/testing.md): what each layer tests and the test projects.
- [conventions.md](docs/conventions.md): SDK and `global.json`, shared build settings, central package management, code style.

How the backend fits into the whole system (API contract, data model, hosting, secrets) is in the root [technical.md](../docs/technical.md).
