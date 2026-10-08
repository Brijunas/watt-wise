# Jobs host and Hangfire

How `WattWise.Jobs` runs background work. Where Jobs sits among the hosts is in [architecture.md](architecture.md#hosts); its logs and traces in [observability.md](observability.md).

## Who uses Hangfire

- **Only the Jobs host.** It runs the Hangfire server, registers the recurring jobs, executes them and serves the dashboard.
- **The Api never touches Hangfire.** It doesn't enqueue jobs, and its `api` role has no access to the `hangfire` schema ([postgres.md](../../deploy/docs/postgres.md)).
- **The Cli only installs the storage** (see [Schema](#schema)).

## Host setup

`Program.cs` wires, in order:

1. `AddObservability("wattwise-jobs")`, `AddInfrastructure()`, `AddApplication()`;
2. `AddHangfireStorage()` (`Storage/`), the storage registration. It lives in Jobs, not Infrastructure, because Hangfire.AspNetCore brings in ASP.NET Core and only Jobs uses Hangfire;
3. `AddHangfireServer` with `WorkerCount` 5;
4. the job classes, registered in DI;
5. after `Build`: `UseJobsRequestLogging()` first, then the dashboard, then `RecurringJobs.Register`.

## Storage

`AddHangfireStorage()` configures Hangfire with PostgreSQL storage (`Hangfire.PostgreSql`):

| Option                     | Value      | Why                                                                                                      |
| -------------------------- | ---------- | -------------------------------------------------------------------------------------------------------- |
| `SchemaName`               | `hangfire` | The dedicated schema for Hangfire's storage (`DatabaseSchemas.Hangfire`)                                 |
| `PrepareSchemaIfNecessary` | `false`    | The `jobs` role has no DDL rights; `migrate` installs the tables                                         |
| `EnableLongPolling`        | `true`     | Workers wait on PostgreSQL `LISTEN/NOTIFY`, so an enqueued job starts at once, not after a poll interval |

- **Connection.** The connection string comes from the bound `DatabaseSettings` options, the same `Database:*` keys as every host, read when the container is built. So test overrides applied at `Build()` count.
- **Serialization.** Data compatibility level 1.8.0, simple assembly-name type serialization and Hangfire's recommended serializer settings. Renaming or moving a job class breaks jobs already stored under the old name, so finish or delete them first.

## Schema

- **Who installs it.** `WattWise.Cli migrate` does, through `DatabaseMigrator`: its `HangfireStorageStep` runs after the EF Core step and calls Hangfire.PostgreSql's `PostgreSqlObjectsInstaller` ([architecture.md](architecture.md#persistence)).
- **Who owns it.** The Cli's connection runs as `owner` (`-c role=owner`), so the tables belong to `owner`, and the default privileges give the `jobs` role read and write on their rows.
- **Upgrades.** The installer is idempotent and applies only the scripts a database is missing. After a Hangfire.PostgreSql upgrade, `migrate` brings the tables up to date before Jobs starts.
- **Tests.** The test fixture migrates through the same `DatabaseMigrator`, so every test database already has the tables ([testing.md](testing.md#shared-postgresql-fixture)).

## Job classes

Jobs live in `WattWise.Jobs/Recurring/`. A job class is thin: it takes its dependencies by constructor, and its `ExecuteAsync(CancellationToken)` only sends an Application use case through the mediator and returns. The logic, validation and logging of the work belong to the use case ([architecture.md](architecture.md#request-handling)).

`NoOpJob` is the template. It only logs one line, since there is no use case yet; real jobs replace that line with a `mediator.Send`.

To add a recurring job:

1. Add a class next to `NoOpJob`, with a `RecurringJobId` constant and `ExecuteAsync(CancellationToken)`.
2. Register it in DI in `Program.cs`.
3. Add it to `RecurringJobs.Register` with its id and cron expression. Hangfire evaluates cron in UTC unless `RecurringJobOptions.TimeZone` is set, while tariff boundaries are Europe/Vilnius local time ([technical.md](../../docs/technical.md)); a job tied to local time sets that time zone, or it shifts by an hour at each DST change. Hangfire passes its own cancellation token in place of `CancellationToken.None`, so the job stops when the server shuts down.
4. Add a test ([Testing a job](#testing-a-job)).

`RecurringJobs.Register` runs at every start and uses `AddOrUpdate`, so a changed schedule takes effect on the next deployment. A job removed from the code must also be removed from storage (`RemoveIfExists`), or Hangfire keeps scheduling it and fails to find the class.

## Dashboard

- **Where.** `/hangfire` on the Jobs URL (the `jobs` 1Password item's `url` in Development). The `http` launch profile opens it.
- **Access.** Until E4 it keeps Hangfire's default filter, which allows only local requests. E4 restricts it to admins.
- **Logging.** Dashboard requests get the same one-line request log as the Api ([observability.md](observability.md#one-log-line-per-request)). An open dashboard polls its stats every few seconds, so expect a line per poll.

## Sizing

- **Workers.** `WorkerCount` is fixed at 5 instead of Hangfire's default (five per CPU core, capped at 20).
- **Connections.** Hangfire.PostgreSql and EF Core each keep their own Npgsql pool, on Npgsql's defaults; nothing about pooling is configured ([postgres.md](../../deploy/docs/postgres.md#hardening)). With long polling, each idle worker holds one `LISTEN` connection.

## Testing a job

`WattWise.Jobs.IntegrationTests` starts the real host through `TestHostFactory<Program>` with a clean database's `jobs` settings ([testing.md](testing.md)). The real Hangfire server runs against that database. Hangfire keeps process-wide static state, so every test class that starts a Jobs host joins the `JobsHostCollection`, which runs them one host at a time.

1. Trigger the job: `IRecurringJobManager.Trigger(NoOpJob.RecurringJobId)`.
2. Wait with `Poll.UntilAsync` until `JobStorage.GetMonitoringApi().SucceededJobs(...)` lists a job of that class.

TestServer leaves the remote address empty, which Hangfire's local-requests-only filter rejects with 401; a test checks exactly that, so a change can't silently open the dashboard. The test that expects 200 registers `LoopbackClient`, a startup filter that sets the address to loopback.

## Not yet

- **Traces.** Hangfire creates no OpenTelemetry spans for job runs. A job's database calls therefore have no parent and are dropped by `ParentlessDatabaseSpanFilter` along with the polling ([observability.md](observability.md#correlation)). A server filter that starts an activity per job comes with the first real job, so its calls form one trace.
