# Setup

How to get a working Watt-Wise checkout on a new machine. This guide covers what exists today: the root workspace, the shared JS tooling, the git hooks, the .NET tools and the Development database with its secrets. Further sections are added by the stories that need them.

## 1. Prerequisites

Install git and Docker Engine with the Compose plugin yourself; `./setup` installs the rest. It supports Fedora only (vidar's release); on another distribution follow [Other distributions](#other-distributions).

| Tool                                                                             | Why                                                                        | Notes                                                                                                                                                                                                                                                                                                                                                                                          |
| -------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| git                                                                              | Version control                                                            | Any recent version                                                                                                                                                                                                                                                                                                                                                                             |
| [Docker Engine](https://docs.docker.com/engine/install/) with the Compose plugin | Runs the whole system: PostgreSQL, Grafana LGTM, Mailpit and the four apps | `docker compose version` must work without `sudo` (add yourself to the `docker` group, or run [rootless Docker](https://docs.docker.com/engine/security/rootless/) as vidar and tethys do; rootless Docker must use the slirp4netns network driver with the builtin port driver, so published ports keep their host IP: [environments.md](../deploy/docs/environments.md#why-rootless-docker)) |
| [gh](https://cli.github.com/) (optional)                                         | Pull requests from the command line                                        | Run `gh auth login` once                                                                                                                                                                                                                                                                                                                                                                       |

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
8. **`deploy/.env`.** `op inject` renders `deploy/development/compose.settings.tpl` (bind addresses, ports, user names, database name, the four apps' `url`s; no passwords) from the `Watt Wise Development` vault, and `./setup` appends each app's published `<APP>_BIND_ADDRESS` and `<APP>_PORT`, derived from its `url`; a `url` that isn't a loopback `http` origin with a port stops it, and so does a bind address of another service that isn't a loopback address ([how](../deploy/docs/development-stack.md#published-ports)). It replaces the gitignored file only when the result differs.

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

The first start of the Development PostgreSQL runs by itself. The roles and why they exist are in [deploy/docs/postgres.md](../deploy/docs/postgres.md).

Start the whole system with one `up`, through `op run` (every `up` does; `down`, `logs` and `exec` need none), from the main checkout (the clone kept on `main`), which is the stack's home ([development-stack.md](../deploy/docs/development-stack.md#one-stack-per-machine)). On an empty volume it initializes PostgreSQL, then the one-shot `db-init` creates the roles, the database, the schemas and the privileges and sets every login role's password from its 1Password item, then the one-shot `cli` creates the tables (the app's and Hangfire's) and seeds the smoke accounts, and only then do the Api, the Jobs host, the frontend and the admin start, built from their `dev` targets. The first run takes a few minutes. The apps connect as `api` and `jobs` from the `postgres-api` and `postgres-jobs` items and are published on the `url`s of the `api`, `jobs`, `frontend` and `admin` items. The Jobs host's Hangfire dashboard answers 401 in the container ([why](../deploy/docs/development-stack.md#the-hangfire-dashboard)); [running-on-the-host.md](running-on-the-host.md) runs the host on the host.

```bash
op run --env-file deploy/development/compose.env -- docker compose -f deploy/docker-compose.development.yml up -d --wait
```

If it fails, read `docker compose -f deploy/docker-compose.development.yml logs db-init cli`. A machine that already has a database volume keeps it: `db-init` finds the passwords set and changes nothing. How it works: [development-stack.md](../deploy/docs/development-stack.md#first-start).

For an optional GUI client, see [Connecting with pgAdmin](../deploy/docs/postgres.md#connecting-with-pgadmin).

The stack also runs Grafana LGTM, which receives the apps' traces, metrics and logs. Grafana is at the `grafana` item's `url` (http://127.0.0.1:3000 today), without a login; see [deploy/docs/lgtm.md](../deploy/docs/lgtm.md). On a machine set up before S2.8, run `./setup` again to render `deploy/.env` and then run the `up` of step 5 to add it.

The stack also runs Mailpit, which catches every email the Development Jobs host sends. Its web UI is at the `mailpit` item's `url` (http://127.0.0.1:8025 today); see [deploy/docs/mailpit.md](../deploy/docs/mailpit.md). On a machine set up before S4.2, run `./setup` again to render `deploy/.env` and then run the `up` of step 5 to add it. A machine set up before S5B.5 does the same: `./setup` renders the new keys and the `up` builds and starts the four apps.

## 6. Check that everything works

Run the [checks](development.md#checks) from development.md. Every one should pass; the backend tests need the Docker daemon running. From here on, [development.md](development.md) covers everyday work.

## Dev VM (tethys)

tethys runs Development remotely and is reached over SSH only. What the provisioning script does and why is in [environments.md](../deploy/docs/environments.md#development-on-tethys); the steps are:

1. **Create the VM** (the developer): Fedora Server on saturn, your user with sudo, and your public key in `~/.ssh/authorized_keys`. The script stops without a key, because it turns passwords off.
2. **Provision it**, from vidar, with the network SSH is accepted from, `<LAN-CIDR>`, which is in the private runbook. The script needs no clone; it installs git. Run it twice: the second run prints no `changed`. Before closing this session, open a second SSH session with your key.

   ```bash
   scp deploy/provision/dev-vm.sh tethys:
   ssh -t tethys 'bash ~/dev-vm.sh --lan <LAN-CIDR>'
   ```

3. **The token.** Put the service-account token in place from a machine that has it (or skip this and paste it at `./setup`'s prompt in an `ssh -t` session): `ssh tethys 'install -d -m 700 ~/.config/watt-wise'`, then `scp ~/.config/watt-wise/op-service-account-token tethys:.config/watt-wise/`.
4. **Clone over HTTPS** (never put a private key on tethys), run `./setup` and then the one `up` of [step 5](#5-development-database), in a new shell:

   ```bash
   git clone https://github.com/Brijunas/watt-wise.git && cd watt-wise && ./setup
   ```

5. **The forwards.** Stop vidar's own stack and add the `LocalForward` lines for the ports in tethys's `deploy/.env` to vidar's `~/.ssh/config`: [environments.md](../deploy/docs/environments.md#reaching-the-apps). VS Code connects with Remote-SSH ([debugging.md](../deploy/docs/debugging.md#remote-tethys)).

## Checking setup

`.github/scripts/check-setup.sh` tests `./setup` on a throwaway Fedora container that has only git and the Docker CLI, built from `.github/setup-check/Dockerfile`; CI's `setup` job runs it. It clones the committed HEAD of the repo, so commit before you run it, or pass `--source <dir>` (for example `--source .`) to test that directory's working tree, uncommitted changes included.

| Command                                                            | What it asserts                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `bash .github/scripts/check-setup.sh`                              | `./setup --skip-secrets` twice: both exit 0, the second reports no `changed` and leaves `~/.bashrc`, `~/.bashrc.d`, `git status`, the Husky hooks, the rpm packages, the mise tools and the yum repos as they were; one `mise activate` line (none added when `~/.bashrc.d` has it); an empty commit runs the pre-commit hook; setup as root stops; node, pnpm, dotnet, betterleaks, zizmor and op run in a new shell; no terminal and no token file stops; Debian is refused; `./setup --derive-url` accepts the loopback `url`s and rejects the others (the published-port derivation's case table); `./setup --check-bind` accepts `127.x.x.x` and `::1` for the five infrastructure bind addresses (PostgreSQL, Grafana, OTLP, Mailpit's SMTP and web UI) and rejects every other value, naming the key and the 1Password item. |
| `bash .github/scripts/check-setup.sh --token-file <file>`          | Also the secrets steps with that service-account token (mounted read-only and copied into the container with loose modes, so setup has to tighten them): the file ends mode 600 in a 700 folder owned by the user, one token line in `~/.bashrc`, `deploy/.env` has every key (the derived bind addresses and ports too) and no `{{`, `op vault list` shows only `Watt Wise Development`, and another vault can't be listed.                                                                                                                                                                                                                                                                                                                                                                                                        |
| `bash .github/scripts/check-setup.sh --token-file <file> --checks` | Also the [checks](development.md#checks) of development.md inside the container. It mounts the host's Docker socket, so the container controls the host's Docker; the socket path is read from the Docker context, and the user in the container joins the group that owns the mounted socket (its gid is read there, as it depends on the host). It is written for rootless Docker. It doesn't run in CI.                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `bash .github/scripts/check-setup.sh --interactive`                | The same assertions as `--token-file`, but you paste the token at setup's hidden prompt, so the loose-modes tightening isn't checked.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |

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

| Problem                                                                         | Fix                                                                                                                                                                                                                                  |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `node`, `pnpm` or `betterleaks` not found, or the wrong version                 | mise isn't active in this shell. Check `mise doctor`, add `mise activate` to your shell config and open a new terminal.                                                                                                              |
| mise ignores `mise.toml`                                                        | Run `mise trust` in the repo root.                                                                                                                                                                                                   |
| `pnpm install` fails with an engines error                                      | Same as above: an old Node or pnpm from outside mise is on `PATH`.                                                                                                                                                                   |
| `op` asks to sign in, or can't find the vault                                   | Turn on the CLI integration in the 1Password desktop app, and check that your account has access to `Watt Wise Development`.                                                                                                         |
| Output under `op run` shows `<concealed by 1Password>`, e.g. in container names | `op run` masks every value it injected, including non-secret ones like the database name `postgres`. Harmless. Add `--no-masking` only when you need to read such output.                                                            |
| `docker` needs `sudo`                                                           | Add yourself to the `docker` group (`sudo usermod -aG docker $USER`), then log out and back in, or switch to rootless Docker (`docker context use rootless`, [environments.md](../deploy/docs/environments.md#why-rootless-docker)). |
| `./setup` stops with "this installer supports Fedora only"                      | It runs on Fedora only. Follow [Other distributions](#other-distributions).                                                                                                                                                          |
| `./setup` stops with "no terminal" and the token path                           | A headless machine needs the service-account token once. Run `./setup` again in a terminal and paste it, or put it in that file yourself (mode 600).                                                                                 |
| `./setup` stops with "op inject failed"                                         | Desktop: turn on the CLI integration in the 1Password desktop app. Headless: the token is wrong, expired or revoked; replace the file's content or delete the file and run `./setup` again.                                          |
| `./setup` stops with "belongs to another user"                                  | The token file must be owned by you. Fix its owner or delete it and run `./setup` again.                                                                                                                                             |
| Commits don't run the hook                                                      | Run `pnpm install` again to rerun `prepare`, then check `git config core.hooksPath` prints `.husky/_`.                                                                                                                               |
