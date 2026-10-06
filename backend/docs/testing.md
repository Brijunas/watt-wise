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
| `WattWise.Testing`                         | Library     | Not a test project: the shared PostgreSQL fixture the three integration projects reference.                                                                 |

## Shared PostgreSQL fixture

The integration and functional projects run against a real PostgreSQL started by Testcontainers. The fixture lives in `tests/WattWise.Testing` and sets the database up the way a deployment does, so tests see the same roles, schemas and privileges as production ([deploy/docs/postgres.md](../../deploy/docs/postgres.md)).

- **One container per test assembly.** `PostgresContainerFixture` is an xUnit assembly fixture, declared in each project's `AssemblyFixtures.cs`. It starts the same image as the compose files on a free loopback port, with superuser `admin`.
- **Bootstrap.** It runs `deploy/postgres/bootstrap.sql` with psql inside the container (the file is linked into the build output), then gives `cli`, `api` and `hangfire` random passwords.
- **Migrations.** It applies them as `cli` with `-c role=owner`, through `DatabaseMigrator`, the same code `WattWise.Cli migrate` runs ([architecture.md](architecture.md#persistence)). The migrated `wattwise` database then serves only as a template.
- **A clean database per test class.** `DatabaseFixture` is a class fixture: it clones the template (`CREATE DATABASE … TEMPLATE wattwise`), re-applies the database-level grants the bootstrap makes, and drops the clone when the class is done. Test classes run in parallel, each on its own clone.
- **No 1Password here.** Host, port and passwords are generated per run and live only as long as the container, so the rule that connection settings come from 1Password doesn't apply. Each role's settings reach the code under test as the usual `Database:*` keys (`TestDatabase.ConfigurationFor(role)`).

Writing an integration test:

- Take `IClassFixture<DatabaseFixture>` and connect as the role the code under test uses: `api` for Api code, `hangfire` for jobs, `cli` only for migration checks. `DatabaseFixture.BuildServices(role)` gives a service provider with `AddInfrastructure()` wired to the clone.
- Api tests start the host through `ApiFactory` (a `WebApplicationFactory<Program>`) with the clone's `api` settings.
- Tests need Docker running and usable without `sudo` ([setup.md](../../docs/setup.md)). The first run pulls the image.

## Testing the pipeline and error mapping

- **Pipeline (Application.Tests).** The test project references `Mediator.SourceGenerator` and calls `AddMediator` with the Application assembly, its own assembly and the same behaviors as the hosts, so tests run the real generated mediator. Test-only requests and handlers live in the test project; no sample use case ships in production code.
- **Error mapping (Api.IntegrationTests).** `ApiFactory` takes an optional `Action<IServiceCollection>`, applied through `ConfigureTestServices`. `ErrorMappingTests` uses it to register a test-only `IEndpointModule` whose endpoints return each kind of `Error` or throw, and asserts the ProblemDetails body: status, `type`, `code`, `traceId` and, for validation, `errors`. A new error category adds a case there ([error-handling.md](error-handling.md#how-to-add-)).

## Framework

xUnit v3 running on Microsoft.Testing.Platform (MTP). The root `global.json` sets MTP as the test runner, so `dotnet test` uses it. Each test project is an executable (`OutputType Exe`) referencing `xunit.v3.mtp-v2`.
