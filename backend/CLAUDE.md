# CLAUDE.md (backend)

Notes for Claude Code when working in `backend/`. The rules themselves live in [docs/](docs/): read [docs/architecture.md](docs/architecture.md) and [docs/conventions.md](docs/conventions.md) before writing C#, [docs/domain-model.md](docs/domain-model.md) before writing Domain or Application code, and [docs/testing.md](docs/testing.md) before writing tests. When a change touches errors, logging or jobs, read [docs/error-handling.md](docs/error-handling.md), [docs/observability.md](docs/observability.md) or [docs/jobs.md](docs/jobs.md) first.

## C# files

- Put each type in the layer that "Layers and dependency direction" in [docs/architecture.md](docs/architecture.md#layers-and-dependency-direction) names. References point inward only; when one would point outward, add a port in Application instead of working around the rule.
- Handlers return expected failures as `Result<T>` errors; Domain throws and Application converts ("Failure model" in [docs/error-handling.md](docs/error-handling.md#failure-model)). Add a new error or category as "How to add …" there says.
- One type per file: each class, record, struct, interface or enum goes in its own file named after it. Only nested private types may share their parent's file. Don't add a helper type (validator, options, extension class) to an existing file; create a new one next to it.
- No unused `using` directives. Implicit usings are on, and a namespace nested inside another (e.g. `WattWise.Application.Tests` inside `WattWise.Application`) needs no `using` for the outer one. After a refactor, check that the usings it made unnecessary are gone.
- The `backend-unused-usings` hook (`.claude/hooks/`) runs after every edit of a `backend/**/*.cs` file and reports unused usings (IDE0005). When it reports one, remove it before moving on.

## Before committing

Run the backend [checks](../docs/development.md#checks) (`dotnet build`, `dotnet format --verify-no-changes`, `dotnet test`); all must pass with 0 warnings. `dotnet format` also reports unused usings across the whole solution.
