# Setup

How to get a working Watt-Wise checkout on a new machine, on Linux, macOS or Windows, for any editor. This guide covers the root workspace, the shared JS tooling, the git hooks, the .NET tools and the Development database with its secrets. Further sections are added by the stories that need them.

## 1. Prerequisites

Install these yourself; `mise run setup` installs the rest and stops with the official install link of every one that is missing.

| Tool                                                                                    | Why                                                                        | Notes                                                                                                                                                                   |
| --------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [git](https://git-scm.com/downloads)                                                    | Version control                                                            | On Windows, [Git for Windows](https://gitforwindows.org) (the hooks run in its bash)                                                                                    |
| [Docker](https://docs.docker.com/engine/install/) with Compose 2.22 or newer            | Runs the whole system: PostgreSQL, Grafana LGTM, Mailpit and the four apps | `docker compose version` must work without `sudo`. Linux: Docker Engine; macOS and Windows: [Docker Desktop](https://docs.docker.com/desktop/)                          |
| [mise](https://mise.jdx.dev/getting-started.html), activated in your shell              | Provides Node, pnpm, .NET, Betterleaks, zizmor and shellcheck              | Activated so the tools are on `PATH` even when git runs a hook ([activate](https://mise.jdx.dev/getting-started.html#activate-mise)); run `mise trust` once in the repo |
| ICU, on Linux only                                                                      | .NET needs `libicu`                                                        | Your distribution's ICU package ([list](https://learn.microsoft.com/dotnet/core/install/linux)); on Fedora setup installs it                                            |
| [1Password CLI](https://developer.1password.com/docs/cli/get-started/) (`op`), every OS | Resolves every `op://` setting; mise doesn't provide it                    | Install it from 1Password's install page; any build works, and setup signs it in with the service-account token                                                         |
| `libatomic`, on Linux only                                                              | Node from mise can't start without it                                      | Your distribution's `libatomic` package (Fedora Workstation usually has it; the Fedora profile doesn't install it)                                                      |
| [gh](https://cli.github.com/) (optional)                                                | Pull requests from the command line                                        | Run `gh auth login` once                                                                                                                                                |

Don't install Node, pnpm or .NET globally for this project: mise provides them per directory, and a global copy can shadow the right one. No editor is required; [Editor](#editor) lists what helps.

Every machine and OS signs in to 1Password the same way: with the token of the read-only `watt-wise-development-readonly` service account, which setup asks for once ([created below](#service-account-for-1password)). There is no desktop-app mode: the `op` CLI can be any build from 1Password's install page.

## 2. Clone and run setup

```bash
git clone git@github.com:Brijunas/watt-wise.git
cd watt-wise
mise trust
mise run setup
```

`mise trust` comes first: mise refuses to read the repo's `mise.toml` (and so to run the `setup` task) until you trust it.

On Windows with WSL, clone inside the WSL file system (`~/code/...`), not under `/mnt/c`: bind mounts and file watching are far faster there, and setup then needs no polling.

Run it as your own user, not as root. It prints `setup: <step>: unchanged|changed|skipped` for each step and is safe to rerun: it changes only what is missing or out of date, so run it again after pulling a change to `mise.toml`, `pnpm-lock.yaml`, `.config/dotnet-tools.json` or one of the 1Password values `deploy/.env` holds. Flags go after `--` (`mise run setup -- --skip-secrets`); `mise run setup -- --help` lists them. Its steps:

1. **Fedora profile** (Fedora only, any version, before the prerequisite check). `sudo dnf install libicu` when missing (not `libatomic`: a [prerequisite](#1-prerequisites)), and a report on Docker: rootless is checked (the user's service, linger, `/etc/subuid`, the `rootless` context) and the gaps are listed with their fix; rootful Docker is left alone and noted. It changes no firewall and no sshd.
2. **Prerequisites.** `git`, `docker compose` (2.22 or newer), mise activated, the 1Password CLI (a missing `op` stops with the install link), ICU on Linux, Git for Windows on Windows (`libatomic` isn't checked); every gap is listed with its link, then setup stops.
3. **Tools.** `mise self-update` when mise allows it (a package-manager install doesn't: `skipped`), then `mise install`: Node, pnpm, Betterleaks, zizmor, shellcheck and .NET. How the versions are chosen is under "Tool versions" in [technical.md](technical.md).
4. **Dependencies.** `pnpm install --frozen-lockfile` (its `prepare` script installs the Husky hooks, so `git config core.hooksPath` prints `.husky/_`) and `dotnet tool restore`. pnpm is the only supported package manager.
5. **1Password.** Setup asks for the service-account token at a hidden prompt (never an argument) when `~/.config/watt-wise/op.env` doesn't exist, and never again after that. It writes `~/.config/watt-wise/op.env` (mode 600 in a folder mode 700, or an `icacls` ACL for your user alone on Windows, checked after it is set; a file or folder of another user stops setup) and stops with the path when it has no terminal. [security.md](security.md#deployment) says why, and that mise loads the file into the environment of every process in the repo. A token file of the earlier installer (`op-service-account-token`) is converted once, and setup prints the old `~/.bashrc` line for you to remove; it never edits a shell profile.
6. **`deploy/.env`.** `op inject` renders `deploy/development/compose.settings.tpl` (bind addresses, ports, user names, database name, the four apps' `url`s; no passwords) from the `Watt Wise Development` vault, and setup appends each app's published `<APP>_BIND_ADDRESS` and `<APP>_PORT`, derived from its `url`, and `WATCH_POLLING`. A `url` that isn't a loopback `http` origin with a port stops it, and so does a bind address of another service that isn't loopback ([how](../deploy/docs/development-stack.md#published-ports)). It replaces the gitignored file only when the result differs. `--derive-url` and `--check-bind` run those two checks on their own.
7. **Polling.** `WATCH_POLLING` is `true` on macOS and on Windows outside WSL, where a bind mount delivers no file events, and `false` elsewhere. `--polling on|off` saves an override; `--polling auto` drops it. It feeds `dotnet watch` and Vite ([development-stack.md](../deploy/docs/development-stack.md#the-tree-and-what-the-containers-write)).
8. **Testcontainers.** `docker info` must answer on the active context, because the backend's integration tests start PostgreSQL through it.

`--skip-secrets` skips steps 5 to 7's 1Password parts (no sign-in, no `deploy/.env`), for CI and for a machine without vault access. A rerun after the floating `lts` and `latest` versions in `mise.toml` have moved installs the newer release (`tools: changed`). Open a new terminal afterwards so the tools are on `PATH`; `mise current` shows what is active.

### Service account for 1Password

The developer creates it once, from any machine signed in to 1Password. It is read-only on the Development vault and on nothing else:

```bash
op service-account create watt-wise-development-readonly --vault "Watt Wise Development:read_items"
```

The command prints the token once. Save it in 1Password at once, and paste it into setup's prompt on each machine. To revoke it, delete the service account in 1Password and create a new one.

## 3. Check 1Password access

Setup already used the vault; this checks it by hand. `op` reads the token from the environment mise loads, so nothing asks for an approval.

```bash
op item list --vault "Watt Wise Development"
```

The vault's items are listed under "Passwords" in [deploy/docs/postgres.md](../deploy/docs/postgres.md#passwords).

## 4. Settings file

Setup rendered `deploy/.env`, which Docker Compose reads automatically. There is nothing to do here unless a value changed in 1Password (including an app's `url`, which sets its published port): run `mise run setup` again. The apps' own settings and passwords come from `deploy/development/compose.env` through `op run` at every `up`, never from this file.

## 5. Development database

The first start of the Development PostgreSQL runs by itself. The roles and why they exist are in [deploy/docs/postgres.md](../deploy/docs/postgres.md).

Start the whole system with `mise run up` (every `up` goes through `op run`; `down` and `logs` need none), from the main checkout (the clone kept on `main`), which is the stack's home ([development-stack.md](../deploy/docs/development-stack.md#one-stack-per-machine)). On an empty volume it initializes PostgreSQL, then the one-shot `db-init` creates the roles, the database, the schemas and the privileges and sets every login role's password from its 1Password item, then the one-shot `cli` creates the tables (the app's and Hangfire's) and seeds the smoke accounts, and only then do the Api, the Jobs host, the frontend and the admin start, built from their `dev` targets. The first run takes a few minutes. The apps connect as `api` and `jobs` from the `postgres-api` and `postgres-jobs` items and are published on the `url`s of the `api`, `jobs`, `frontend` and `admin` items. The Jobs host's Hangfire dashboard answers 401 in the container ([why](../deploy/docs/development-stack.md#the-hangfire-dashboard)); [running-on-the-host.md](running-on-the-host.md) runs the host on the host.

If it fails, read `docker compose -f deploy/docker-compose.development.yml logs db-init cli`. A machine that already has a database volume keeps it: `db-init` finds the passwords set and changes nothing. How it works: [development-stack.md](../deploy/docs/development-stack.md#first-start). The raw command behind the task is in [development.md](development.md#running-the-system).

For an optional GUI client, see [Connecting with pgAdmin](../deploy/docs/postgres.md#connecting-with-pgadmin).

The stack also runs Grafana LGTM, which receives the apps' traces, metrics and logs. Grafana is at the `grafana` item's `url`, without a login; see [deploy/docs/lgtm.md](../deploy/docs/lgtm.md). It also runs Mailpit, which catches every email the Development Jobs host sends; its web UI is at the `mailpit` item's `url` ([mailpit.md](../deploy/docs/mailpit.md)). A machine set up before a story added a service runs `mise run setup` again to render the new keys, then `mise run up`.

## 6. Check that everything works

`mise run check` runs the [checks](development.md#checks) from development.md. Every one should pass; the backend tests need the Docker daemon running. From here on, [development.md](development.md) covers everyday work.

## Where it is tested

**CI-tested: Fedora (latest, container).** CI's `setup` job runs `mise run setup --skip-secrets` and then `mise run check` in a `fedora:latest` container that has only git, Docker's CLI and the runner's Docker socket, built from `.github/setup-check/Dockerfile`; `bash .github/scripts/check-setup.sh` runs it locally ([workflows README](../.github/workflows/README.md)). The setup unit tests (`node --test tools/`) run in `pnpm test`.

**macOS and native Windows: expected to work, untested.** The first Windows user verifies the `icacls` check of the token file, and that mise's `{{ xdg_config_home }}` (in `mise.toml`) resolves to the folder setup writes `op.env` to (`XDG_CONFIG_HOME` or `~/.config`), and says what broke.

## Not OS-independent

What setup can't make the same everywhere, and the check it runs for each:

| What                              | Why it differs                                                                                                                                                                                            | The check                                                                                                                                               |
| --------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| git and Docker with Compose 2.22  | Installed by hand, differently on each OS                                                                                                                                                                 | Prerequisites step: `git --version`, `docker compose version --short`, with the install link                                                            |
| ICU on Linux                      | .NET loads the system's `libicu`; macOS and Windows have their own                                                                                                                                        | Prerequisites step looks for `libicuuc` (or `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT`); Fedora installs it                                                |
| `libatomic` on Linux              | Node's binary links it; absent on some minimal images                                                                                                                                                     | None by setup: Node fails to start without it (install your distribution's package)                                                                     |
| Git for Windows on Windows        | The hooks and line endings expect its bash; `.gitattributes` forces `eol=lf`                                                                                                                              | Prerequisites step: `git --version` must say Windows                                                                                                    |
| Rootless Docker (Linux, optional) | The dev stack's bind mount is read-only for containers' users on rootless Docker, which is the supported way on Linux ([why](../deploy/docs/development-stack.md#the-tree-and-what-the-containers-write)) | The Fedora profile reports it and lists what is missing; other distributions follow Docker's [guide](https://docs.docker.com/engine/security/rootless/) |
| Remote-machine hardening          | Key-only SSH and a firewall that takes SSH from the home network only belong to the machine and its owner                                                                                                 | None: it is the owner's job ([security.md](security.md#deployment)); setup only enforces loopback binds                                                 |
| Token file protection             | `chmod` on POSIX, `icacls` on Windows                                                                                                                                                                     | Token step verifies the mode or ACL after setting it                                                                                                    |

## Editor

Any editor works. For VS Code, install the ESLint and Prettier extensions and turn on format-on-save with Prettier; debugging is in [debugging.md](../deploy/docs/debugging.md).

**TypeScript version.** The repo has TypeScript 7 (`tsc`) and TypeScript 6 (the `typescript` package) side by side, because typescript-eslint doesn't support TypeScript 7 yet. Editors pick up TypeScript 6 from `node_modules/typescript`, which matches what ESLint sees. [known-issues.md](known-issues.md) explains the setup and when it goes away.

## Claude Code

Project settings for Claude Code ship with the repo (`.claude/` and `CLAUDE.md`), so there is nothing to install. What they enforce is described under "Code style / linting" and "Secrets and configuration" in [technical.md](technical.md). Personal overrides go in `.claude/settings.local.json`, which stays out of git.

`.mcp.json` adds three MCP servers that run in Docker: `postgres` (read-only, as the `reader` role through the `postgres-reader` item), `grafana` (Grafana LGTM) and `github` (uses the `gh` sign-in). `postgres` is off by default, because it reads the `reader` password from 1Password at every session start; turn it on with `/mcp` in a session that needs the database. `grafana` and `github` start with every session. `postgres` and `grafana` need the Development stack running; `postgres` also needs `op` signed in, `grafana` reads its URL from `deploy/.env` (rendered by setup) and `github` needs `gh auth login`.

## Troubleshooting

| Problem                                                                         | Fix                                                                                                                                                                             |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `node`, `pnpm` or `betterleaks` not found, or the wrong version                 | mise isn't active in this shell. Check `mise doctor`, add `mise activate` to your shell config and open a new terminal.                                                         |
| mise ignores `mise.toml`                                                        | Run `mise trust` in the repo root.                                                                                                                                              |
| `pnpm install` fails with an engines error                                      | Same as above: an old Node or pnpm from outside mise is on `PATH`.                                                                                                              |
| `op` asks to sign in, or can't find the vault                                   | The token is missing from the environment (open a new shell in the repo, with mise activated) or the service account can't read `Watt Wise Development`.                        |
| Output under `op run` shows `<concealed by 1Password>`, e.g. in container names | `op run` masks every value it injected, including non-secret ones like the database name `postgres`. Harmless. Add `--no-masking` only when you need to read such output.       |
| `docker` needs `sudo`                                                           | On Linux add yourself to the `docker` group (`sudo usermod -aG docker $USER`), then log out and back in, or switch to rootless Docker (`docker context use rootless`).          |
| Setup stops with "these prerequisites are missing or need a fix:"               | Install what it lists, from its link, and run `mise run setup` again.                                                                                                           |
| Setup stops with "the token file ... is missing and there is no terminal"       | Setup needs the service-account token once. Run setup again in a terminal and paste it, or put `OP_SERVICE_ACCOUNT_TOKEN=<token>` in that file yourself (readable by you only). |
| Setup stops with "op inject failed"                                             | The token is wrong, expired or revoked; delete `~/.config/watt-wise/op.env` and run setup again.                                                                                |
| Setup stops with "belongs to another user"                                      | The token file or its folder must be owned by you. Fix the owner or delete it and run setup again.                                                                              |
| Setup stops on the Docker daemon                                                | Start Docker; the backend tests need it ([development.md](development.md#troubleshooting)).                                                                                     |
| Commits don't run the hook                                                      | Run `pnpm install` again to rerun `prepare`, then check `git config core.hooksPath` prints `.husky/_`.                                                                          |
