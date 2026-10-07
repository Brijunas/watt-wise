# Logging and observability

How the backend hosts log, trace and measure. Serilog writes structured JSON to the console. OpenTelemetry records traces and metrics. When an OTLP endpoint is configured, all three (logs, traces, metrics) are also exported to it. The layers and hosts are described in [architecture.md](architecture.md); how failures are logged is in [error-handling.md](error-handling.md#logging).

## Shared setup

Every host calls `builder.AddObservability("<service name>")` (`WattWise.Infrastructure/Observability/`) first thing after creating its builder:

| Host            | Service name    | Extra                                                          |
| --------------- | --------------- | -------------------------------------------------------------- |
| `WattWise.Api`  | `wattwise-api`  | Request logging (`UseApiRequestLogging`, `Api/Observability/`) |
| `WattWise.Cli`  | `wattwise-cli`  | A root span per command ([Cli](#cli))                          |
| `WattWise.Jobs` | `wattwise-jobs` | Request logging (`UseJobsRequestLogging`, a copy of the Api's) |

`AddObservability` sets up the following.

**Serilog**

- It replaces the default logging providers, so every `ILogger<T>` writes through Serilog.
- It reads minimum levels from the `Serilog` configuration section.
- It writes one JSON object per line to stdout (`RenderedCompactJsonFormatter`).
- `ILogEventSink`s registered in DI get every event too (`ReadFrom.Services`); the tests use this.

**OpenTelemetry**

- The resource carries `service.name`, `service.version` (the entry assembly's informational version) and `deployment.environment.name` (the lower-cased .NET environment).
- Traces come from the `WattWise.*`, `Microsoft.AspNetCore` and `System.Net.Http` activity sources, plus Npgsql. .NET 11 emits the ASP.NET Core and HttpClient spans natively, so no instrumentation packages are needed for them.
- Metrics come from the `WattWise.*`, `System.Runtime`, `System.Net.Http` and ASP.NET Core meters (hosting, Kestrel, routing, diagnostics), plus Npgsql.

**OTLP export**

- It is enabled only when `Observability:OtlpEndpoint` is set; see [OTLP endpoint](#otlp-endpoint).
- Traces and metrics go through the OpenTelemetry exporter, logs through `Serilog.Sinks.OpenTelemetry`. OpenTelemetry's own log exporter is not used, since Serilog already owns the logs.

`AddSerilog` runs with `preserveStaticLogger: true`. The static `Log.Logger` is never set, and each host owns its logger and disposes it with the host. Without this, the integration tests' parallel `WebApplicationFactory` hosts would share one static logger, and the first host to stop would close it for all the others. As a consequence, nothing may log through the static `Log` class, and Serilog's request logging is given the DI logger explicitly.

There is no Serilog "bootstrap logger" for startup. It also depends on the static logger. Options validation runs after the container is built, so its failures still go through Serilog; anything earlier (such as malformed `appsettings.json`) ends as an unhandled exception on stderr.

## Log format

One JSON line per event, written by the compact formatter:

| Field        | What                                                                           |
| ------------ | ------------------------------------------------------------------------------ |
| `@t`         | Timestamp, UTC, ISO 8601                                                       |
| `@m`         | Rendered message                                                               |
| `@i`         | Message template id (hash), for grouping events of one kind                    |
| `@l`         | Level; left out for Information                                                |
| `@x`         | Exception with stack trace, when there is one                                  |
| `@tr`, `@sp` | Trace id and span id of the current activity, see [Correlation](#correlation)  |
| others       | The template's properties (`RequestPath`, `StatusCode`, …) and `SourceContext` |

Development reads the same JSON as production. To browse it more comfortably, use Grafana's Loki view ([deploy/docs/lgtm.md](../../deploy/docs/lgtm.md)).

## Levels

Each host's `appsettings.json` holds a `Serilog` section with `MinimumLevel.Default` set to Information. The `MinimumLevel.Override` entries hold back framework noise:

| Category                                   | Api      | Jobs    | Cli         |
| ------------------------------------------ | -------- | ------- | ----------- |
| `Microsoft.AspNetCore`                     | Warning  | Warning | not used    |
| `Microsoft.EntityFrameworkCore`            | Warning  | Warning | Warning     |
| `Microsoft.EntityFrameworkCore.Migrations` | Warning  | Warning | Information |
| `Npgsql`                                   | Warning  | Warning | Warning     |
| `System.Net.Http.HttpClient`               | Warning  | Warning | not used    |
| `Hangfire`                                 | not used | Warning | not used    |

The Cli keeps migration messages at Information, so a deployment log shows which migrations ran. Jobs holds Hangfire's own server and worker chatter at Warning; a job's own log lines still show at Information. `Microsoft.Hosting.Lifetime` stays at Information for the start and stop lines.

## One log line per request

An HTTP request that succeeds writes exactly one Information event, the request line:

```
HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed} ms
```

- **Where it comes from:** `UseApiRequestLogging` wraps Serilog's request logging. It is the first middleware in `Program.cs`, so it sees the final status after the exception handler and the status-code pages have run.
- **Its level is set by `RequestLogLevel`:**
  - Error, when an exception escaped the pipeline (which shouldn't happen behind the exception handler);
  - Warning, for any 5xx status;
  - Information otherwise.
  - A 5xx is Warning rather than Serilog's default Error because the Api's exception handler already logs the exception once at Error, with its stack trace. So in the Api one unexpected failure means one Error event plus one Warning request line.
  - Jobs copies the same rules (`UseJobsRequestLogging`) but has no exception handler: an exception escaping the dashboard is logged at Error by the request line and once more by the developer exception page or Kestrel.
- **Use cases:** `LoggingBehavior` logs a successful use case at Debug, so it doesn't add a second line at the default level. A failed use case (a `Result` error) does log at Information, with its error code ([error-handling.md](error-handling.md#logging)).
- **Other framework logs:** the overrides above keep ASP.NET Core's own request start/finish lines and EF Core's command logs out.

## Correlation

- **One trace per request.** The ASP.NET Core server span is the root. Database commands become Npgsql child spans of the same trace.
- **No parentless database spans.** SQL run outside any request, job or command (Hangfire's workers and queue listener poll all the time) would make one single-span trace per command. `ParentlessDatabaseSpanFilter` (`Infrastructure/Observability/`), added to every host's tracer, marks Npgsql spans without a parent as not recorded, so the exporters skip them. They are still created, so the ids on log events don't change. A database span with a parent is kept.
- **Log events carry the ids.** Serilog takes the trace and span ids from the current `Activity`, so every event logged during a request carries the trace id as `@tr`. The request line carries the server span's id as `@sp`.
- **The id clients see.** The ProblemDetails `traceId` ([error-handling.md](error-handling.md#the-contract)) is the W3C `traceparent` of the server span: `00-<trace id>-<span id>-<flags>`. Its second segment is the `@tr` to search for in the logs, and the trace id to open in Tempo; its third segment is the request line's `@sp`.

## OTLP endpoint

- **Key:** `Observability:OtlpEndpoint`, the gRPC endpoint of an OTLP receiver, for example `http://127.0.0.1:4317`.
- **Validation:** `ObservabilitySettingsValidator` runs with `ValidateOnStart`. The value must be an absolute http or https URI with no path, query, fragment or user info; otherwise the host stops at start.
- **Empty means off.** No exporter is added, and logs go only to the console. Spans are still recorded, so the log ids work either way. The integration tests rely on this, and so does any environment without a collector.
- **When the decision is made.** When the logger and the tracer and meter providers are built, from the bound `ObservabilitySettings` options. So every configuration source counts, including test overrides that `WebApplicationFactory` applies only at `Build()`. Don't read `builder.Configuration` during registration for such decisions: those overrides aren't there yet.
- **Where the value comes from.** It is a URL, so it comes from 1Password like every connection setting. `appsettings.json` lists the key empty, and `.env.development` maps `Observability__OtlpEndpoint` to the `otlp` item's `url`. In Development the receiver is the Grafana LGTM container ([deploy/docs/lgtm.md](../../deploy/docs/lgtm.md)). Testing, Staging and Production pick theirs in E5.

## Cli

The Cli builds its host (`CliHost.Build`) but never starts it, since System.CommandLine runs the command itself. Because of that:

- **Telemetry is started by hand.** `CliHost.StartTelemetry` resolves the tracer and meter providers, which is what OpenTelemetry's hosted service would do on start.
- **Each command runs in one root span** from the `WattWise.Cli` activity source (`CliTelemetry.Source`), e.g. `migrate`, so all its SQL commands land in one trace. A failure sets the span status to Error. Inside `migrate`, each schema step gets a child span (`schema-step <name>`, from the `WattWise.Infrastructure` source, `InfrastructureTelemetry.Source`).
- **Disposing the host flushes everything** (`using IHost`): pending spans, a last metrics export and the logger. If the collector is unreachable, exit can wait up to the exporter timeout (10 s).
