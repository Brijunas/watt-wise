# Backend conventions

Build settings, package management and code style for the .NET solution. The layers and projects are described in [architecture.md](architecture.md).

## Solution and SDK

- **Solution file:** `backend/WattWise.slnx`, the XML solution format that `dotnet new sln` produces by default since .NET 10. It is shorter than `.sln` and merges cleanly.
- **SDK version:** the root `mise.toml` installs the .NET SDK. The root `global.json` pins the same version, so the `dotnet` CLI, IDEs and CI agree on it:
  - `rollForward: latestFeature` accepts newer feature bands and patches of the same major.
  - `allowPrerelease: true` is needed while .NET 11 is a release candidate.
  - `test.runner: Microsoft.Testing.Platform` makes `dotnet test` use MTP (see [testing.md](testing.md)).
- **Why `global.json` is at the repo root:** the SDK looks for `global.json` from the current directory upward, not from the project's folder. Every command runs from the repo root ([development.md](../../docs/development.md)), so a file inside `backend/` would not be found.
- **At .NET 11 GA:** switch `mise.toml` to `latest` and update `global.json` to the GA SDK with `allowPrerelease: false`, in the same change.

## Shared build settings

`backend/Directory.Build.props` applies to every project, so individual `.csproj` files only hold what is specific to them (SDK, references, packages):

| Property                  | Value     | Why                                                                                                                                                                                      |
| ------------------------- | --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TargetFramework`         | `net11.0` | One target for the whole solution.                                                                                                                                                       |
| `Nullable`                | `enable`  | Nullable reference types everywhere.                                                                                                                                                     |
| `ImplicitUsings`          | `enable`  | SDK default usings.                                                                                                                                                                      |
| `AnalysisLevel`           | `latest`  | The built-in .NET analyzers at the default rule set of the current SDK. Individual rules are tuned in `.editorconfig`.                                                                   |
| `EnforceCodeStyleInBuild` | `true`    | `.editorconfig` code style (IDE rules) is reported during `dotnet build`, not only in the IDE.                                                                                           |
| `TreatWarningsAsErrors`   | `false`   | A project decision: warnings are reported but don't fail the build. Keep the build at 0 warnings anyway. A rule that must block the build is set to `error` severity in `.editorconfig`. |

## Package management

Central package management: `backend/Directory.Packages.props` sets `ManagePackageVersionsCentrally` and holds one `PackageVersion` per NuGet package. A `.csproj` lists `<PackageReference Include="..." />` without a version. To add or bump a package, change the version in `Directory.Packages.props` only. The update policy (minimum release age, Renovate from E5) is in "Dependency updates" in the root [technical.md](../../docs/technical.md).

`backend/nuget.config` clears every inherited package source and adds only nuget.org. This keeps restores the same on every machine and in CI, whatever feeds a developer has configured for other work. With one source, NuGet also doesn't raise NU1507, the warning about central package management with several sources.

## Code style

- **`.editorconfig`:** `backend/.editorconfig` comes from `dotnet new editorconfig`. It doesn't set `root = true`, so it layers on top of the repo-root `.editorconfig` (UTF-8 without BOM, LF, final newline, trimmed whitespace). It adds 4-space indentation for `*.cs`, keeps XML and project files at 2 spaces, and holds the C# code style and naming rules.
- **`dotnet format`** applies `.editorconfig` (whitespace, code style, analyzers). The solution must pass `dotnet format --verify-no-changes`. From S2.10, the pre-commit hook runs it on staged `.cs` files.
- **One type per file:** each class, record, struct, interface or enum goes in its own file named after it, as the .NET guidelines recommend. Only nested private types may share their parent's file.
- **Unused usings:** IDE0005 is a warning in `.editorconfig`, so `dotnet format --verify-no-changes` and the IDE report unused `using` directives. The build doesn't: on build IDE0005 needs `GenerateDocumentationFile`, so `Directory.Build.props` silences the compiler's `EnableGenerateDocumentationFile` hint instead. For Claude Code, the `backend-unused-usings` hook in `.claude/hooks/` checks each `.cs` file right after it is edited.
- **Build-breaking diagnostics:** most rules are warnings, but a few are set to `error` in `.editorconfig` because they guard correctness:
  - `CS8509` (switch expression doesn't cover every value): a new `ErrorType` must get its HTTP status in `ErrorTypeMapping` ([error-handling.md](error-handling.md#error-categories)). `CS8524` (unnamed enum values) is off, so such switches need no `_` arm, which would hide the missing case.
  - `MSG0001`–`MSG0005` (Mediator source generator: duplicate, invalid or missing handler, invalid message): a request without exactly one valid handler would only fail at runtime. These diagnostics have no source location, which `.editorconfig` severities need, so they are set in `backend/.globalconfig` (`is_global = true`), which applies to every project in the solution.
- **JSON files** in `backend/` (`appsettings*.json`, `launchSettings.json`) are formatted by Prettier like the rest of the repo. `bin/`, `obj/` and `TestResults/` are listed in the root `.prettierignore`.
- **Ignored files:** `backend/.gitignore` comes from `dotnet new gitignore`, plus `appsettings.*.local.json` for local secret overrides.
