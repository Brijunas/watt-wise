# Setup

How to get a working Watt-Wise checkout on a new machine. This guide covers what exists today: the root workspace, the shared JS tooling, the git hooks, the .NET tools and the Development database with its secrets. Further sections are added by the stories that need them.

## 1. Prerequisites

Install these yourself. Everything else comes from the repository.

| Tool                                                                                                                              | Why                                                                                   | Notes                                                                                                                                                                  |
| --------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| git                                                                                                                               | Version control                                                                       | Any recent version                                                                                                                                                     |
| [mise](https://mise.jdx.dev/getting-started.html)                                                                                 | Installs Node, pnpm, .NET and Betterleaks at the versions the repo asks for           | Must be [activated in your shell](https://mise.jdx.dev/getting-started.html#activate-mise) so the tools are on `PATH` in every terminal, including when git runs hooks |
| [Docker Engine](https://docs.docker.com/engine/install/) with the Compose plugin                                                  | Runs PostgreSQL and pgAdmin for Development, and the Testcontainers integration tests | `docker compose version` must work without `sudo` (add yourself to the `docker` group)                                                                                 |
| [1Password](https://1password.com/downloads/) desktop app and [CLI](https://developer.1password.com/docs/cli/get-started/) (`op`) | Database passwords and other secrets, injected with `op run`                          | Turn on the CLI integration in the desktop app (Settings → Developer). You need access to the `Watt Wise Development` vault                                            |
| [gh](https://cli.github.com/) (optional)                                                                                          | Pull requests from the command line                                                   | Run `gh auth login` once                                                                                                                                               |

Don't install Node, pnpm or .NET globally for this project. mise provides them per directory, and a global copy can shadow the right one.

To activate mise in bash, add this to `~/.bashrc`, then open a new terminal. For zsh, put the same line in `~/.zshrc` with `zsh` instead of `bash`.

```bash
eval "$(mise activate bash)"
```

## 2. Clone and install the tools

```bash
git clone git@github.com:Brijunas/watt-wise.git
cd watt-wise
mise trust
mise install
```

- `mise trust` is needed once per clone: mise doesn't load a project's `mise.toml` until you trust it.
- `mise install` installs Node, pnpm, Betterleaks (the secret scanner used by the pre-commit hook) and the .NET SDK from the root `mise.toml`. How their versions are chosen is described under "Tool versions" in [technical.md](technical.md).

Check what's active:

```bash
mise current
```

Then restore the .NET local tools (`dotnet-ef`) pinned in `.config/dotnet-tools.json`:

```bash
dotnet tool restore
```

## 3. Install the JS dependencies

```bash
pnpm install
```

- pnpm is the only supported package manager. `npm install` and `yarn` are rejected by a `preinstall` check.
- The install fails if your Node or pnpm is older than the `engines` floors in `package.json`. That usually means mise isn't active in your shell.
- The `prepare` script installs the git hooks (Husky). `git config core.hooksPath` should now print `.husky/_`.

## 4. Check 1Password access

The Development database settings, passwords and app URLs (the Api listen URL, and the frontend and admin origins the Api allows through CORS) are read from the `Watt Wise Development` vault; its items are listed under "Passwords" in [deploy/docs/postgres.md](../deploy/docs/postgres.md#passwords). Check that the CLI can reach the vault. The desktop app asks you to approve the first access.

```bash
op item list --vault "Watt Wise Development"
```

## 5. Development database

One-time setup of the Development PostgreSQL. The roles and why they exist are in [deploy/docs/postgres.md](../deploy/docs/postgres.md).

1. Render the stack's settings (bind addresses, ports, user names, database name) from 1Password into `deploy/.env`, which Docker Compose reads automatically. The file holds no passwords and is gitignored. Render it again whenever one of those values changes in 1Password.

   ```bash
   op inject -f -i deploy/development/compose.settings.tpl -o deploy/.env
   ```

2. Start the stack for the first time. The images read the `admin` and pgAdmin passwords only to initialize empty volumes, so only this first start goes through `op run`. Every later compose command (`up`, `down`, `logs`, `exec`) runs without it.

   ```bash
   op run --env-file deploy/development/compose.env -- docker compose -f deploy/docker-compose.development.yml up -d --wait
   ```

3. Create the roles, the database, the schemas and the privileges:

   ```bash
   docker compose -f deploy/docker-compose.development.yml exec -T postgres sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < deploy/postgres/bootstrap.sql
   ```

4. Set each role's password. Open psql as the superuser:

   ```bash
   docker compose -f deploy/docker-compose.development.yml exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
   ```

   Then run these one at a time. For each one, paste the password from the matching 1Password item (`postgres-api`, `postgres-cli`, `postgres-hangfire`, `postgres-backup`) twice. Quit with `\q`.

   ```
   \password api
   \password cli
   \password hangfire
   \password backup
   ```

5. Create the tables. The Cli takes every connection field (host, port, database, user, password, options) from the `postgres-cli` item:

   ```bash
   op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- migrate
   ```

pgAdmin listens on the address and port in the `pgadmin` item (http://127.0.0.1:5050 today). The first time you open the server, it asks for the `postgres-admin` password.

## 6. Check that everything works

Run the [checks](development.md#checks) from development.md. All five should exit without errors. From here on, [development.md](development.md) covers everyday work.

## Editor

Any editor works. For VS Code, install the ESLint and Prettier extensions and turn on format-on-save with Prettier.

**TypeScript version.** The repo has TypeScript 7 (`tsc`) and TypeScript 6 (the `typescript` package) side by side, because typescript-eslint doesn't support TypeScript 7 yet. Editors pick up TypeScript 6 from `node_modules/typescript`, which matches what ESLint sees. [known-issues.md](known-issues.md) explains the setup and when it goes away.

## Claude Code

Project settings for Claude Code ship with the repo (`.claude/` and `CLAUDE.md`), so there is nothing to install. What they enforce is described under "Code style / linting" and "Secrets and configuration" in [technical.md](technical.md). Personal overrides go in `.claude/settings.local.json`, which stays out of git.

## Troubleshooting

| Problem                                                                         | Fix                                                                                                                                                                       |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `node`, `pnpm` or `betterleaks` not found, or the wrong version                 | mise isn't active in this shell. Check `mise doctor`, add `mise activate` to your shell config and open a new terminal.                                                   |
| mise ignores `mise.toml`                                                        | Run `mise trust` in the repo root.                                                                                                                                        |
| `pnpm install` fails with an engines error                                      | Same as above: an old Node or pnpm from outside mise is on `PATH`.                                                                                                        |
| `op` asks to sign in, or can't find the vault                                   | Turn on the CLI integration in the 1Password desktop app, and check that your account has access to `Watt Wise Development`.                                              |
| Output under `op run` shows `<concealed by 1Password>`, e.g. in container names | `op run` masks every value it injected, including non-secret ones like the database name `postgres`. Harmless. Add `--no-masking` only when you need to read such output. |
| `docker` needs `sudo`                                                           | Add yourself to the `docker` group (`sudo usermod -aG docker $USER`), then log out and back in.                                                                           |
| Commits don't run the hook                                                      | Run `pnpm install` again to rerun `prepare`, then check `git config core.hooksPath` prints `.husky/_`.                                                                    |
