# Backend test refactor: plan

The batches that rewrite the backend tests to [testing.md](../testing.md), following [decisions.md](decisions.md) and measured against [baseline.md](baseline.md). Each batch runs in a fresh session, is committed on its own, and ends with the numbers in the checklist. A fresh session starts at the first unchecked batch.

## Progress

| Batch | Scope                                         | Done | Tests (before → after) | Run time (MTP)                                                                                                                 | Mutation score (baseline → after)    |
| ----- | --------------------------------------------- | ---- | ---------------------- | ------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------ |
| 0     | Support code, Infrastructure Stryker baseline | [x]  | 157 → 157              | 19.3 s → 34.2 s (machine busier: unchanged Domain 0.41 → 1.33 s)                                                               | Infrastructure: 37.42 % (taken here) |
| 1     | Domain.Tests                                  | [x]  | 14 → 27                | 0.41 s → 0.98 s (on battery, powersave: unchanged Application 0.50 → 0.69 s)                                                   | Domain 69.23 % → 88.46 %             |
| 2     | Application.Tests                             | [x]  | 23 → 26                | 0.50 s → 0.55 s (on battery, balanced: unchanged Domain 0.98 → 0.45 s)                                                         | Application 65.62 % → 78.12 %        |
| 3     | Infrastructure.Tests (new, unit)              | [x]  | 0 → 59                 | — → 0.48 s (on battery, balanced: unchanged Application 0.55 → 0.38 s)                                                         | Infrastructure 37.42 % → 51.94 %     |
| 4     | Infrastructure.IntegrationTests: Accounts     | [x]  | 71 → 76                | 6.71 s → 12.28 s (cloud machine, 4 vCPU: same project before this batch 13.17 s; unchanged Infrastructure.Tests 1.52 → 1.46 s) | Infrastructure 51.94 % → 52.26 %     |
| 5     | Infrastructure.IntegrationTests: platform     | [ ]  | →                      | →                                                                                                                              | Infrastructure ≥ baseline            |
| 6     | Api.Tests (new, unit)                         | [ ]  | 0 →                    | →                                                                                                                              | Api: taken here                      |
| 7     | Api.IntegrationTests                          | [ ]  | 41 →                   | 6.08 s →                                                                                                                       | Api ≥ batch 6                        |
| 8     | Jobs.IntegrationTests                         | [ ]  | 7 →                    | 5.55 s →                                                                                                                       | n/a                                  |
| 9     | Final: testing.md, agents, thresholds         | [ ]  | 157 →                  | 19.3 s →                                                                                                                       | all projects                         |

Infrastructure.IntegrationTests is split in two (batches 4 and 5) to keep each session-sized; each half deletes only its own old files.

## Every batch

1. Read this plan, decisions.md and the batch.
2. `backend-test-writer` returns its plan table (with `Replaces` and `Why not pinned elsewhere`, and `dropped: <reason>` for every old test no row replaces). Check it against the batch below, then show it to the user; characterization rows (`characterization: <file:line>`) need the user's explicit approval.
3. After approval the writer writes the tests and runs them, the whole project and the mutation run.
4. `backend-test-reviewer` reviews; blocking findings go back to the writer, then review again.
5. Delete the old tests once the done criteria hold.
6. Fill in this batch's row, commit naming the batch (`Backend tests batch N: …`), then stop and report.

Done criteria:

- The backend checks pass with 0 warnings: `dotnet build backend/WattWise.slnx`, `dotnet format backend/WattWise.slnx --verify-no-changes`, `dotnet test --solution backend/WattWise.slnx`.
- Mutation score ≥ the target on the same scope (full project, never `--since:main`), from the repo root, one source project per run.
- The reviewer reports no weak tests, no unexplained redundancy and no coverage lost by deletion.
- Test count and run time compared with the baseline in the checklist.

Rules from decisions.md that every batch applies: one logical assert per test (D15.2), a sad path for every reachable branch (D14), characterization marking (D8), the lowest layer that reaches a behavior, and higher layers testing only what they add.

## Batch 0: support code and Infrastructure baseline

- **First**, before any test changes: full Stryker run of Infrastructure with its integration tests (Docker), recorded in baseline.md with its survivors and a copy under `backend/StrykerOutput/Baseline/Infrastructure/` (D17):
  `dotnet stryker -f backend/stryker-config.json -O backend/StrykerOutput/Infrastructure -p WattWise.Infrastructure.csproj -tp backend/tests/WattWise.Infrastructure.IntegrationTests/WattWise.Infrastructure.IntegrationTests.csproj`
  If a run takes over an hour or can't start its container per mutant run, stop and ask.
- `AccountsTestSupport`: the shared `UniqueViolation` (`23505`), a count helper that reads in a new scope, and `CreateAccountIdentityAsync` that writes in its own scope and returns the ids (removes the same-context reads; replaces the copies in AccountModelTests, EmailRuleTests, RolesAndPermissionsTests).
- `ProblemDetailsAssertions`: `traceId` is the request's W3C trace id, `title` is the status's default.
- Additive only: old tests keep compiling and passing; no test is added or deleted.

## Batch 1: Domain.Tests

Old: `Accounts/AccountTests.cs`, `PermissionTests.cs`, `RoleTests.cs` rewritten; `DomainAssemblyTests.cs` dropped (fails only on a rename, D15.8).

| Behavior                                                                           | Source                       | Bug it catches                       |
| ---------------------------------------------------------------------------------- | ---------------------------- | ------------------------------------ |
| B1 `Account.Create`: v7 id, language, time, no roles                               | domain-model.md, accounts.md | random ids, swapped fields           |
| B2 `AddRole` idempotent; `Guid.Empty` throws                                       | `Account.cs:41,44`           | duplicate links; dangling empty role |
| B3 `Role.Create` blank / null name throws; `Grant` idempotent; `Guid.Empty` throws | `Role.cs:28,33,36`           | nameless roles; duplicate grants     |
| B4 `Permission.Create` blank / null name and null description throw                | `Permission.cs:30-31`        | nameless permissions                 |
| `DomainException` keeps its code, message and inner exception                      | `DomainException.cs`         | codes lost before error mapping      |

## Batch 2: Application.Tests

Old: `GetAboutQueryHandlerTests.cs`, `Pipeline/MediatorPipelineTests.cs`, `Pipeline/RequestContractTests.cs`, `Results/ErrorTests.cs` rewritten; `ResultTests.cs` kept; `ApplicationAssemblyTests.cs` dropped.

- About: the name, and the version as characterization: the SDK's informational version, starting `1.0.0`, never `0.0.0` (D13). The fallback branch is unreachable without a production change (D14).
- Pipeline: a valid request reaches the handler; an invalid one returns `Validation.Failed` with the exact messages per property and never reaches the handler; acronym and dotted property names; a request without validators keeps its handler's error; success logs Debug, failure logs Information with its code, an exception propagates unlogged by the behavior. Source: error-handling.md; messages from the test-only validators.
- Request contract: every Application request returns a `Result` (non-empty guard in Arrange); the helper's own test gets a negative case, or is dropped if one can't be written without breaking the generated mediator.
- Errors: each factory sets code, type and field errors (a Theory where they differ only in data). Accepted survivors stay listed in baseline.md (D13).

## Batch 3: Infrastructure.Tests (new unit project)

- Create the project like `WattWise.Domain.Tests` (MTP, `xunit.v3.mtp-v2`), add it to `WattWise.slnx`, add `InternalsVisibleTo` for it to `src/WattWise.Infrastructure/WattWise.Infrastructure.csproj` (D16, the one production change), add its row to testing.md's "Test projects" table. The Infrastructure Stryker run now takes both test projects (testing.md gets all four runs in batch 9):
  `dotnet stryker -f backend/stryker-config.json -O backend/StrykerOutput/Infrastructure -p WattWise.Infrastructure.csproj -tp backend/tests/WattWise.Infrastructure.Tests/WattWise.Infrastructure.Tests.csproj -tp backend/tests/WattWise.Infrastructure.IntegrationTests/WattWise.Infrastructure.IntegrationTests.csproj`
- Tests: B5 email normalization (trim, NFKC with full-width and ligature input, lowercase) through `TextNormalization` and `EmailLookupNormalizer`; B8 password rules as one rejecting Theory (exactly one error code each) and one accepting Theory (14 and 256); B9 NFKC counting (characterization); D9 code units was left out by the user (no test this batch); B10 the hasher verifies NFD against an NFC hash; the database and observability settings validators; `ParentlessDatabaseSpanFilter` (moved from the integration project, whose old test file is deleted in this batch).

## Batch 4: Infrastructure.IntegrationTests, Accounts

Old: everything under `Accounts/` rewritten. Only what needs PostgreSQL or the real ASP.NET Core Identity wiring stays here.

- B6 `DuplicateEmail` through `UserManager` and the unique index when it is bypassed (SQLSTATE and exact constraint name, D15.7); B7 one identity per account; B10 wiring: a password set through `UserManager` verifies in NFD from a new scope.
- B12 lockout: 5 failures lock for exactly 15 minutes, read back in a new scope; first check whether `UserManager` reads `TimeProvider` from DI and, if so, use a hand-written `TimeProvider` fake (D15.6); otherwise keep a wall-clock window and say so.
- B14–B22 email links: stored hash only; replace and the concurrent pair; single use as a Theory over both kinds; concurrent use; lifetimes as a Theory per kind with the instant itself (characterization, D8); bound to identity and kind; canonical form as a Theory of altered tokens (characterization); a rejected password keeps the link; survives a new provider; unsupported purpose throws.
- B23 one account deletion: identity, role links and email links gone, role kept, each pre-checked. B24 role and permission deletion (characterization), B25 unique names (Theory), B26 two contexts adding the same role at once: one wins, the other gets 23505. B27 `lt` / `en` stored, other text fails to load (characterization). B28 `CreatedAt` equals the value truncated to microseconds.

## Batch 5: Infrastructure.IntegrationTests, platform

Old: `RoleAccessTests.cs`, `DatabaseMigratorTests.cs` rewritten, `HangfireSchemaTests.cs` merged into an ownership Theory, `InitialMigrationTests.cs` kept, `DatabaseRoundTripTests.cs` and `InfrastructureAssemblyTests.cs` dropped (`ParentlessDatabaseSpanFilterTests.cs` moved and deleted in batch 3).

- Role matrix from deploy/docs/postgres.md with literals only (no `DatabaseSchemas` constant); `reader` reads `migrations` per the SQL (D11); one ownership Theory for `__ef_migrations_history` and the Hangfire tables.
- Migrator: steps run EF Core first; a rerun leaves the migration history unchanged; one span per step.

## Batch 6: Api.Tests (new unit project)

- Create it like batch 3 (`InternalsVisibleTo` in `src/WattWise.Api/WattWise.Api.csproj`, D16), add its testing.md row and the Api Stryker command; take the Api baseline at the end (D18):
  `dotnet stryker -f backend/stryker-config.json -O backend/StrykerOutput/Api -p WattWise.Api.csproj -tp backend/tests/WattWise.Api.Tests/WattWise.Api.Tests.csproj`
- Tests: `ErrorTypeMapping`; `ResultHttpExtensions` (status, `code`, camel-cased keys keeping indexers); `ProblemDetailsCustomization` codes from reason phrases, `Unexpected` for 500, `Status599`; `HealthResponseWriter` (moved); `CorsSettingsValidator` as a Theory over every rejected form (path, no scheme, `ftp`, query, fragment, user info, trailing slash, blank).

## Batch 7: Api.IntegrationTests

Old: all rewritten except `HealthResponseWriterTests.cs` (moved in batch 6), `ObservabilityValidationTests.cs` and `OpenApiProductionTests.cs` (kept); `ApiAssemblyTests.cs` dropped. Each test checks only what the host adds.

- One test per error category through the host: status and `code` via `ProblemDetailsAssertions`; a 500 hides its message; binding, 405 and unknown-route problems.
- About: route, 200 and JSON shape; the host's behavior order.
- CORS: allowed and other origins as Theories, plus the methods, headers, 10-minute max age and no credentials from architecture.md; one bad origin stops the host, naming `Cors:AllowedOrigins`.
- Hosts stop on missing or invalid `Database` settings (development.md "Troubleshooting").
- Health: 200 healthy, 503 unhealthy with JSON. Observability: one request line and one server span correlated; an exception logs one Error and a Warning request line. OTLP: a valid endpoint makes the trace exporter connect (observability.md "OTLP endpoint"); the writer proposes how to tell traces from the other exporters.
- OpenAPI: valid document, title, version, `/api/v1/about` listed; Scalar served outside Production.

## Batch 8: Jobs.IntegrationTests

Old: `NoOpJobTests.cs` rewritten (no tautology; the cron value needs characterization approval; stale comment fixed), `BackgroundSpanTests.cs` kept, `JobsAssemblyTests.cs` and `JobsRoleTests.cs` dropped. New: the Jobs host stops on missing `Database` settings.

## Batch 9: final

- testing.md gets D15's changes in this wording (reviewed in this batch's diff): layers table and rules of thumb (unit projects for code needing no database or host); "Writing tests that catch bugs" gains one logical assert, Theories that never branch, sad paths for every reachable branch, characterization marking, no rename-only tests; the PostgreSQL list says to assert SQLSTATE and constraint name until a use case maps the violation; "Mutation testing" lists all four runs, the rewrite rule and the targets; a "Faking time" line for ASP.NET Core Identity's `TimeProvider`; a "Reference tests" section naming 1–2 classes per project.
- Both agents link to testing.md's sections instead of copying the assertion rules and the PostgreSQL list (D15.10); `backend-test-reviewer` back to `model: sonnet` (D20).
- `break` in `backend/stryker-config.json` rises to the lowest final score rounded down to 5 (D19).
- backend/CLAUDE.md "Testing rules" and docs/development.md list the new projects and Stryker commands.
- The final comparison (baseline vs result per project: test count, run time, mutation score) goes into this batch's commit message and the report to the user.
- **Cleanup, last:** delete `backend/docs/test-refactor/` with all its files, the git-ignored `backend/StrykerOutput/Baseline/`, and every reference to them: the writer agent's "for the current refactor, `backend/docs/test-refactor/inventory.md`" becomes "the baseline you were given"; `grep -rn 'test-refactor' .` must find nothing.
