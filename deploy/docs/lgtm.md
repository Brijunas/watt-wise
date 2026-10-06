# Grafana LGTM (Development)

The Development compose stack runs [`grafana/otel-lgtm`](https://github.com/grafana/docker-otel-lgtm). It is a single container that bundles:

- an OpenTelemetry Collector;
- Tempo (traces), Prometheus (metrics) and Loki (logs);
- Grafana, with those three already set up as data sources.

The backend hosts send it their traces, metrics and logs over OTLP. What the hosts emit is described in [backend/docs/observability.md](../../backend/docs/observability.md).

## Settings

| 1Password item (`Watt Wise Development`) | Fields                  | Used for                                                                                           |
| ---------------------------------------- | ----------------------- | -------------------------------------------------------------------------------------------------- |
| `grafana`                                | `server`, `port`, `url` | Address and port the Grafana UI is published on (container port 3000); `url` is for people         |
| `otlp`                                   | `server`, `port`, `url` | Address and port the OTLP gRPC receiver is published on (container port 4317); the apps read `url` |

- **The compose stack** gets `server` and `port` through `compose.settings.tpl`, rendered into `deploy/.env` (see [postgres.md](postgres.md#passwords)).
- **The apps** get `url` through their `.env.development` as `Observability__OtlpEndpoint`.
- **If you change an item,** render `deploy/.env` again and recreate the container.
- **Ports:** only gRPC is published. The container's OTLP HTTP (4318) and Prometheus (9090) ports stay internal.

## Access

Grafana runs with anonymous Admin access (`GF_AUTH_ANONYMOUS_ENABLED`, which is also the image's default), like pgAdmin's desktop mode. The image also has a built-in `admin`/`admin` login. Neither is a risk while the ports are bound to a loopback address. A remote Development machine is reached through an SSH tunnel, as with PostgreSQL. No password goes through compose or 1Password.

## Data

- **Storage:** everything the container stores lives in the `lgtm-data` volume (`/data`), so traces, metrics and logs survive a restart. `docker compose … down -v` deletes them along with the database.
- **Health:** the image has its own healthcheck, so `up --wait` waits for it.
- **Memory:** the bundle is the heaviest part of the stack. Expect it to use well over 1 GB of RAM.

## Finding a request

1. Take the trace id. It is the `@tr` field of a JSON log line, or the second segment of a ProblemDetails `traceId` (`00-<trace id>-<span id>-<flags>`).
2. In Grafana, open **Explore** and pick **Tempo**. Search for the trace id to see the request span and its Npgsql child spans.
3. Pick **Loki** to read the logs, for example `{service_name="wattwise-api"}`. From a trace, "Logs for this span" jumps to the matching log lines.
4. Metrics such as `http_server_request_duration_seconds` are under **Prometheus**.
