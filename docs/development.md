# Development

Everyday commands and workflow. For first-time machine setup, see [setup.md](setup.md). Every command runs from the repo root.

## Checks

```bash
pnpm lint
pnpm format:check
pnpm typecheck
pnpm test
pnpm build
dotnet build backend/WattWise.slnx
dotnet format backend/WattWise.slnx --verify-no-changes
dotnet test --solution backend/WattWise.slnx
```

Run all eight before committing; CI enforces the same from E5. `typecheck` and `test` run in both apps and in every package under `packages/`; `build` runs in both apps. The backend build must end with 0 warnings, and its integration tests need Docker running (Testcontainers).

## Workspace (JS)

The rules behind these (lint rules, the shared Vitest and TypeScript configs, where tests go) are in [frontend/docs/conventions.md](../frontend/docs/conventions.md), [frontend/docs/architecture.md](../frontend/docs/architecture.md#shared-packages) and [frontend/docs/testing.md](../frontend/docs/testing.md).

| Command                                                       | What it does                                                                                                                                                                                                                                  |
| ------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `mise install`                                                | Installs the tool versions from `mise.toml`. Rerun after pulling a change to it.                                                                                                                                                              |
| `pnpm install`                                                | Installs the JS dependencies for the whole workspace. Rerun after a pull that changes `pnpm-lock.yaml`.                                                                                                                                       |
| `pnpm lint`                                                   | ESLint over the whole repo, using the root `eslint.config.js`. Warnings fail.                                                                                                                                                                 |
| `pnpm format`                                                 | Prettier rewrites every file it supports.                                                                                                                                                                                                     |
| `pnpm format:check`                                           | Prettier reports unformatted files without changing them.                                                                                                                                                                                     |
| `pnpm typecheck`                                              | Runs `typecheck` (`tsc`, TypeScript 7) in every workspace package that has one.                                                                                                                                                               |
| `pnpm test`                                                   | Runs `test` (`vitest run`) in every workspace package that has one.                                                                                                                                                                           |
| `pnpm build`                                                  | Runs `build` in every workspace package that has one.                                                                                                                                                                                         |
| `pnpm generate:api`                                           | Builds the Api (which writes `backend/openapi/wattwise-api.json`) and regenerates `@wattwise/api-client` from it. Run after changing an endpoint; commit both ([frontend/docs/architecture.md](../frontend/docs/architecture.md#api-client)). |
| `pnpm --filter @wattwise/core exec tsc6`                      | Type-checks one package with TypeScript 6, the version ESLint uses.                                                                                                                                                                           |
| `pnpm --filter <workspace> mutation [--mutate src/<file>.ts]` | StrykerJS mutation testing of one workspace; run it alone. Scoping and options are in [frontend/docs/testing.md](../frontend/docs/testing.md#mutation-testing).                                                                               |

### Running a single test

Use `--filter` with the package name (`frontend`, `admin` or `@wattwise/<name>`). Extra arguments go straight to Vitest.

| Command                                                                            | What it runs                                                                              |
| ---------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `pnpm --filter frontend test`                                                      | One app's or package's tests                                                              |
| `pnpm --filter frontend test src/features/about/about-panel.test.tsx`              | One test file (path relative to the app or package)                                       |
| `pnpm --filter frontend test -t "shows the error text"`                            | Tests whose name matches                                                                  |
| `pnpm --filter @wattwise/core test src/runtime-config/read-runtime-config.test.ts` | The same in a package                                                                     |
| `pnpm --filter frontend exec vitest`                                               | One app's or package's tests in watch mode (in a terminal; without one, Vitest runs once) |

## Running the apps

Each app's dev server and preview get their settings from 1Password through the app's own `.env.op.development`: the address to listen on (the `frontend` or `admin` item's `url`, the origin the Api's CORS allows) and the Api's URL, which they serve to the app as `/config.js` ([frontend/docs/architecture.md](../frontend/docs/architecture.md#runtime-config)). The app shows data only while the Api runs ([Running the hosts](#running-the-hosts)). Swap `frontend` for `admin` for the admin app; its `.env.op.development` also gives the frontend's address (`FRONTEND_URL`, the `frontend` item's `url`) for the forgot-password link. Vite's log shows the address as `<concealed by 1Password>`, like the hosts; the URLs are in the items.

| Command                                                                            | What it does                                                                                    |
| ---------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `op run --env-file frontend/.env.op.development -- pnpm --filter frontend dev`     | Starts the dev server with hot reload on the `frontend` item's `url`. No service worker in dev. |
| `pnpm --filter frontend build`                                                     | Type-checks and builds `frontend/dist/`. Needs no settings: the API address is read at runtime. |
| `op run --env-file frontend/.env.op.development -- pnpm --filter frontend preview` | Serves the last build on the same address, service worker included, to try the PWA.             |
| `pnpm --filter frontend generate:icons`                                            | Regenerates the PWA icons and `favicon.ico` from `public/favicon.svg`; commit the results.      |

## Backend

The rules behind these (analyzers, `.editorconfig`, the test projects) are in [backend/docs/conventions.md](../backend/docs/conventions.md) and [backend/docs/testing.md](../backend/docs/testing.md).

| Command                                                                                                                                                                        | What it does                                                                                                                                                                  |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `dotnet tool restore`                                                                                                                                                          | Restores the local tools (`dotnet-ef`, `dotnet-stryker`) from `.config/dotnet-tools.json`. Rerun after a change to it.                                                        |
| `dotnet build backend/WattWise.slnx`                                                                                                                                           | Builds the solution. Must end with 0 warnings.                                                                                                                                |
| `dotnet format backend/WattWise.slnx`                                                                                                                                          | Fixes whitespace, code style and unused usings across the solution.                                                                                                           |
| `dotnet format backend/WattWise.slnx --verify-no-changes`                                                                                                                      | Reports what `dotnet format` would change, without changing it.                                                                                                               |
| `dotnet test --solution backend/WattWise.slnx`                                                                                                                                 | Runs every test project. The integration tests start PostgreSQL in Docker.                                                                                                    |
| `dotnet stryker -f backend/stryker-config.json -O backend/StrykerOutput/Domain -p WattWise.Domain.csproj -tp backend/tests/WattWise.Domain.Tests/WattWise.Domain.Tests.csproj` | Mutation tests Domain with its unit tests; the Application, Infrastructure and Api commands and the options are in [testing.md](../backend/docs/testing.md#mutation-testing). |

### Running a single test

The tests run on Microsoft.Testing.Platform. Filters match the fully qualified name, so give the full name or start it with `*`. `--filter-class` and `--filter-method` together must both match. A run where no test matches fails with exit code 8.

| Command                                                                                                     | What it runs                    |
| ----------------------------------------------------------------------------------------------------------- | ------------------------------- |
| `dotnet test --project backend/tests/WattWise.Api.IntegrationTests`                                         | One project's tests             |
| `dotnet test --project backend/tests/WattWise.Api.IntegrationTests --filter-class "*.OpenApiDocumentTests"` | One test class                  |
| `dotnet test --project backend/tests/WattWise.Api.IntegrationTests --filter-method "*Scalar*"`              | Test methods whose name matches |

### Running the hosts

Each host gets its settings and its database role from 1Password through its own `.env.development` file. Without `op run` it starts as Production and stops on the missing `Database` settings.

| Command                                                                                                          | What it does                                                                                                                                             |
| ---------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `op run --env-file backend/src/WattWise.Api/.env.development -- dotnet run --project backend/src/WattWise.Api`   | Starts the Api on the `api` item's `url`: `/health`, `/scalar` and `/openapi/v1.json` ([architecture.md](../backend/docs/architecture.md#http-surface)). |
| `op run --env-file backend/src/WattWise.Jobs/.env.development -- dotnet run --project backend/src/WattWise.Jobs` | Starts the Jobs host on the `jobs` item's `url`, with the Hangfire dashboard at `/hangfire` ([jobs.md](../backend/docs/jobs.md)).                        |

The listen address in the log shows as `<concealed by 1Password>` (see "Troubleshooting" in [setup.md](setup.md#troubleshooting)); the URLs are in the 1Password items.

## Development database

First-time setup (roles, passwords, first start) is "Development database" in [setup.md](setup.md#5-development-database); the roles and the stack's settings are explained in [deploy/docs/postgres.md](../deploy/docs/postgres.md). Compose reads its settings from the rendered `deploy/.env`, so everyday compose commands need no `op run`.

| Command                                                                                                              | What it does                                                                                               |
| -------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `op inject -f -i deploy/development/compose.settings.tpl -o deploy/.env`                                             | Renders the stack's settings again after one of them changes in 1Password.                                 |
| `docker compose -f deploy/docker-compose.development.yml up -d --wait --remove-orphans`                              | Starts PostgreSQL, Grafana LGTM and Mailpit and waits until they are healthy.                              |
| `docker compose -f deploy/docker-compose.development.yml down`                                                       | Stops and removes the containers. The data stays in the volumes.                                           |
| `docker compose -f deploy/docker-compose.development.yml ps`                                                         | Shows the containers and their health.                                                                     |
| `docker compose -f deploy/docker-compose.development.yml logs -f postgres`                                           | Follows one service's log.                                                                                 |
| `docker compose -f deploy/docker-compose.development.yml exec postgres sh -c 'psql -U "$POSTGRES_USER" -d wattwise'` | Opens psql as `admin` in the `wattwise` database.                                                          |
| `docker compose -f deploy/docker-compose.development.yml down -v`                                                    | Also deletes the volumes: the database and Grafana's data. Afterwards redo setup.md step 5 from the start. |

Machines that previously ran pgAdmin can remove its leftover volume: run `docker volume ls`, then `docker volume rm <volume-name>` for the volume ending in `pgadmin-data`.

Grafana is at the `grafana` item's `url` ([lgtm.md](../deploy/docs/lgtm.md)). Mailpit's web UI is at the `mailpit` item's `url`, and every email the Development Jobs host sends shows up there ([mailpit.md](../deploy/docs/mailpit.md)). Register through the Api and the email arrives within a few seconds, once the Jobs host is running too ([email-outbox.md](../backend/docs/email-outbox.md)).

## Migrations

Only `WattWise.Cli migrate` changes the schema, in every environment; never use `dotnet ef database update`. Why, and how the migrate steps work, is under "Persistence" in [architecture.md](../backend/docs/architecture.md#persistence). The `dotnet ef` commands below need `--project backend/src/WattWise.Infrastructure`, shown as `<infra>`.

| Command                                                                                                                                                           | What it does                                                                                                                                                            |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- migrate`                                         | Applies pending EF Core migrations and installs or upgrades Hangfire's tables. Safe to rerun.                                                                           |
| `dotnet ef migrations add <Name> <infra> --output-dir Persistence/Migrations`                                                                                     | Adds a migration for the model changes. Doesn't connect, so no `op run`.                                                                                                |
| `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet ef migrations remove <infra>`                                                              | Removes the last migration if it isn't applied. Connects to check that.                                                                                                 |
| `docker compose -f deploy/docker-compose.development.yml exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d wattwise' < deploy/development/reset-app-tables.sql` | Drops the `app` tables and the migration history, after `Initial` was regenerated ([architecture.md](../backend/docs/architecture.md#persistence)). Then run `migrate`. |
| `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet ef migrations list <infra>`                                                                | Lists the migrations and marks the ones not yet applied `(Pending)`. Without `op run` it can't show that.                                                               |
| `dotnet ef migrations has-pending-model-changes <infra>`                                                                                                          | Fails if the model has changes no migration covers. CI runs it from E5.                                                                                                 |
| `dotnet ef migrations script --idempotent <infra> -o <file>.sql`                                                                                                  | Writes the SQL of every migration, for review.                                                                                                                          |

## Accounts for development

The commands connect like `migrate` (the `.env.development` of the Cli, `owner` role; [postgres.md](../deploy/docs/postgres.md)). The account must exist and have a confirmed email. What the admin commands do and print is "Admin role" in [accounts.md](../backend/docs/features/accounts.md#admin-role), and the seed's is "Smoke accounts" ([accounts.md](../backend/docs/features/accounts.md#smoke-accounts)).

| Command                                                                                                                                | What it does                                                                                                                                                            |
| -------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- admin grant <email>`  | Gives the account the `Admin` role, so it can call `/api/v1/admin` once its token is refreshed.                                                                         |
| `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- admin revoke <email>` | Takes the role away and ends all the account's sessions.                                                                                                                |
| `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- seed smoke-accounts`  | Development only: makes the smoke-check user and admin accounts exist with the 1Password credentials; prints `user\|admin: created\|changed\|unchanged`. Safe to rerun. |

## Updating dependencies

The policy (minimum release age, how majors are taken, Renovate later) is in "Dependency updates" in [technical.md](technical.md). Check [known-issues.md](known-issues.md) before touching TypeScript or typescript-eslint.

A full refresh (every source to latest, doc references, all checks) is the `update-deps` Claude skill; its read-only survey also runs on its own: `python3 .claude/skills/update-deps/scripts/outdated.py`.

| Command                          | What it does                                                                  |
| -------------------------------- | ----------------------------------------------------------------------------- |
| `pnpm outdated -r`               | Lists outdated dependencies in every workspace package, including the root.   |
| `pnpm update -r`                 | Updates within the existing ranges, so no majors. Low risk.                   |
| `pnpm update -r -i --latest`     | Lets you pick bumps past the current range (majors) and rewrites the ranges.  |
| `pnpm update -r --latest <name>` | Bumps one package, or a pattern such as `"@eslint/*"`, to its latest version. |

If the newest release is younger than the minimum release age, pnpm picks the newest one that is old enough. After updating, run the [checks](#checks), then commit the `package.json` files and `pnpm-lock.yaml` together in their own commit.

## What happens when you commit

The pre-commit hook (`.husky/pre-commit`) runs two checks on the staged changes. Either one can stop the commit.

1. **Secret scan.** Betterleaks looks for credentials (API keys, tokens, passwords) in the staged changes. If it finds one, the commit is rejected and the finding is printed with the value redacted.
   - Remove the secret from the change. Secrets live in 1Password and reach the app as environment variables; see "Secrets and configuration" in [technical.md](technical.md).
   - Don't bypass the hook with `git commit --no-verify`, and don't add an exception without agreeing on it first.
   - If the hook says `betterleaks not found`, mise isn't active in the shell git runs in, or you haven't run `mise install`.
2. **Lint and format** (lint-staged, configured in `lint-staged.config.js`). ESLint `--fix` and Prettier run on the staged files, wherever they are in the repo (both apps, the packages, the root); fixes are added to the commit automatically. Any remaining lint error stops the commit.
   - Staged `.cs` files under `backend/` go through `dotnet format` twice: once to fix what it can (added to the commit like the ESLint fixes), then with `--verify-no-changes`, so any issue it can't fix (an analyzer warning such as CA2013) stops the commit and is printed. Generated migrations are skipped. This adds about 15–20 seconds to a commit that touches C#.

## Troubleshooting

| Problem                                                                       | Fix                                                                                                                                                                                                                                                   |
| ----------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Hook fails only in a GUI git client                                           | The GUI doesn't load your shell config, so mise's tools aren't on its `PATH`. Commit from a terminal, or start the GUI from one.                                                                                                                      |
| ESLint says a file "was not found by the project service"                     | The `.ts`/`.tsx` file isn't included by any `tsconfig.json`. Add it to the nearest package's tsconfig `include`.                                                                                                                                      |
| ESLint reports stale import errors after `pnpm install` added a dependency    | Delete `.eslintcache` (ESLint's `--cache` kept the old resolution).                                                                                                                                                                                   |
| `docker compose … exec postgres` fails in a fresh worktree (no `deploy/.env`) | Run the reset with `docker exec -i wattwise-development-postgres-1 sh -c 'psql -U "$POSTGRES_USER" -d wattwise' < deploy/development/reset-app-tables.sql`, or add `--env-file` pointing at the main checkout's `deploy/.env` to the compose command. |
| Backend tests fail with `DockerUnavailableException`                          | Docker isn't running. Start it and run the tests again.                                                                                                                                                                                               |
| A host stops with `Database configuration is missing or invalid`              | It was started without `op run --env-file …/.env.development`.                                                                                                                                                                                        |
| An app stops with an error about `/config.js` or `API_BASE_URL`               | Its dev server or preview was started without `op run --env-file <app>/.env.op.development`.                                                                                                                                                          |
| The admin's `/config.js` throws about `FRONTEND_URL`                          | Its dev server or preview was started without `op run --env-file admin/.env.op.development`.                                                                                                                                                          |
| An app's dev server exits with "Port … is already in use"                     | Another process holds the app's port. Stop it; the server doesn't move to another port ([why](../frontend/docs/architecture.md#runtime-config)).                                                                                                      |

For tool, install and hook-setup problems, see "Troubleshooting" in [setup.md](setup.md).
