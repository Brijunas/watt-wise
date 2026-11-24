# Setup

How to get a working Watt-Wise checkout on a new machine. This guide covers what exists today: the root workspace, the shared JS tooling, the git hooks, the .NET tools and the Development database with its secrets. Further sections are added by the stories that need them.

## 1. Prerequisites

Install git and Docker Engine with the Compose plugin yourself; `./setup` installs the rest. It supports Fedora only (vidar's release); on another distribution follow [Other distributions](#other-distributions).

| Tool                                                                             | Why                                                                        | Notes                                                                                  |
| -------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | -------------------------------------------------------------------------------------- |
| git                                                                              | Version control                                                            | Any recent version                                                                     |
| [Docker Engine](https://docs.docker.com/engine/install/) with the Compose plugin | Runs the whole system: PostgreSQL, Grafana LGTM, Mailpit and the four apps | `docker compose version` must work without `sudo` (add yourself to the `docker` group) |
| [gh](https://cli.github.com/) (optional)                                         | Pull requests from the command line                                        | Run `gh auth login` once                                                               |

Which 1Password access `./setup` uses depends on the machine:

- **Desktop** (vidar): the [1Password desktop app](https://1password.com/downloads/) is installed. Turn on the CLI integration (Settings → Developer) and make sure your account can read the `Watt Wise Development` vault. The app asks you to approve each access.
- **Headless** (tethys, a VM, CI): no desktop app. `./setup` asks once for the token of the `watt-wise-development-readonly` service account, [created below](#service-account-for-a-headless-machine).

Don't install Node, pnpm or .NET globally for this project. mise provides them per directory, and a global copy can shadow the right one.

## 2. Clone and run setup

```bash
git clone git@github.com:Brijunas/watt-wise.git
cd watt-wise
./setup
```

Run it as your own user, not as root. It prints `setup: <step>: unchanged|changed|skipped` for each step and is safe to rerun: it changes only what is missing or out of date, so run it again after pulling a change to `mise.toml`, `pnpm-lock.yaml`, `.config/dotnet-tools.json` or one of the 1Password values `deploy/.env` holds. Its steps:

1. **Preflight.** Stops unless the machine is Fedora and you are not root, and `git` and `docker compose version` work.
2. **Mode.** Desktop when `/opt/1Password/1password` or a `1password` command exists, headless otherwise. The token file never decides it, so vidar stays on the app. `--headless` forces headless.
3. **System packages**, through `sudo dnf` and only for what is missing (a machine that has them gets no sudo prompt): `libicu` (.NET) and `libatomic` (Node), mise from [its signed RPM repo](https://mise.jdx.dev/installing-mise.html) and the 1Password CLI from [1Password's](https://developer.1password.com/docs/cli/get-started/), both repos with `gpgcheck` on.
4. **Shell.** Adds `eval "$(mise activate bash)"` to `~/.bashrc` unless a `mise activate` line is already in it or in a file under `~/.bashrc.d/`. Bash only: with zsh add the mise line yourself to `~/.zshrc` (with `zsh` instead of `bash`), and on a headless machine the token line of step 7 too.
5. **Tools.** `mise trust` for the repo's `mise.toml`, then `mise install`: Node, pnpm, Betterleaks, zizmor and the .NET SDK. How their versions are chosen is under "Tool versions" in [technical.md](technical.md).
6. **Dependencies.** `pnpm install --frozen-lockfile` (its `prepare` script installs the Husky hooks, so `git config core.hooksPath` prints `.husky/_`) and `dotnet tool restore` for the local tools in `.config/dotnet-tools.json`. pnpm is the only supported package manager.
7. **Token** (headless only). The token lives in `${XDG_CONFIG_HOME:-~/.config}/watt-wise/op-service-account-token`; [security.md](security.md#deployment) has its modes and why. Without the file, setup asks for the token at a hidden prompt, or stops with the path when it has no terminal. Looser modes are tightened; a file or folder owned by another user stops setup. It adds a guarded line to `~/.bashrc` that exports `OP_SERVICE_ACCOUNT_TOKEN` from the file, so `op` works in every new terminal.
8. **`deploy/.env`.** `op inject` renders `deploy/development/compose.settings.tpl` (bind addresses, ports, user names, database name, the four apps' `url`s; no passwords) from the `Watt Wise Development` vault, and `./setup` appends each app's published `<APP>_BIND_ADDRESS` and `<APP>_PORT`, derived from its `url`; a `url` that isn't a loopback `http` origin with a port stops it ([how](../deploy/docs/development-stack.md#published-ports)). It replaces the gitignored file only when the result differs.

`--skip-secrets` skips steps 7 and 8, for CI and for a machine without vault access. A rerun after the floating `lts` and `latest` versions in `mise.toml` have moved installs the newer release (`tools: changed`). Open a new terminal (or `source ~/.bashrc`) afterwards so the tools are on `PATH`; `mise current` shows what is active.

### Service account for a headless machine

The developer creates it once, on a machine where the desktop app is signed in. It is read-only on the Development vault and on nothing else:

```bash
op service-account create watt-wise-development-readonly --vault "Watt Wise Development:read_items"
```

The command prints the token once. Save it in 1Password at once, and paste it into `./setup`'s prompt on each headless machine. To revoke it, delete the service account in 1Password and create a new one.

## 3. Check 1Password access

`./setup` already used the vault; this checks it by hand. The desktop app asks you to approve the first access.

```bash
op item list --vault "Watt Wise Development"
```

The vault's items are listed under "Passwords" in [deploy/docs/postgres.md](../deploy/docs/postgres.md#passwords).

## 4. Settings file

`./setup` rendered `deploy/.env` in step 8, which Docker Compose reads automatically. There is nothing to do here unless a value changed in 1Password (including an app's `url`, which sets its published port): run `./setup` again. The apps' own settings and passwords come from `deploy/development/compose.env` through `op run` at every `up`, never from this file.

## 5. Development database

One-time setup of the Development PostgreSQL. The roles and why they exist are in [deploy/docs/postgres.md](../deploy/docs/postgres.md).

1. Start the infrastructure for the first time. The PostgreSQL image reads the `admin` password only to initialize an empty volume. Only these three services start: `api` can't turn healthy before the roles and the migration below exist. Every `up` goes through `op run`; `down`, `logs` and `exec` need none.

   ```bash
   op run --env-file deploy/development/compose.env -- docker compose -f deploy/docker-compose.development.yml up -d --wait postgres lgtm mailpit
   ```

2. Create the roles, the database, the schemas and the privileges:

   ```bash
   docker compose -f deploy/docker-compose.development.yml exec -T postgres sh -c 'psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' < deploy/postgres/bootstrap.sql
   ```

3. Set each role's password. Open psql as the superuser:

   ```bash
   docker compose -f deploy/docker-compose.development.yml exec postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
   ```

   Then run these one at a time. For each one, paste the password from the matching 1Password item (`postgres-api`, `postgres-cli`, `postgres-jobs`, `postgres-backup`, `postgres-reader`) twice. Quit with `\q`.

   ```
   \password api
   \password cli
   \password jobs
   \password backup
   \password reader
   ```

4. Create the tables, both the app's and Hangfire's. On a machine set up before S2.9, run this again to add Hangfire's. The Cli takes every connection field (host, port, database, user, password, options) from the `postgres-cli` item:

   ```bash
   op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- migrate
   ```

5. Start the whole system: the Api, the Jobs host, the frontend and the admin, built from their `dev` targets (the first build takes a few minutes). They connect as `api` and `jobs` from the `postgres-api` and `postgres-jobs` items and are published on the `url`s of the `api`, `jobs`, `frontend` and `admin` items. The Jobs host's Hangfire dashboard answers 401 in the container ([why](../deploy/docs/development-stack.md#the-hangfire-dashboard)); [development.md](development.md#running-on-the-host) runs the host on the host.

   ```bash
   op run --env-file deploy/development/compose.env -- docker compose -f deploy/docker-compose.development.yml up -d --wait
   ```

For an optional GUI client, see [Connecting with pgAdmin](../deploy/docs/postgres.md#connecting-with-pgadmin).

The stack also runs Grafana LGTM, which receives the apps' traces, metrics and logs. Grafana is at the `grafana` item's `url` (http://127.0.0.1:3000 today), without a login; see [deploy/docs/lgtm.md](../deploy/docs/lgtm.md). On a machine set up before S2.8, run `./setup` again to render `deploy/.env` and then run the `up` of step 5 to add it.

The stack also runs Mailpit, which catches every email the Development Jobs host sends. Its web UI is at the `mailpit` item's `url` (http://127.0.0.1:8025 today); see [deploy/docs/mailpit.md](../deploy/docs/mailpit.md). On a machine set up before S4.2, run `./setup` again to render `deploy/.env` and then run the `up` of step 5 to add it. A machine set up before S5B.5 does the same: `./setup` renders the new keys and the `up` builds and starts the four apps.

## 6. Check that everything works

Run the [checks](development.md#checks) from development.md. Every one should pass; the backend tests need the Docker daemon running. From here on, [development.md](development.md) covers everyday work.

## Checking setup

`.github/scripts/check-setup.sh` tests `./setup` on a throwaway Fedora container that has only git and the Docker CLI, built from `.github/setup-check/Dockerfile`; CI's `setup` job runs it. It clones the committed HEAD of the repo, so commit before you run it.

| Command                                                            | What it asserts                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                        |
| ------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `bash .github/scripts/check-setup.sh`                              | `./setup --skip-secrets` twice: both exit 0, the second reports no `changed` and leaves `~/.bashrc`, `~/.bashrc.d`, `git status`, the Husky hooks, the rpm packages, the mise tools and the yum repos as they were; one `mise activate` line (none added when `~/.bashrc.d` has it); an empty commit runs the pre-commit hook; setup as root stops; node, pnpm, dotnet, betterleaks, zizmor and op run in a new shell; no terminal and no token file stops; Debian is refused; `./setup --derive-url` accepts the loopback `url`s and rejects the others (the published-port derivation's case table). |
| `bash .github/scripts/check-setup.sh --token-file <file>`          | Also the secrets steps with that service-account token (mounted read-only and copied into the container with loose modes, so setup has to tighten them): the file ends mode 600 in a 700 folder owned by the user, one token line in `~/.bashrc`, `deploy/.env` has every key (the derived bind addresses and ports too) and no `{{`, `op vault list` shows only `Watt Wise Development`, and another vault can't be listed.                                                                                                                                                                           |
| `bash .github/scripts/check-setup.sh --token-file <file> --checks` | Also the [checks](development.md#checks) of development.md inside the container. It mounts the host's Docker socket, so the container controls the host's Docker; the socket path is read from the Docker context, and the user in the container joins the group that owns the mounted socket (its gid is read there, as it depends on the host). It is written for rootless Docker. It doesn't run in CI.                                                                                                                                                                                             |
| `bash .github/scripts/check-setup.sh --interactive`                | The same assertions as `--token-file`, but you paste the token at setup's hidden prompt, so the loose-modes tightening isn't checked.                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |

## Other distributions

`./setup` stops on a distribution other than Fedora. By hand: install git, Docker with the Compose plugin, [mise](https://mise.jdx.dev/getting-started.html) (activated in your shell, so the tools are on `PATH` even when git runs hooks) and the [1Password CLI](https://developer.1password.com/docs/cli/get-started/), plus the native libraries .NET and Node need (ICU, libatomic). Then, from the repo root:

```bash
mise trust && mise install
pnpm install --frozen-lockfile && dotnet tool restore
op inject -f -i deploy/development/compose.settings.tpl -o deploy/.env
```

## Editor

Any editor works. For VS Code, install the ESLint and Prettier extensions and turn on format-on-save with Prettier.

**TypeScript version.** The repo has TypeScript 7 (`tsc`) and TypeScript 6 (the `typescript` package) side by side, because typescript-eslint doesn't support TypeScript 7 yet. Editors pick up TypeScript 6 from `node_modules/typescript`, which matches what ESLint sees. [known-issues.md](known-issues.md) explains the setup and when it goes away.

## Claude Code

Project settings for Claude Code ship with the repo (`.claude/` and `CLAUDE.md`), so there is nothing to install. What they enforce is described under "Code style / linting" and "Secrets and configuration" in [technical.md](technical.md). Personal overrides go in `.claude/settings.local.json`, which stays out of git.

`.mcp.json` adds three MCP servers that run in Docker: `postgres` (read-only, as the `reader` role through the `postgres-reader` item), `grafana` (Grafana LGTM) and `github` (uses the `gh` sign-in). `postgres` is off by default, because it asks 1Password for the `reader` password on every session start; turn it on with `/mcp` in a session that needs the database. `grafana` and `github` start with every session. `postgres` and `grafana` need the Development stack running; `postgres` also needs `op` signed in, `grafana` reads its URL from `deploy/.env` (rendered by `./setup`) and `github` needs `gh auth login`.

## Troubleshooting

| Problem                                                                         | Fix                                                                                                                                                                                         |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `node`, `pnpm` or `betterleaks` not found, or the wrong version                 | mise isn't active in this shell. Check `mise doctor`, add `mise activate` to your shell config and open a new terminal.                                                                     |
| mise ignores `mise.toml`                                                        | Run `mise trust` in the repo root.                                                                                                                                                          |
| `pnpm install` fails with an engines error                                      | Same as above: an old Node or pnpm from outside mise is on `PATH`.                                                                                                                          |
| `op` asks to sign in, or can't find the vault                                   | Turn on the CLI integration in the 1Password desktop app, and check that your account has access to `Watt Wise Development`.                                                                |
| Output under `op run` shows `<concealed by 1Password>`, e.g. in container names | `op run` masks every value it injected, including non-secret ones like the database name `postgres`. Harmless. Add `--no-masking` only when you need to read such output.                   |
| `docker` needs `sudo`                                                           | Add yourself to the `docker` group (`sudo usermod -aG docker $USER`), then log out and back in.                                                                                             |
| `./setup` stops with "this installer supports Fedora only"                      | It runs on Fedora only. Follow [Other distributions](#other-distributions).                                                                                                                 |
| `./setup` stops with "no terminal" and the token path                           | A headless machine needs the service-account token once. Run `./setup` again in a terminal and paste it, or put it in that file yourself (mode 600).                                        |
| `./setup` stops with "op inject failed"                                         | Desktop: turn on the CLI integration in the 1Password desktop app. Headless: the token is wrong, expired or revoked; replace the file's content or delete the file and run `./setup` again. |
| `./setup` stops with "belongs to another user"                                  | The token file must be owned by you. Fix its owner or delete it and run `./setup` again.                                                                                                    |
| Commits don't run the hook                                                      | Run `pnpm install` again to rerun `prepare`, then check `git config core.hooksPath` prints `.husky/_`.                                                                                      |
