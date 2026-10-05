# CLAUDE.md (backend)

Notes for Claude Code when working in `backend/`. The rules themselves live in [docs/conventions.md](docs/conventions.md) and [docs/architecture.md](docs/architecture.md); read them before writing C#.

## C# files

- One type per file: each class, record, struct, interface or enum goes in its own file named after it. Only nested private types may share their parent's file. Don't add a helper type (validator, options, extension class) to an existing file; create a new one next to it.
- No unused `using` directives. Implicit usings are on, and a namespace nested inside another (e.g. `WattWise.Application.Tests` inside `WattWise.Application`) needs no `using` for the outer one. After a refactor, check that the usings it made unnecessary are gone.
- The `backend-unused-usings` hook (`.claude/hooks/`) runs after every edit of a `backend/**/*.cs` file and reports unused usings (IDE0005). When it reports one, remove it before moving on.

## Before committing

Run `dotnet build`, `dotnet format --verify-no-changes` and `dotnet test` from `backend/`; all must pass with 0 warnings. `dotnet format` also reports unused usings across the whole solution.
