# Backend test refactor: baseline

The numbers the rewrite is measured against, collected on `4ab46ca` (2026-10-27) with every test passing. Part of the [inventory](inventory.md).

Working tree clean before and after.

## Environment

| Item              | Value                                                                                   |
| ----------------- | --------------------------------------------------------------------------------------- |
| .NET SDK          | 11.0.100-rc.3.26522.148                                                                 |
| Docker            | 29.9.2 (build a82e528)                                                                  |
| CPU count (nproc) | 16                                                                                      |
| Stryker.NET       | 5.0.0 (MTP runner, "preview" warning)                                                   |
| Postgres image    | postgres:18.6-trixie, already pulled before the first run (no image pull in either run) |
| OS                | Linux 7.2.11-200.fc44.x86_64                                                            |

## Build

`dotnet build backend/WattWise.slnx`: 0 warnings, 0 errors (after `dotnet tool restore`; build was incremental, 5.8s).

## Tests (`dotnet test --project <path> --no-build`, run one at a time)

Total / passed / failed / skipped are identical in both runs. No test failed. MTP duration is the "duration:" line printed by MTP; wall is measured around the whole command (includes process start and host/container start).

| Project                                  | Total   | Passed  | Failed | Skipped | Run 1 MTP | Run 1 wall (s) | Run 2 MTP | Run 2 wall (s) |
| ---------------------------------------- | ------- | ------- | ------ | ------- | --------- | -------------- | --------- | -------------- |
| WattWise.Domain.Tests                    | 14      | 14      | 0      | 0       | 413ms     | 0.82           | 415ms     | 0.83           |
| WattWise.Application.Tests               | 23      | 23      | 0      | 0       | 503ms     | 0.92           | 489ms     | 0.92           |
| WattWise.Infrastructure.IntegrationTests | 72      | 72      | 0      | 0       | 6s 756ms  | 7.20           | 6s 661ms  | 7.09           |
| WattWise.Api.IntegrationTests            | 41      | 41      | 0      | 0       | 6s 098ms  | 6.50           | 6s 069ms  | 6.47           |
| WattWise.Jobs.IntegrationTests           | 7       | 7       | 0      | 0       | 5s 483ms  | 5.89           | 5s 619ms  | 6.05           |
| **Sum**                                  | **157** | **157** | 0      | 0       | 19.3s     | 21.3           | 19.3s     | 21.4           |

Test counts from `--list-tests` match the run totals (Domain 14, Application 23, Infrastructure 72, Api 41, Jobs 7). Parameterised cases (`[Theory]` rows) count individually (e.g. the three `name: ""/" "/"\t\n"` rows in Domain).

## Stryker (full project; Domain and Application exit code 0, Infrastructure 2 because its score is under `break`; config backend/stryker-config.json, thresholds high 80 / low 60 / break 60)

Score = (killed + timeout) / (killed + timeout + survived + no coverage); "covered" score = (killed + timeout) / (killed + timeout + survived). Ignored and compile errors are excluded from both.

|                                                     | Domain  | Application | Infrastructure             |
| --------------------------------------------------- | ------- | ----------- | -------------------------- |
| Mutants created                                     | 32      | 42          | 397                        |
| Tested                                              | 24      | 32          | 226                        |
| Killed                                              | 18      | 21          | 116                        |
| Timeout                                             | 0       | 0           | 0                          |
| Survived                                            | 6       | 11          | 110                        |
| No coverage                                         | 2       | 0           | 84                         |
| Ignored ("Removed by block already covered filter") | 6       | 8           | 70                         |
| Compile errors                                      | 0       | 2           | 17                         |
| Mutation score (overall)                            | 69.23 % | 65.62 %     | 37.42 %                    |
| Covered score                                       | 75.00 % | 65.62 %     | 51.33 %                    |
| Stryker "Time Elapsed"                              | 6.56 s  | 7.27 s      | 173.8 s                    |
| Wall clock (incl. dotnet stryker start-up)          | 6.9 s   | 7.6 s       | 175 s                      |
| Tests found                                         | 14      | 23          | 72                         |
| Exit code                                           | 0       | 0           | 2 (score below `break` 60) |

Reports (git-ignored copies taken right after the baseline runs, so later runs don't overwrite them):

- Domain: `backend/StrykerOutput/Baseline/Domain/reports/mutation-report.html` and `.json`
- Application: `backend/StrykerOutput/Baseline/Application/reports/mutation-report.html` and `.json`
- Infrastructure: `backend/StrykerOutput/Baseline/Infrastructure/reports/mutation-report.html` and `.json` (run with the Infrastructure integration tests on a clean tree; a further 252 mutants in generated migrations are excluded by the mutate filter)

### Per-file score

Infrastructure (194 survived or uncovered mutants in 32 files) has its own table and survivor lists in [baseline-infrastructure.md](baseline-infrastructure.md) and [baseline-infrastructure-model.md](baseline-infrastructure-model.md).

Domain (all under backend/src/WattWise.Domain/):

| File                                        | Score  | Covered | Killed | Survived | No cov | Ignored |
| ------------------------------------------- | ------ | ------- | ------ | -------- | ------ | ------- |
| Accounts/Account.cs                         | 85.71  | 85.71   | 6      | 1        | 0      | 2       |
| Accounts/AccountRole.cs                     | 100.00 | 100.00  | 2      | 0        | 0      | 0       |
| Accounts/Permission.cs                      | 40.00  | 40.00   | 2      | 3        | 0      | 1       |
| Accounts/Role.cs                            | 75.00  | 75.00   | 6      | 2        | 0      | 3       |
| Accounts/RolePermission.cs                  | 100.00 | 100.00  | 2      | 0        | 0      | 0       |
| Exceptions/DomainException.cs               | 0.00   | N/A     | 0      | 0        | 2      | 0       |
| DomainAssembly.cs, Localization/Language.cs | N/A    | N/A     | 0      | 0        | 0      | 0       |

Application (all under backend/src/WattWise.Application/):

| File                                                                                                                     | Score  | Covered | Killed | Survived | No cov | Ignored | Compile err |
| ------------------------------------------------------------------------------------------------------------------------ | ------ | ------- | ------ | -------- | ------ | ------- | ----------- |
| About/GetAboutQueryHandler.cs                                                                                            | 0.00   | 0.00    | 0      | 5        | 0      | 1       | 0           |
| Behaviors/LoggingBehavior.cs                                                                                             | 100.00 | 100.00  | 3      | 0        | 0      | 3       | 0           |
| Behaviors/ValidationBehavior.cs                                                                                          | 66.67  | 66.67   | 2      | 1        | 0      | 1       | 2           |
| DependencyInjection.cs                                                                                                   | 0.00   | 0.00    | 0      | 2        | 0      | 1       | 0           |
| Results/Error.cs                                                                                                         | 80.00  | 80.00   | 4      | 1        | 0      | 1       | 0           |
| Results/Result.cs                                                                                                        | 85.71  | 85.71   | 12     | 2        | 0      | 1       | 0           |
| About/AboutResponse.cs, About/GetAboutQuery.cs, ApplicationAssembly.cs, Results/ErrorType.cs, Results/IFallibleResult.cs | N/A    | N/A     | 0      | 0        | 0      | 0       | 0           |

### Survived and NoCoverage mutants

Paths are repo-relative; line:column are the mutant's start location. Columns of original are the full source line (trimmed) containing the mutant.

#### Domain

| File                                                      | Line:col | Status     | Mutator                | Original line -> replacement                                                               | Verdict                                                |
| --------------------------------------------------------- | -------- | ---------- | ---------------------- | ------------------------------------------------------------------------------------------ | ------------------------------------------------------ |
| backend/src/WattWise.Domain/Accounts/Account.cs           | 44:9     | Survived   | Statement mutation     | `ArgumentOutOfRangeException.ThrowIfEqual(roleId, Guid.Empty);` -> (removed)               | killable: AddRole(Guid.Empty) throws                   |
| backend/src/WattWise.Domain/Accounts/Permission.cs        | 24:48    | Survived   | String mutation        | `public string Name { get; private set; } = string.Empty;` -> `"Stryker was here!"`        | equivalent: only EF's constructor sees the initializer |
| backend/src/WattWise.Domain/Accounts/Permission.cs        | 26:55    | Survived   | String mutation        | `public string Description { get; private set; } = string.Empty;` -> `"Stryker was here!"` | equivalent: only EF's constructor sees the initializer |
| backend/src/WattWise.Domain/Accounts/Permission.cs        | 31:9     | Survived   | Statement mutation     | `ArgumentNullException.ThrowIfNull(description);` -> (removed)                             | killable: null description throws                      |
| backend/src/WattWise.Domain/Accounts/Role.cs              | 22:48    | Survived   | String mutation        | `public string Name { get; private set; } = string.Empty;` -> `"Stryker was here!"`        | equivalent: only EF's constructor sees the initializer |
| backend/src/WattWise.Domain/Accounts/Role.cs              | 36:9     | Survived   | Statement mutation     | `ArgumentOutOfRangeException.ThrowIfEqual(permissionId, Guid.Empty);` -> (removed)         | killable: Grant(Guid.Empty) throws                     |
| backend/src/WattWise.Domain/Exceptions/DomainException.cs | 11:5     | NoCoverage | Block removal mutation | constructor body `{` -> `{}`                                                               | killable: a Domain test of a subclass keeps the code   |
| backend/src/WattWise.Domain/Exceptions/DomainException.cs | 17:5     | NoCoverage | Block removal mutation | constructor body `{` -> `{}`                                                               | killable: the inner exception is kept                  |

#### Application

| File                                                             | Line:col | Status   | Mutator                      | Original line -> replacement                                                                                                                          | Verdict                                                                    |
| ---------------------------------------------------------------- | -------- | -------- | ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| backend/src/WattWise.Application/About/GetAboutQueryHandler.cs   | 19:40    | Survived | Conditional (true) mutation  | `new AboutResponse(ApiName, string.IsNullOrWhiteSpace(version) ? FallbackVersion : version))` -> `(true?FallbackVersion :version)`                    | killable with a characterization value: the version isn't the fallback     |
| backend/src/WattWise.Application/About/GetAboutQueryHandler.cs   | 19:40    | Survived | Conditional (false) mutation | same line -> `(false?FallbackVersion :version)`                                                                                                       | equivalent while the SDK sets InformationalVersion                         |
| backend/src/WattWise.Application/About/GetAboutQueryHandler.cs   | 19:40    | Survived | String mutation              | same line -> `(version!=null)`                                                                                                                        | equivalent while the SDK sets InformationalVersion                         |
| backend/src/WattWise.Application/About/GetAboutQueryHandler.cs   | 19:40    | Survived | String mutation              | same line -> `(version!="")`                                                                                                                          | equivalent while the SDK sets InformationalVersion                         |
| backend/src/WattWise.Application/About/GetAboutQueryHandler.cs   | 19:40    | Survived | String mutation              | same line -> `(version.Trim()!="")`                                                                                                                   | equivalent while the SDK sets InformationalVersion                         |
| backend/src/WattWise.Application/Behaviors/ValidationBehavior.cs | 26:9     | Survived | Block removal mutation       | block `{` -> `{}`                                                                                                                                     | equivalent: with no validators the general path also returns next()        |
| backend/src/WattWise.Application/DependencyInjection.cs          | 11:9     | Survived | Statement mutation           | `services.AddValidatorsFromAssembly(typeof(ApplicationAssembly).Assembly, includeInternalTypes: true);` -> (removed)                                  | unreachable: Application has no validator yet                              |
| backend/src/WattWise.Application/DependencyInjection.cs          | 11:104   | Survived | Boolean mutation             | same line, `includeInternalTypes: true` -> `false`                                                                                                    | unreachable: Application has no validator yet                              |
| backend/src/WattWise.Application/Results/Error.cs                | 18:47    | Survived | String mutation              | `return new Error("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation, fieldErrors);` -> `""` (the message argument) | string: the Api never shows this description; pin only as characterization |
| backend/src/WattWise.Application/Results/Result.cs               | 27:47    | Survived | String mutation              | `: throw new InvalidOperationException("A failed result has no value.");` -> `""`                                                                     | string: exception message; pin only as characterization                    |
| backend/src/WattWise.Application/Results/Result.cs               | 31:48    | Survived | String mutation              | `?? throw new InvalidOperationException("A successful result has no error.");` -> `""`                                                                | string: exception message; pin only as characterization                    |

Observations: no Application test exercises the About handler's version fallback; the Application unit tests never check that validators are registered (DependencyInjection) or the exception messages in Result.
