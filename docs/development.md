# Development

Everyday commands and workflow. For first-time machine setup, see [setup.md](setup.md). Every command runs from the repo root. Commands for the apps (E3) are added here by the stories that introduce them.

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

Run all eight before committing; CI enforces the same from E5. `typecheck` and `test` run in every package under `packages/`. Until the apps exist (E3), `build` has nothing to run and passes trivially. The backend build must end with 0 warnings, and its integration tests need Docker running (Testcontainers).

## Commands

| Command                                  | What it does                                                                                            |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| `mise install`                           | Installs the tool versions from `mise.toml`. Rerun after pulling a change to it.                        |
| `pnpm install`                           | Installs the JS dependencies for the whole workspace. Rerun after a pull that changes `pnpm-lock.yaml`. |
| `pnpm lint`                              | ESLint over the whole repo, using the root `eslint.config.js`. Warnings fail.                           |
| `pnpm format`                            | Prettier rewrites every file it supports.                                                               |
| `pnpm format:check`                      | Prettier reports unformatted files without changing them.                                               |
| `pnpm typecheck`                         | Runs `typecheck` (`tsc`, TypeScript 7) in every workspace package that has one.                         |
| `pnpm test`                              | Runs `test` (`vitest run`) in every workspace package that has one.                                     |
| `pnpm build`                             | Runs `build` in every workspace package that has one.                                                   |
| `pnpm --filter @wattwise/core exec tsc6` | Type-checks one package with TypeScript 6, the version ESLint uses.                                     |

### Running a single test

Use `--filter` with the package name. Extra arguments go straight to Vitest.

| Command                                                           | What it runs                                 |
| ----------------------------------------------------------------- | -------------------------------------------- |
| `pnpm --filter @wattwise/core test`                               | One package's tests                          |
| `pnpm --filter @wattwise/core test src/index.test.ts`             | One test file (path relative to the package) |
| `pnpm --filter @wattwise/core test -t "exports its package name"` | Tests whose name matches                     |
| `pnpm --filter @wattwise/core exec vitest`                        | One package's tests in watch mode            |

## Backend

The rules behind these (analyzers, `.editorconfig`, the test projects) are in [backend/docs/conventions.md](../backend/docs/conventions.md) and [backend/docs/testing.md](../backend/docs/testing.md).

| Command                                                   | What it does                                                                                         |
| --------------------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `dotnet tool restore`                                     | Restores the local tools (`dotnet-ef`) from `.config/dotnet-tools.json`. Rerun after a change to it. |
| `dotnet build backend/WattWise.slnx`                      | Builds the solution. Must end with 0 warnings.                                                       |
| `dotnet format backend/WattWise.slnx`                     | Fixes whitespace, code style and unused usings across the solution.                                  |
| `dotnet format backend/WattWise.slnx --verify-no-changes` | Reports what `dotnet format` would change, without changing it.                                      |
| `dotnet test --solution backend/WattWise.slnx`            | Runs every test project. The integration tests start PostgreSQL in Docker.                           |

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

`op run` conceals every value it injected, so the listen address shows as `<concealed by 1Password>` in the log. The URLs are in the 1Password items.

## Development database

First-time setup (roles, passwords, first start) is "Development database" in [setup.md](setup.md#5-development-database); the roles and the stack's settings are explained in [deploy/docs/postgres.md](../deploy/docs/postgres.md). Compose reads its settings from the rendered `deploy/.env`, so everyday compose commands need no `op run`.

| Command                                                                                                              | What it does                                                                                                                |
| -------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| `op inject -f -i deploy/development/compose.settings.tpl -o deploy/.env`                                             | Renders the stack's settings again after one of them changes in 1Password.                                                  |
| `docker compose -f deploy/docker-compose.development.yml up -d --wait`                                               | Starts PostgreSQL, pgAdmin and Grafana LGTM and waits until they are healthy.                                               |
| `docker compose -f deploy/docker-compose.development.yml down`                                                       | Stops and removes the containers. The data stays in the volumes.                                                            |
| `docker compose -f deploy/docker-compose.development.yml ps`                                                         | Shows the containers and their health.                                                                                      |
| `docker compose -f deploy/docker-compose.development.yml logs -f postgres`                                           | Follows one service's log.                                                                                                  |
| `docker compose -f deploy/docker-compose.development.yml exec postgres sh -c 'psql -U "$POSTGRES_USER" -d wattwise'` | Opens psql as `admin` in the `wattwise` database.                                                                           |
| `docker compose -f deploy/docker-compose.development.yml down -v`                                                    | Also deletes the volumes: the database, pgAdmin's state and Grafana's data. Afterwards redo setup.md step 5 from the start. |

pgAdmin is at the `pgadmin` item's address and port, and asks for the `postgres-admin` password the first time ([postgres.md](../deploy/docs/postgres.md#pgadmin)). Grafana is at the `grafana` item's `url` ([lgtm.md](../deploy/docs/lgtm.md)).

## Migrations

Only `WattWise.Cli migrate` changes the schema, in every environment; never use `dotnet ef database update`. Why, and how the migrate steps work, is under "Persistence" in [architecture.md](../backend/docs/architecture.md#persistence). The `dotnet ef` commands below need `--project backend/src/WattWise.Infrastructure`, shown as `<infra>`.

| Command                                                                                                                   | What it does                                                                                              |
| ------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- migrate` | Applies pending EF Core migrations and installs or upgrades Hangfire's tables. Safe to rerun.             |
| `dotnet ef migrations add <Name> <infra> --output-dir Persistence/Migrations`                                             | Adds a migration for the model changes. Doesn't connect, so no `op run`.                                  |
| `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet ef migrations remove <infra>`                      | Removes the last migration if it isn't applied. Connects to check that.                                   |
| `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet ef migrations list <infra>`                        | Lists the migrations and marks the ones not yet applied `(Pending)`. Without `op run` it can't show that. |
| `dotnet ef migrations has-pending-model-changes <infra>`                                                                  | Fails if the model has changes no migration covers. CI runs it from E5.                                   |
| `dotnet ef migrations script --idempotent <infra> -o <file>.sql`                                                          | Writes the SQL of every migration, for review.                                                            |

## Updating dependencies

The policy (minimum release age, how majors are taken, Renovate later) is in "Dependency updates" in [technical.md](technical.md). Check [known-issues.md](known-issues.md) before touching TypeScript or typescript-eslint.

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
2. **Lint and format** (lint-staged, configured in `lint-staged.config.js`). ESLint `--fix` and Prettier run on the staged files; fixes are added to the commit automatically. Any remaining lint error stops the commit.
   - Staged `.cs` files under `backend/` go through `dotnet format` twice: once to fix what it can (added to the commit like the ESLint fixes), then with `--verify-no-changes`, so any issue it can't fix (an analyzer warning such as CA2013) stops the commit and is printed. Generated migrations are skipped. This adds about 15–20 seconds to a commit that touches C#.

## Troubleshooting

| Problem                                                          | Fix                                                                                                                              |
| ---------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Hook fails only in a GUI git client                              | The GUI doesn't load your shell config, so mise's tools aren't on its `PATH`. Commit from a terminal, or start the GUI from one. |
| ESLint says a file "was not found by the project service"        | The `.ts`/`.tsx` file isn't included by any `tsconfig.json`. Add it to the nearest package's tsconfig `include`.                 |
| Backend tests fail with `DockerUnavailableException`             | Docker isn't running. Start it and run the tests again.                                                                          |
| A host stops with `Database configuration is missing or invalid` | It was started without `op run --env-file …/.env.development`.                                                                   |

For tool, install and hook-setup problems, see "Troubleshooting" in [setup.md](setup.md).
