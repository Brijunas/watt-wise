# Images

The Docker images: the .NET hosts `WattWise.Api`, `WattWise.Jobs` and `WattWise.Cli`, and the two web apps `frontend` and `admin` ([below](#frontend-and-admin-images)). The decisions behind them are "Deployment" in [technical.md](../../docs/technical.md) and "Images" in [security.md](../../docs/security.md); the build commands are in [development.md](../../docs/development.md#images).

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

`.dockerignore` at the repo root is an allowlist: it excludes everything, lets back in only the build settings files and `backend/src/`, and excludes build output, `.env*`, `appsettings.*.local.json`, `*.local` and `launchSettings.json` again. So no settings file reaches a stage even though the `.env.development` files sit inside the project folders, and `backend/tests` stays out. For the web images it also lets in the JS workspace (the root `package.json`, `pnpm-lock.yaml`, `pnpm-workspace.yaml`, `tsconfig.base.json`, the two Vitest base files, `frontend/`, `admin/`, `packages/`) and `deploy/nginx/`, and excludes `node_modules`, `dist`, `coverage`, `reports`, `.stryker-tmp`, `.eslintcache` and `*.local.*` files again, so the `.env.op.development` and `.env.example` files stay out.

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

## Frontend and admin images

One `Dockerfile` per app (`frontend/Dockerfile`, `admin/Dockerfile`), built from the repo root: `docker build -f frontend/Dockerfile [--target dev] -t wattwise-frontend .`. The two differ only in the app's name, its `config.js.template` and its settings. One image built once runs in every environment; the container's environment says which API it talks to.

| Stage     | What it is                                                                                                                                                                                                                                                                                                                                                          |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `deps`    | Node, pnpm (from npm: Node 26 ships no corepack), `HUSKY=0`, and `pnpm fetch` as `node` into `/pnpm/store` from `pnpm-lock.yaml` and `pnpm-workspace.yaml` alone, so it stays cached until a dependency changes.                                                                                                                                                    |
| `dev`     | `deps` plus the entry point `pnpm --filter <app> dev`, as `node`. It copies no source: Compose (S5B.5) bind-mounts the tree at `/src`.                                                                                                                                                                                                                              |
| `build`   | `deps` plus the root files, `<app>/` and `packages/`, then `pnpm install --frozen-lockfile --offline --ignore-scripts --filter <app>...` and `pnpm --filter <app> build` (the `tsc && vite build` of CI's `js` job). `--ignore-scripts` skips the root's `preinstall` (`npx` needs the network) and `prepare` (husky). `index.html` is moved aside as the template. |
| `runtime` | The last stage: `nginxinc/nginx-unprivileged`, `USER 101`, port 8080. See below.                                                                                                                                                                                                                                                                                    |

**Base images**: `node:26.13.1-trixie-slim` (mise's Node LTS) and `nginxinc/nginx-unprivileged:1.31.7-trixie` (mainline, which nginx recommends), exact tags without digests, refreshed by `update-deps`. pnpm is pinned with `npm install -g pnpm@12.18.0`, the version mise has; `update-deps` moves it with mise's.

**The `dev` target needs from S5B.5**: a `node_modules` that works under the bind mount (a volume, or `pnpm install --offline` from the image's store), and a non-loopback listen host, because `serverAddress(APP_URL)` binds `localhost` inside the container.

### nginx rules

`deploy/nginx/default.conf` is copied read-only into the image and reads no environment. Each location sets its own `add_header` (nginx drops the outer headers when an inner level sets one; S6.2's security headers must repeat this).

| Request                                         | Answer                                                                                                           |
| ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `/assets/…` (Vite's hashed output)              | The file with `Cache-Control: public, max-age=31536000, immutable`; a missing file is a 404, never `index.html`. |
| `/config.js`                                    | The rendered `/run/wattwise/config.js`, `no-cache`, JS content type; not rendered is a 404, never `index.html`.  |
| `/index.html`                                   | The rendered `/run/wattwise/index.html`, `no-cache`.                                                             |
| `/manifest.webmanifest`                         | `no-cache`, `application/manifest+json`.                                                                         |
| Any other file (`sw.js`, `workbox-*.js`, icons) | The file, `no-cache`: it revalidates by ETag, so a release reaches users through the update prompt.              |
| Any path that is no file (`/`, `/settings`, …)  | The rendered `index.html` with 200 and `no-cache`, for the client router.                                        |

### Runtime settings

- **Render.** The stock entry point's envsubst script renders the templates in `/etc/nginx/templates` (`index.html.template`, the app's `config.js.template`) into `NGINX_ENVSUBST_OUTPUT_DIR=/run/wattwise`, the one directory the runtime user can write. `NGINX_ENVSUBST_FILTER` is anchored to the app's names (`API_BASE_URL`, `APP_URL`, and `FRONTEND_URL` for the admin), so nothing else in the files is substituted. `/config.js` is `window.__WATTWISE_CONFIG__ = {...};` exactly as the Vite plugin serves it ([architecture.md](../../frontend/docs/architecture.md#runtime-config)). Read-only root file systems need `/run/wattwise` as a tmpfs.
- **Guard.** `deploy/nginx/05-check-settings.sh` runs before the envsubst script (`sort -V` order in `/docker-entrypoint.d/`; the entry point runs under `set -e`, so a non-zero exit stops the container before nginx starts). For each name in `WATTWISE_SETTINGS` it fails with a message when the value is unset, empty, multi-line or not an origin: `https://host[:port]`, or `http://` only for `localhost`, `127.0.0.1` or `[::1]`; no path (not even a trailing `/`, since `${APP_URL}/…` is built from it), query, fragment or credentials, port at most 65535. This applies the same shape rule as `checkOrigin`, as a shape check rather than URL parsing (it accepts some hosts `new URL` rejects, such as `https://999.1.1.1`); the browser's `readRuntimeConfig` stays the full check of `API_BASE_URL` and `FRONTEND_URL`. The guard's own job is to keep `"`, `<`, `\` and `$` out of the rendered JS string and HTML attribute.
- **Files.** `/etc/nginx` is root's (the base image gives it to uid 101), so the runtime user can't change the configuration; the base image's ipv6 script skips the file it can no longer write, which is why `default.conf` listens on `[::]:8080` itself. The base image's stock pages (`index.html`, `50x.html`) are removed, so `/50x.html` falls back to the app's `index.html` like any other path.
- **Health check.** The nginx image ships `curl`: `curl -fsS --max-time 2 http://localhost:8080/index.html` with the backend images' timings.

### Running an app image against Development

The 1Password `url` of each app is its origin, so `op run` gives the container its settings, and the Api runs on the host as usual (stop the app's dev server first: the port is the same):

```bash
op run --env-file frontend/.env.op.development -- sh -c 'p=${APP_URL##*:}; exec docker run -d --rm --name wattwise-frontend -p "127.0.0.1:$p:8080" -e API_BASE_URL -e APP_URL wattwise-frontend'
```

The admin adds `-e FRONTEND_URL` and its own env file. This assumes each `APP_URL` has an explicit port and a loopback host, as the Development items have.

## What CI builds and asserts

The `images` job of the pull request workflow ([README](../../.github/workflows/README.md)) builds the `dev` target, the `build` stage and the runtime image of each host and app (five matrix entries, `images (<image>)`), without pushing. It then fails when the `dev` or runtime image runs as root or as no user. For the .NET images it also fails when the runtime user can write `/app` or the host's DLL, Api or Jobs has no health check, a runtime image holds a .NET SDK, or when the runtime image or the `build` stage holds a `.env*`, `appsettings.*.local.json` or `launchSettings.json` file. For the web images it fails on a `.env*` file anywhere in the runtime image or `build` stage and on a `*.local*` file in their sources, and `.github/scripts/check-web-image.sh <image> frontend|admin` (runnable locally) starts containers of the runtime image and fails unless:

- the image's user is 101, a running container's `id -u` is 101, the image has a health check and the container turns `healthy`, and the runtime user can't write `default.conf`, the templates, `nginx.conf`, the guard or the html root;
- `/`, `/index.html` and deep links (`/settings`, `/a/b/c`) give 200 with the rendered `index.html` and `no-cache`; `og:url`, `og:image` and `twitter:image` hold the container's `APP_URL`;
- a missing `/assets/…` is a 404, a real hashed asset has exactly `public, max-age=31536000, immutable`, `sw.js` and `manifest.webmanifest` are `no-cache`;
- two containers of one image with different `API_BASE_URL` values each serve their own exact `/config.js` (the admin's with its `frontendUrl`), `no-cache`;
- a container with a missing, empty or invalid setting (`http://example.test`, a path, a trailing `/`, credentials, a query) stops with a non-zero exit and the guard's message;
- started with the entry point bypassed (nothing rendered), `/config.js` is a 404, not `index.html`.

The script picks its own free loopback ports (below the ephemeral range) because `docker port` reports a closed port on rootless Docker.
