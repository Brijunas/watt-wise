# Running on the host

The commands to run the apps and the hosts directly on the machine, as an option next to the containerized stack ([development.md](development.md#running-the-system)). Stop the same service in the stack first: the ports are the same, and the database is too. Every command runs from the repo root. Outside an interactive shell with mise activated (an agent's or tool's shell), write `mise exec -- op run …` for the `op run` commands.

## Running the apps

Each app's dev server and preview get their settings from 1Password through the app's own `.env.op.development`: the address to listen on (the `frontend` or `admin` item's `url`, the origin the Api's CORS allows) and the Api's URL, which they serve to the app as `/config.js` ([frontend/docs/architecture.md](../frontend/docs/architecture.md#runtime-config)). The app shows data only while the Api runs ([Running the hosts](#running-the-hosts)). Swap `frontend` for `admin` for the admin app; its `.env.op.development` also gives the frontend's address (`FRONTEND_URL`, the `frontend` item's `url`) for the forgot-password link. Vite's log shows the address as `<concealed by 1Password>`, like the hosts; the URLs are in the items.

| Command                                                                            | What it does                                                                                    |
| ---------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `op run --env-file frontend/.env.op.development -- pnpm --filter frontend dev`     | Starts the dev server with hot reload on the `frontend` item's `url`. No service worker in dev. |
| `pnpm --filter frontend build`                                                     | Type-checks and builds `frontend/dist/`. Needs no settings: the API address is read at runtime. |
| `op run --env-file frontend/.env.op.development -- pnpm --filter frontend preview` | Serves the last build on the same address, service worker included, to try the PWA.             |

## Running the hosts

Each host gets its settings and its database role from 1Password through its own `.env.development` file. Without `op run` it starts as Production and stops on the missing `Database` settings. The listen address in the log shows as `<concealed by 1Password>` (see "Troubleshooting" in [setup.md](setup.md#troubleshooting)); the URLs are in the 1Password items.

| Command                                                                                                          | What it does                                                                                                                                             |
| ---------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `op run --env-file backend/src/WattWise.Api/.env.development -- dotnet run --project backend/src/WattWise.Api`   | Starts the Api on the `api` item's `url`: `/health`, `/scalar` and `/openapi/v1.json` ([architecture.md](../backend/docs/architecture.md#http-surface)). |
| `op run --env-file backend/src/WattWise.Jobs/.env.development -- dotnet run --project backend/src/WattWise.Jobs` | Starts the Jobs host on the `jobs` item's `url`, with the Hangfire dashboard at `/hangfire` ([jobs.md](../backend/docs/jobs.md)).                        |
