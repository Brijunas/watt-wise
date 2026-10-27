# Backend testing

What each backend layer tests and where those tests live. The layers themselves are described in [architecture.md](architecture.md).

## What to test in each layer

Both inner layers get unit tests, each of a different kind. The outer layers are covered by integration and functional tests instead.

| Layer          | What to test                                                                                                       | How                                                                                                                          |
| -------------- | ------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| Domain         | Entities, value objects and domain services: the cost engine, time-of-use zones, DST handling, pricing components. | Unit tests. Pure: no mocks, no I/O. Most of the backend's tests live here.                                                   |
| Application    | Use cases and handlers, validators, pipeline behaviors.                                                            | Unit tests. The ports (repositories, external data sources) are replaced with fakes, so the tests check orchestration logic. |
| Infrastructure | EF Core mappings, migrations, repositories, external data adapters.                                                | No unit tests. Integration tests against a real PostgreSQL (Testcontainers), with external HTTP sources stubbed.             |
| Api, Jobs      | Endpoints, auth, error mapping; each Hangfire job end to end.                                                      | Few or no unit tests. Functional tests through `WebApplicationFactory` and against a real PostgreSQL.                        |

Rules of thumb:

- If a scenario can be tested with a unit test, test it with a unit test. Use an integration test only for what a unit test can't reach.
- Test conditional logic and error paths (one happy path and at least one sad path), not coverage numbers.
- Endpoints should stay thin and delegate to handlers, so their behaviour is checked by functional tests, not unit tests.

## Test projects

| Project                                    | Kind        | Covers                                                                                                                                                      |
| ------------------------------------------ | ----------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `WattWise.Domain.Tests`                    | Unit        | Domain, including golden-case tests for the calculation engine.                                                                                             |
| `WattWise.Application.Tests`               | Unit        | Application handlers, validators and pipeline behaviors.                                                                                                    |
| `WattWise.Infrastructure.IntegrationTests` | Integration | Persistence, migrations and external adapters against PostgreSQL.                                                                                           |
| `WattWise.Api.IntegrationTests`            | Functional  | Endpoints and auth through `WebApplicationFactory`, ProblemDetails mapping.                                                                                 |
| `WattWise.Jobs.IntegrationTests`           | Functional  | Each Hangfire job end to end against PostgreSQL with stubbed external sources: expected rows written, draft plans produced, retries and idempotent re-runs. |
| `WattWise.Testing`                         | Library     | Not a test project: the shared PostgreSQL fixture, `TestHostFactory` and `Poll`, referenced by the three integration projects.                              |

## Shared PostgreSQL fixture

The integration and functional projects run against a real PostgreSQL started by Testcontainers. The fixture lives in `tests/WattWise.Testing` and sets the database up the way a deployment does, so tests see the same roles, schemas and privileges as production ([deploy/docs/postgres.md](../../deploy/docs/postgres.md)).

- **One container per test assembly.** `PostgresContainerFixture` is an xUnit assembly fixture, declared in each project's `AssemblyFixtures.cs`. It starts the same image as the compose files on a free loopback port, with superuser `admin`.
- **Bootstrap.** It runs `deploy/postgres/bootstrap.sql` with psql inside the container (the file is linked into the build output), then gives `cli`, `api` and `jobs` random passwords.
- **Migrations.** It applies them as `cli` with `-c role=owner`, through `DatabaseMigrator`, the same code `WattWise.Cli migrate` runs ([architecture.md](architecture.md#persistence)). The migrated `wattwise` database then serves only as a template.
- **A clean database per test class.** `DatabaseFixture` is a class fixture: it clones the template (`CREATE DATABASE … TEMPLATE wattwise`), re-applies the database-level grants the bootstrap makes, and drops the clone when the class is done. Test classes run in parallel, each on its own clone.
- **Access per role.** `RoleAccessTests` (Infrastructure.IntegrationTests) checks each role against the matrix in [postgres.md](../../deploy/docs/postgres.md#schemas-and-privileges): a probe table in every schema, data access where the role should have it, `permission denied` where it shouldn't, and no `CREATE TABLE` anywhere. A change to the grants changes that test in the same commit.
- **No 1Password here.** Host, port and passwords are generated per run and live only as long as the container, so the rule that connection settings come from 1Password doesn't apply. Each role's settings reach the code under test as the usual `Database:*` keys (`TestDatabase.ConfigurationFor(role)`).

Writing an integration test:

- Take `IClassFixture<DatabaseFixture>` and connect as the role the code under test uses: `api` for Api code, `jobs` for jobs, `cli` only for migration checks. `DatabaseFixture.BuildServices(role)` gives a service provider with `AddInfrastructure()` wired to the clone.
- Identity comes with `AddInfrastructure()`, so `BuildServices(role)` already has `UserManager`. To test expiry, swap the clock with `BuildServices(role, services => services.Replace(ServiceDescriptor.Singleton<IClock>(fakeClock)))`.
- Api tests start the host through `TestHostFactory<Program>` (`WattWise.Testing`, a `WebApplicationFactory` for any web host) with the clone's `api` settings. It runs as Development unless given an `environment`, e.g. `"Production"` to check what is hidden there.
- Jobs tests start the Jobs host through the same `TestHostFactory<Program>`, with the clone's `jobs` settings. How to test a job, and why those classes run one host at a time, is in [jobs.md](jobs.md#testing-a-job).
- Tests need Docker running and usable without `sudo` ([setup.md](../../docs/setup.md)). The first run pulls the image.

## Testing the pipeline and error mapping

- **Pipeline (Application.Tests).** The test project references `Mediator.SourceGenerator` and calls `AddMediator` with the Application assembly, its own assembly and the same behaviors as the hosts, so tests run the real generated mediator. Test-only requests and handlers live in the test project; no sample use case ships in production code.
- **Error mapping (Api.IntegrationTests).** `TestHostFactory` takes an optional `Action<IServiceCollection>`, applied through `ConfigureTestServices`. `ErrorMappingTests` shares one host per class through `ErrorsApiFixture`, which uses that hook to register a test-only `IEndpointModule` (endpoints that return each kind of `Error` or throw), a `LogEventCollector` (a Serilog sink, picked up through `ReadFrom.Services`) and a `SpanCollector` (an OpenTelemetry processor). The tests assert the ProblemDetails body: status, `type`, `code`, `traceId` and, for validation, `errors`. A new error category adds a case there ([error-handling.md](error-handling.md#how-to-add-)).
- **Logs and traces (Api.IntegrationTests).** `ObservabilityTests` use the same fixture to check that a request writes one log line and one trace, correlated by trace id ([observability.md](observability.md)). Activity listeners are process-wide, so the `SpanCollector` also sees spans of other test hosts running in parallel: always filter collected spans and log events by trace id, never assert on the whole list. The server span ends after the response is sent, so wait for it with `Poll.UntilAsync`. `ObservabilityValidationTests` check that a malformed OTLP endpoint stops the host. `OtlpExportTests` check that a valid one turns export on: the host still serves requests, and a flush connects to a local TCP listener that stands in for the collector.

## Writing tests that catch bugs

A test earns its place by failing for a realistic bug. The `backend-test-writer` and `backend-test-reviewer` Claude Code agents apply these rules.

Assertions:

- Expected values are literals from the spec or worked out by hand (with the arithmetic in a comment when it isn't obvious), never computed by production code or a copy of its logic.
- `NotNull`, `NotEmpty`, a count or "doesn't throw" is never the only check. Assert exact values, exact error codes (`Error.Code`, the ProblemDetails `code`) and exact stored state.
- Never fake the class under test. Fake only boundaries: `IClock` with NodaTime's `FakeClock`, external HTTP sources, SMTP and the ports in Application tests (hand-written fakes, no mocking library). Integration tests never fake `AppDbContext`, repositories or Identity.
- One behavior per test, arranged, acted and asserted in separate blocks. Names are sentences (`A_reset_link_sets_the_new_password_and_a_second_use_fails`). Plain xUnit `Assert`; every async call gets `TestContext.Current.CancellationToken`.

PostgreSQL integration tests (fixtures: [Shared PostgreSQL fixture](#shared-postgresql-fixture)):

- Never use EF Core InMemory, SQLite, `EnsureCreated` or a hand-built container or `DbContext`; they differ from PostgreSQL in constraints, transactions, collation and types.
- Tests in a class share one clone, so each makes unique data (`AccountsTestSupport.UniqueEmail()`, fresh `Guid`s) and asserts on its own rows, never on whole-table counts.
- Run each step in its own scope, as requests do, and read stored state back through a new scope or provider, never the context that wrote it. Api tests assert both the HTTP response and the rows.
- Cover the PostgreSQL behavior the change touches:
  - unique and foreign-key violations map to the right error and status (409, 404, 422), not 500;
  - optimistic concurrency: two scopes load and both save (`SaveChangesBarrier`, `CommandBarrier`), exactly one wins;
  - a failure mid-use-case persists nothing;
  - `timestamptz` and NodaTime: `Instant` round trips at microsecond precision (PostgreSQL's limit), Europe/Vilnius local time on 23- and 25-hour DST days, zone boundaries at the exact minute;
  - case-sensitive text and collation in lookups and filters, Identity's normalized fields included;
  - `numeric` precision and rounding for money and energy amounts; `jsonb` mapping and queries, if used;
  - stable pagination (`ORDER BY` with a unique tie-breaker);
  - null, empty and missing values in requests and columns.

## Mutation testing

Stryker.NET (`dotnet-stryker`, a local tool: `dotnet tool restore`) changes the production code in small ways and checks that a test fails. A surviving mutant is behavior no test pins down. Settings are in `backend/stryker-config.json`.

- **What is mutated, which tests run.** `WattWise.Domain` with `WattWise.Domain.Tests`, and `WattWise.Application` with `WattWise.Application.Tests`: two runs, because Stryker's solution mode would also run the integration tests (Docker). Migrations and generated files are excluded.
- **Command.** Run each from the repo root; reports go to `backend/StrykerOutput/<Project>/reports/` (git-ignored; open `mutation-report.html`):

  ```bash
  dotnet stryker -f backend/stryker-config.json -O backend/StrykerOutput/Domain -p WattWise.Domain.csproj -tp backend/tests/WattWise.Domain.Tests/WattWise.Domain.Tests.csproj
  dotnet stryker -f backend/stryker-config.json -O backend/StrykerOutput/Application -p WattWise.Application.csproj -tp backend/tests/WattWise.Application.Tests/WattWise.Application.Tests.csproj
  ```

- **Only changed code.** Add `--since:main` (files changed against `main`) or `--mutate "**/Pricing/*.cs"` (a glob, repeatable) to a run.
- **Thresholds.** `high`, `low` and `break` go under `stryker-config` in `stryker-config.json`; `high` 80 and `low` 60 are Stryker's defaults and only colour the report. `break` 60 makes a run exit with code 1 below 60 %, a floor under today's scores so the score can't drop; raise it as survivors are killed.
- Treat each survivor as a missing or weak test: add the assertion that kills it, or note why the mutant is equivalent.

## Property-based tests

CsCheck generates inputs and shrinks a failing case. Use it where a rule must hold for all inputs, not for single examples:

- Domain calculations (totals, rounding, splitting an amount across periods never loses or invents a cent).
- DST and zone logic: any `Instant` maps to exactly one zone and local time, and back.
- Parsing and mapping round trips: `parse(format(x)) == x`.

Example-based tests of the known edge cases stay; a property adds the cases nobody listed. Properties use `Gen` and `.Sample(...)` inside a normal `[Fact]`. A failure prints a seed (e.g. `Seed = 0ABC...`); rerun that case by passing it as `seed: "0ABC..."` to `Sample`, then keep the seed as a regression test.

## External HTTP stubs

External HTTP sources are stubbed with WireMock.Net, an in-process HTTP server, so tests run the real `HttpClient` pipeline. It is added with E9's ESO grid plan ingestion, the first external source ([epics.md](../../docs/epics.md#e9-grid-plan-domain)).

## Framework

xUnit v3 running on Microsoft.Testing.Platform (MTP). The root `global.json` sets MTP as the test runner, so `dotnet test` uses it. Each test project is an executable (`OutputType Exe`) referencing `xunit.v3.mtp-v2`.
