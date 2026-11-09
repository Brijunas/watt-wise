# Agent host (tethys)

tethys is a VM on saturn with about 8.6 GB of RAM (two parallel test runs peaked at 6.4 GB). Epic runs execute there as the user `corvus`, with the repo at `/home/corvus/code/personal/watt-wise`: the orchestrator in tmux, its Sonnet subagents in worktrees, and the epic's Development stack (PostgreSQL, Grafana LGTM, Mailpit) and Testcontainers in Docker. How a run works is in [agentic-delivery.md](../../docs/agentic-delivery.md); why the VM is the security boundary is in [security.md](../../docs/security.md#agents). The scripts are in `deploy/agent-host/`.

## Same Claude everywhere

The Claude configuration is identical on vidar and tethys. Only credentials differ.

- **Project config** (`.claude/`, both `settings.json`, `CLAUDE.md` files) comes from git. tethys has nothing of its own; `agent-user.sh` writes no Claude settings.
- **User-level config** is mirrored from vidar by `sync-user-claude.sh`, run on vidar. It copies `~/.claude/CLAUDE.md`, mirrors the marketplaces with the same source as vidar's `known_marketplaces.json` (a remote one of the same name with another source is removed and re-added), installs every user-scope plugin of vidar's `installed_plugins.json` enabled or disabled as vidar has it, uninstalls remote user-scope plugins vidar doesn't have (matched by `name@marketplace`), installs the Orca skills (`orca skills install ... --agent claude`; `orca` is found on `PATH` or at `~/.orca/cli/bin/orca`, which non-interactive ssh doesn't have on `PATH`), and merges a fixed list of keys from vidar's `~/.claude/settings.json` into tethys's. It never touches the hooks and status line Orca installs per host, and never credentials. It ends with a parity check that diffs the normalized user config of both machines (CLAUDE.md, plugin list, skill list, merged keys) and exits non-zero on any difference. `start-epic.sh` runs the same check and warns, but only from another machine than tethys; on tethys the check and the memory sync run from vidar.
- **Auto mode** (`permissions.defaultMode: auto`) is a user setting: project settings can't set it. It reaches every machine through the mirrored user settings above.
- **Memory** is synced both ways with `unison`, because Claude keys a project's memory folder by its path, which differs per machine and per checkout. On tethys one canonical folder, `~/.claude/wattwise-memory/`, is symlinked as `memory` into every watt-wise project key (the main checkout, the Orca worktrees, the epic worktree that `start-epic.sh` adds). Unison syncs vidar's memory folder with that canonical folder. A systemd user timer on vidar runs it every 15 minutes while vidar is awake, and `start-epic.sh` syncs once at start when run from vidar with tethys reachable. On a conflict unison keeps both copies (`-copyonconflict`) and the next session that reads them merges them.

## State and setup

tethys is provisioned: the three steps below have run, and the scripts are safe to rerun.

1. **`deploy/agent-host/setup-tethys.sh [--dry-run]`**, run as `corvus` on tethys. tethys has passwordless sudo, so the script uses `sudo -n` and an agent can run the package steps without a human; where sudo needs a password it falls back to plain `sudo` and needs a terminal. It installs Docker CE and adds `corvus` to the `docker` group, plus tmux, jq, shellcheck, `libatomic` (Node 26 loads `libatomic.so.1`) `openssl3-libs` (Fedora 45 has only OpenSSL 4, and Stryker segfaults without OpenSSL 3), and the Playwright system libraries, and runs `loginctl enable-linger corvus`. Right after `usermod -aG docker` the shell lacks the group, so the script runs its docker checks through `sg docker -c`; no re-login is needed in the same run. It walks you through creating the 1Password service account and the GitHub token (below) and sets up commit signing (next paragraph). `--dry-run` prints what each stage would do, asks nothing and never waits for Enter; without a terminal on stdin the "Ready to start?" pause is skipped too.

   **Signing key.** Stage 7 never asks for a bare path. If `user.signingkey` is set and the file exists, it is used. Otherwise the script lists `~/.ssh/*.pub` as numbered choices plus "generate `~/.ssh/tethys_signing`" (`ssh-keygen -t ed25519 -N '' -f ~/.ssh/tethys_signing -C 'tethys signing'`), and sets `gpg.format=ssh`, `commit.gpgsign=true` and `user.signingkey` globally. For non-interactive use set `SIGNING_KEY=<path>`. It then compares the key with the public `https://api.github.com/users/Brijunas/ssh_signing_keys`. If the key isn't listed it tries `gh ssh-key add <pub> --type signing --title "tethys signing"`; the scoped token can't, so it prints the one-line command to run on any machine signed in to `gh` as the owner (`gh auth refresh -h github.com -s admin:ssh_signing_key` first), and checks again.

2. **`deploy/agent-host/agent-user.sh`**, as `corvus`. If the login lacks the `docker` group it runs its compose and docker steps through `sg docker -c`. It sets the main checkout's `origin` to `https://github.com/Brijunas/watt-wise.git` and runs `gh auth setup-git`, so pushes use the fine-grained token; it has no Workflows permission, so GitHub rejects changes under `.github/workflows` (commit signing stays SSH). It sets up the main checkout (`REPO_DIR`, default `~/code/personal/watt-wise`), fast-forwarding `main` first when the tree is clean, whichever checkout the script is run from. It installs mise and the tool versions, runs `pnpm install` and `dotnet tool restore`, and writes `~/.config/wattwise/env`, which exports only `OP_SERVICE_ACCOUNT_TOKEN` from the token file and is sourced from `~/.bashrc`. Then it renders `deploy/.env` with `op inject`, starts the Development stack and runs `migrate`.
3. **`deploy/agent-host/sync-user-claude.sh`**, from vidar. It makes the user-level config match, sets up the memory sync and the timer, and prints the parity check, which must show no differences.

## Credentials

What each credential can do, and the risk that remains, is in [security.md](../../docs/security.md#agents).

| Credential                | Where it lives on tethys                                             | Scope                                                                                     |
| ------------------------- | -------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| GitHub fine-grained token | `gh auth login --with-token`, in `gh`'s own store; `origin` is HTTPS | This repository only: Contents and Pull requests read/write; no Workflows, Admin, Secrets |
| 1Password service account | `~/.config/wattwise/op-token` (mode 600), exported by `env` above    | Read-only, `Watt Wise Development` vault only                                             |
| SSH signing key           | `corvus`'s `~/.ssh`, configured for `git` commit signing             | Signs commits on tethys only; registered on GitHub as a signing key                       |

## Rotating credentials

Rotate on suspicion, or when a credential nears its expiry. Nothing else on tethys needs to change.

1. **GitHub token.** Create a new fine-grained token with the same scope, run `gh auth login --with-token` as `corvus`, check `gh auth status`, then delete the old token on GitHub.
2. **1Password service account.** Create a new read-only service account on the `Watt Wise Development` vault, write its token to `~/.config/wattwise/op-token` (mode 600), check `op whoami` in a new shell, then delete the old service account.
3. **Signing key.** Generate a new key as `corvus`, point git's `user.signingkey` at it, register it on GitHub as a signing key, then remove the old key from GitHub. Commits signed with the old key stay verified.

## Watching a run

In Orca on vidar, open the tethys SSH target and a terminal in the epic worktree, then `tmux attach -t epic-<E>`. Detach with `Ctrl-b d`; the run continues. The orchestrator's questions appear in that session only.
