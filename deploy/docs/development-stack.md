# Development stack

The whole system in containers on one machine: `deploy/docker-compose.development.yml` runs PostgreSQL, Grafana LGTM and Mailpit ([postgres.md](postgres.md), [lgtm.md](lgtm.md), [mailpit.md](mailpit.md)) and the four apps, built from the `dev` targets of their Dockerfiles ([images.md](images.md)) with the repo's tree bind-mounted. The commands are in [development.md](../../docs/development.md#running-the-system); this page is how it works. Running the hosts and the dev servers on the host stays an option ([development.md](../../docs/development.md#running-on-the-host)).

## Services

| Service    | Runs                                                                                           | Published (host side)                                    | Waits for                                                         |
| ---------- | ---------------------------------------------------------------------------------------------- | -------------------------------------------------------- | ----------------------------------------------------------------- |
| `db-init`  | one-shot: `deploy/postgres/db-init.sh` on the `postgres` image ([First start](#first-start))   | nothing                                                  | `postgres` healthy                                                |
| `cli`      | one-shot: the Cli's runtime image runs `migrate`, then `seed smoke-accounts`                   | nothing                                                  | `db-init` completed                                               |
| `api`      | `dotnet watch` on `WattWise.Api`, listening on `8080` in the container                         | `API_BIND_ADDRESS:API_PORT`, from the `api` item's `url` | `postgres` healthy, `db-init`, `cli` completed, `lgtm`            |
| `jobs`     | `dotnet watch` on `WattWise.Jobs`                                                              | `JOBS_…`, from the `jobs` item                           | `postgres` healthy, `db-init`, `cli` completed, `mailpit`, `lgtm` |
| `frontend` | Vite dev server (`--host 0.0.0.0 --port 8080`, which replaces the address `APP_URL` would set) | `FRONTEND_…`, from the `frontend` item                   | nothing                                                           |
| `admin`    | the same for the admin app                                                                     | `ADMIN_…`, from the `admin` item                         | nothing                                                           |

Inside the network the topology is fixed in the compose file: the hosts reach PostgreSQL at `postgres:5432`, Mailpit at `mailpit:1025` (Jobs only) and LGTM at `lgtm:4317`. The published ports, the browser URLs and the CORS origins are the 1Password values, so a browser, the Playwright MCP and a host-run process see the same addresses as before. The Api and Jobs hosts run as `Development`, so `appsettings.Development.json` applies except for the Smtp host and port, which the compose file overrides (its `127.0.0.1` would be the container itself).

Every app service and `cli` is built on every `up` (`pull_policy: build`), from cache when nothing changed, so a worktree with other `.csproj` files or another lockfile never runs another worktree's stale image. The `cli` runtime image rebuilds its publish stage after any backend source change, so the next `up` takes about a minute longer; that is the price of running the same image the deployed stacks run.

## Settings: two files

| File                             | Rendered or resolved by | Holds                                                                                                                                                                                                                                                                                                                          |
| -------------------------------- | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `deploy/.env`                    | `./setup` (`op inject`) | Non-secret settings, read by Compose on its own: bind addresses and ports of the existing services, user names, the four apps' `*_URL` and the derived `*_BIND_ADDRESS`, `*_PORT`.                                                                                                                                             |
| `deploy/development/compose.env` | `op run`                | Every container's secret or vault-held settings under per-service names: `POSTGRES_PASSWORD`, `CLI_DATABASE_*`, `CLI_SMOKE_*`, `API_DATABASE_*`, `API_JWT_SIGNING_KEY`, `JOBS_DATABASE_*`, `JOBS_SMTP_*`, `BACKUP_DATABASE_PASSWORD`, `READER_DATABASE_PASSWORD`, and the browser URLs `API_URL`, `FRONTEND_URL`, `ADMIN_URL`. |

The compose file hands each service only its own names through `environment:`, never an `env_file:`, so `docker inspect` of `api` holds no `jobs` password and no password of the Cli's role (`cli` gets its own). `db-init` gets every password as a Compose secret instead ([First start](#first-start)). What `docker inspect` shows and to whom is accepted in [security.md](../../docs/security.md#deployment). The browser URLs are in both files: `deploy/.env` needs them for the port derivation, `compose.env` for the containers, and the value from `op run` wins with the same field behind both.

App settings are written `${VAR:-}`, never `:?`, so `ps`, `logs`, `exec`, `stop` and `down` work without `op run`. Only `up` and `watch` need it. A host started without it stops at start on its missing `Database` and `Jwt` settings. The browser URLs (and so the CORS origins and the web apps' `/config.js`) still resolve, because `deploy/.env` holds them too. A container keeps the environment it was created with, so a restart after a reboot needs no `op run`; changing a value in 1Password needs another `up` through `op run`, which recreates the container.

PostgreSQL gets `POSTGRES_PASSWORD` on every `up` ([postgres.md](postgres.md#passwords)).

## Published ports

`./setup` derives each app's published address and port from its 1Password `url`, because Compose can publish a port but can't split a URL. It appends `<APP>_BIND_ADDRESS` and `<APP>_PORT` (`API`, `JOBS`, `FRONTEND`, `ADMIN`) to `deploy/.env` after `op inject`, before comparing with the old file, so an unchanged vault still reports `unchanged`.

- Only a bare origin is accepted: `http://localhost:<port>`, `http://127.0.0.1:<port>` or `http://[::1]:<port>`, with a lowercase host and nothing after the port, not even a `/`. The same value is the apps' `API_BASE_URL`, `APP_URL` and `FRONTEND_URL` and the Api's CORS origins, which all take an origin only ([images.md](images.md#runtime-settings)). A path, a trailing slash, another scheme or host, no port, a port outside 1 to 65535 or with a leading zero stops `./setup`, naming the key and the item. So a wrong item can't publish an app on a public address.
- `localhost` becomes `127.0.0.1` and `[::1]` becomes `::1`, since Docker binds an address.
- `./setup --derive-url <APP> <url>` runs the same function and prints the two lines, or the message. `.github/scripts/check-setup.sh` tests it with a case table, in CI too ([setup.md](../../docs/setup.md#checking-setup)).

The compose file keeps `:?` on these keys (and on the other bind addresses and ports): without a rendered `deploy/.env`, every compose command says to run `./setup`.

## The tree and what the containers write

The repo root is bind-mounted read-write at `/src`, with no user mapping. A container's user can read the checkout and can't write it, **on rootless Docker** (vidar): it maps the container's uids (1654 for the .NET images, `node` for the web images) to subuids that own none of your files, so `docker compose exec api touch /src/x` fails, and so does `exec frontend touch /src/x`. This is an assumption about the Docker mode, not something the compose file enforces: on rootful Docker the web containers' `node` user is uid 1000 and can write a checkout owned by uid 1000. S5B.8 decides the mode and the mount for tethys. Every write the stack needs goes to a volume or the container's layer:

- **.NET.** Every project's `bin` and `obj` go to `/artifacts` ([images.md](images.md#build-output)), so nothing is written under `/src`. `/artifacts` has no volume: it lives in each host container's own layer, seeded from the image's restored copy. A `restart` keeps it (incremental builds); a recreate (worktree switch, rebuild, changed settings) starts cold. A named volume would carry one worktree's build output into another's: MSBuild skips a compile when the outputs are newer than the sources, so a switch back would run the other worktree's code. The Api's `dev` target doesn't generate the OpenAPI document, so `dotnet watch` writes nothing to `backend/openapi/` (`pnpm generate:api` owns it).
- **Web apps.** Each app has named volumes over every `node_modules` its filter needs: the root, `<app>/` and `packages/{api-client,core,i18n,ui}/`, created in the image owned by `node`. The start installs from the image's store and runs Vite with the compose `command`'s flags ([images.md](images.md#frontend-and-admin-images)), so an image rebuilt after a lockfile change brings the new packages into the existing volume. The store and the volumes are on different filesystems, so the first install copies the packages instead of hard-linking them; that is why the web start period is long. A new workspace package needs its own volume line; without one the install fails loudly, because the container can't write the tree. Vite's caches sit inside those volumes.

## Reload and `watch`

- A C# edit: `dotnet watch` hot-reloads or restarts the host in the same container (`--non-interactive`; `DOTNET_WATCH_RESTART_ON_RUDE_EDIT=true` restarts on an edit it can't apply instead of asking). The SDK image's polling file watcher is on. `api` and `jobs` run with `init: true`: `dotnet watch` as PID 1 ignores SIGTERM, so without it `docker compose stop` waits 10 s and kills it (exit 137); with it, `stop` takes under a second.
- A frontend edit: Vite's HMR through the published port.
- `docker compose watch` (through `op run`, in a terminal of its own) rebuilds one service when its inputs change, and nothing else: `api` and `jobs` on any `*.csproj` under `backend/` and `backend/Directory.Packages.props`, `frontend` and `admin` on `pnpm-lock.yaml` (the workspace has one lockfile, so a change rebuilds both). Edits don't `sync`: they arrive through the bind mount. Compose's watch paths take no globs, so the backend has two rules per host: `backend/src` with `include: ['**/*.csproj']` (a bare `*.csproj` doesn't match the nested projects; the test projects under `backend/tests` and `.cs` files trigger nothing) and the file `backend/Directory.Packages.props`. The root `.dockerignore` allowlist does not filter watch events, which is why the rule watches `backend/src` and not `backend/`.
- The other inputs of the image's restore (`global.json`, `backend/Directory.Build.props`, `backend/nuget.config`, `.globalconfig`) aren't watched: after changing one, run a plain `up`, which rebuilds the image (`pull_policy: build`) and recreates the container.

## One stack per machine

The project is named `wattwise-development`, so a machine has one PostgreSQL, one Mailpit, one LGTM and one set of app containers, built from whichever worktree ran `up` last. `up` from another worktree changes the bind source and the dev image, so Compose recreates the four app containers (they then serve that worktree's code); `postgres`, `lgtm` and `mailpit` keep their containers and volumes, because their configuration is unchanged. The `node_modules` volumes are shared across worktrees: the install at start reconciles them. The hosts' build output is not (see above), so every switch is a cold build. Run `up` again in the first worktree to switch back.

## Health checks

The `dev` targets have none, so the compose file defines them. Timings: every 10 s, timeout 3 s, 3 retries; `start_period` 300 s for `api` and `jobs` (a cold `dotnet watch` build) and 180 s for the web apps (the first install), with `start_interval` 2 s, so `up --wait` returns within seconds of the app answering.

- `api`, `jobs`: `curl -fsS --max-time 2 http://localhost:8080/health` (the SDK image has curl); 503 when the database check fails ([images.md](images.md#ports-and-health-check)).
- `frontend`, `admin`: a `node` one-liner that fetches `http://127.0.0.1:8080/` (the node image has no curl; Vite listens on IPv4 here).
- `postgres`: `pg_isready -h 127.0.0.1`, over TCP. While the image initializes an empty volume its temporary server listens only on its socket, so a socket check could pass before `db-init` can connect.

## First start

`up` on an empty volume does the whole first start ([setup.md](../../docs/setup.md#5-development-database)):

1. `db-init` connects as `admin` and runs [`db-init.sh`](../postgres/db-init.sh), which runs the bootstrap and sets each login role's password ([postgres.md](postgres.md#bootstrap)). Every password is a Compose secret from the environment `op run` sets: a file under `/run/secrets` in the container, so `docker inspect` shows none.
2. `cli` runs `migrate` and then `seed smoke-accounts` (Development only; the deployed stacks run `migrate` alone). Both are idempotent.
3. `api` and `jobs` start only when both exited 0 (`depends_on` with `service_completed_successfully`). When `migrate` fails, `up` fails and neither host starts.

One-shots run again on every `up`: `db-init` finds the passwords set and does nothing, `migrate` applies nothing and the seed puts the smoke accounts back to their known state. `ps`, `logs` and `down` need no `op run`: an unset secret variable fails only `up`.

## The Hangfire dashboard

The containerized Jobs host answers `/hangfire` with 401. The dashboard allows local requests only, and a request through the published port reaches the host from Docker's gateway address, which is not local. That filter is a security control ([jobs.md](../../backend/docs/jobs.md), [security.md](../../docs/security.md)), so it stays. To use the dashboard, stop `jobs` and run the Jobs host on the host ([development.md](../../docs/development.md#running-on-the-host)).

## Troubleshooting

| Problem                                                             | Fix                                                                                                                                                                                                               |
| ------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `up` says to render `deploy/.env`, or a `*_PORT` is missing         | Run `./setup`: it renders `deploy/.env` with the derived keys.                                                                                                                                                    |
| `./setup` stops on a `*_URL` value                                  | The item's `url` must be an `http` loopback origin with a port ([Published ports](#published-ports)); fix the item.                                                                                               |
| A host exits at start on its missing `Database` or `Jwt` setting    | The container was created without `op run`. Run `up` through `op run`.                                                                                                                                            |
| `up` fails, or `api` and `jobs` don't start                         | A one-shot failed: read `docker compose logs db-init cli`. Else `api` is still building: a cold build takes up to 5 minutes.                                                                                      |
| A web app's `pnpm install` fails with a permission or missing error | A new workspace package needs a `node_modules` volume line, or run `up` again after the lockfile changed so the image is rebuilt.                                                                                 |
| Port already in use on `up`                                         | A host-run process holds it. Stop it, or `stop` the container and keep the host-run one.                                                                                                                          |
| Container names show as `<concealed by 1Password>-development-…-1`  | Under `op run`, compose's output masks every injected value, and `wattwise`, `api` and `jobs` are values of the database name and usernames. Cosmetic: `docker compose ps` without `op run` shows the real names. |
| `/hangfire` answers 401                                             | By design in the container ([above](#the-hangfire-dashboard)).                                                                                                                                                    |
| Compose still shows the old code after switching worktree           | Run `up` through `op run` in the worktree you want; it recreates the app containers.                                                                                                                              |
