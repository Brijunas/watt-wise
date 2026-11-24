# Images

The Docker images of the .NET hosts: `WattWise.Api`, `WattWise.Jobs` and `WattWise.Cli`. The frontend and admin images come in S5B.3. The decisions behind them are "Deployment" in [technical.md](../../docs/technical.md) and "Images" in [security.md](../../docs/security.md); the build commands are in [development.md](../../docs/development.md#images).

## Layout

Each host has one `Dockerfile` in its project folder (`backend/src/WattWise.<Host>/Dockerfile`). The build context is always the repo root, so the Dockerfile can reach `global.json` and `backend/Directory.*`.

| Stage     | What it is                                                                                                                                                                                                                                                                                                                         |
| --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `restore` | The SDK image with the build settings files (`global.json`, the `.editorconfig` files, `Directory.Build.props`, `Directory.Packages.props`, `nuget.config`, `.globalconfig`) and only the `.csproj` files of the host's project closure, then `dotnet restore` into `/nuget`. Cached until a `.csproj` or package version changes. |
| `dev`     | `restore` plus an entry point: `dotnet watch … run` for the Api and Jobs, `dotnet run … --` for the Cli. It copies no source: Compose (S5B.5) bind-mounts the tree at `/src`.                                                                                                                                                      |
| `build`   | `restore` plus `backend/src/`, then `dotnet publish -c Release --no-restore` into `/out`. The Api passes `-p:OpenApiGenerateDocuments=false`: the OpenAPI document is generated and committed by `pnpm generate:api`, not in an image.                                                                                             |
| `runtime` | The last stage, so a plain `docker build` gives it: the base runtime image, `/out` copied to `/app` owned by root and read-only (`--chown=root:root --chmod=a=rX`), `USER $APP_UID`, and the entry point `dotnet WattWise.<Host>.dll`.                                                                                             |

`dev` and `build` run as the SDK image's non-root `app` user (uid 1654, `$APP_UID`); `/src`, `/nuget` and `/out` belong to it and nothing is world-writable. S5B.5 may change the user mapping if a bind mount needs it.

## Base images

All from `mcr.microsoft.com/dotnet`, with exact tags and no digest; `update-deps` refreshes them ([development.md](../../docs/development.md#updating-dependencies)).

| Image     | Used by                   | Tag                 |
| --------- | ------------------------- | ------------------- |
| `sdk`     | `restore` (all hosts)     | `11.0.100-resolute` |
| `aspnet`  | `runtime` of Api and Jobs | `11.0.0-resolute`   |
| `runtime` | `runtime` of the Cli      | `11.0.0-resolute`   |

- The SDK tag equals the SDK in `global.json`; the other two are the runtime that SDK ships with.
- `-resolute` is Ubuntu 26.04, which is what Microsoft publishes for .NET 11. There is no Debian `-trixie` variant of .NET 11, so the images use Microsoft's standard variant rather than a different distribution.
- The Cli needs only `Microsoft.NETCore.App` (its `WattWise.Cli.runtimeconfig.json` names no ASP.NET Core framework), so it uses the smaller `runtime` image.
- The runtime images run as the base image's `app` user (`USER $APP_UID`, so `docker image inspect` shows `1654`) and hold no SDK. Api and Jobs add `curl` for the health check. `/app` belongs to root and is not writable by `app`, so a compromised process can't overwrite the DLLs it runs; the hosts write nothing there.

## The build context

`.dockerignore` at the repo root is an allowlist: it excludes everything, lets back in only the build settings files and `backend/src/`, and excludes build output, `.env*`, `appsettings.*.local.json`, `*.local` and `launchSettings.json` again. So no settings file reaches a stage even though the `.env.development` files sit inside the project folders, and `backend/tests` stays out. S5B.3 adds the frontend's paths.

The images hold no settings or secrets: every setting arrives as an environment variable ([technical.md](../../docs/technical.md#cross-cutting)). The committed `appsettings*.json` files, which hold no secret, stay in the images.

## Ports and health check

The Api and Jobs listen on the base image's `ASPNETCORE_HTTP_PORTS=8080`; a set `ASPNETCORE_URLS` overrides it. Both serve `/health` ([architecture.md](../../backend/docs/architecture.md#http-surface), [jobs.md](../../backend/docs/jobs.md)): 200 when healthy, 503 when the database check fails.

Their `HEALTHCHECK` runs every 30 s (timeout 3 s, 3 retries); during the first 30 s it runs every 2 s, so a started container turns `healthy` quickly. It calls `curl -fsS --max-time 2 <url>/health`, where `<url>` is the first entry of `ASPNETCORE_URLS` with a wildcard host (`+`, `*`, `0.0.0.0`, `[::]`, with or without a port) replaced by `localhost` and a trailing slash dropped, or `http://localhost:<first HTTP port>` (8080 by default) when `ASPNETCORE_URLS` is empty. `-f` makes the 503 unhealthy. The first `ASPNETCORE_URLS` entry must be plain `http` with no leading space: an `https` first entry, or only `ASPNETCORE_HTTPS_PORTS`, is not supported by the check. The Cli has no health check.

## The Cli

`docker run <image> migrate` runs `WattWise.Cli migrate`: the entry point is the Cli, so the arguments are its commands (`docker run <image> --help` lists them). The Cli's `dev` target runs `dotnet run`, so its arguments follow the same way.

## Running an image against Development

The Development `.env.development` files hold `op://` references and loopback addresses (`ASPNETCORE_URLS`, the database host). `op run` resolves the references; each variable is passed into the container by name with `-e NAME`, and `--network host` makes the loopback addresses reach the host's PostgreSQL, Mailpit and Grafana:

```bash
op run --env-file backend/src/WattWise.Cli/.env.development -- sh -c 'exec docker run --rm --network host $(sed -n "s/^\([A-Za-z_][A-Za-z0-9_]*\)=.*/-e \1/p" backend/src/WattWise.Cli/.env.development) wattwise-cli migrate'
```

The same works for `wattwise-api` and `wattwise-jobs` with their own file (add `-d --name <name>` to keep them running; `docker inspect -f '{{.State.Health.Status}}' <name>` shows the health). Stop the host-run Api or Jobs first: the ports are the same. Compose with the `dev` targets (S5B.5) replaces this.

## What CI builds and asserts

The `images` job of the pull request workflow ([README](../../.github/workflows/README.md)) builds the `dev` target, the `build` stage and the runtime image of each host, without pushing. It then fails when the `dev` or runtime image runs as root or as no user, the runtime user can write `/app` or the host's DLL, Api or Jobs has no health check, a runtime image holds a .NET SDK, or when the runtime image or the `build` stage holds a `.env*`, `appsettings.*.local.json` or `launchSettings.json` file.
