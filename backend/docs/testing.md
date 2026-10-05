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

The three integration and functional projects share one Testcontainers fixture that starts PostgreSQL 18.6, applies the migrations and gives each test class a clean database.

## Framework

xUnit v3 running on Microsoft.Testing.Platform (MTP). The root `global.json` sets MTP as the test runner, so `dotnet test` uses it. Each test project is an executable (`OutputType Exe`) referencing `xunit.v3.mtp-v2`.
