# Agent host (tethys)

tethys is a VM on saturn. Epic runs execute there as the user `corvus`, with the repo at `/home/corvus/code/personal/watt-wise`: the orchestrator in tmux, its Sonnet subagents in worktrees, and the epic's Development stack (PostgreSQL, Grafana LGTM, Mailpit) and Testcontainers in Docker. How a run works is in [agentic-delivery.md](../../docs/agentic-delivery.md); why the VM is the security boundary is in [security.md](../../docs/security.md#agents). The scripts are in `deploy/agent-host/`.

## Same Claude everywhere

The Claude configuration is identical on vidar and tethys. Only credentials differ.

- **Project config** (`.claude/`, both `settings.json`, `CLAUDE.md` files) comes from git. tethys has nothing of its own; `agent-user.sh` writes no Claude settings.
- **User-level config** is mirrored from vidar by `sync-user-claude.sh`, run on vidar. It copies `~/.claude/CLAUDE.md`, installs the same plugins and marketplaces, installs the Orca skills, and merges a fixed list of keys from vidar's `~/.claude/settings.json` into tethys's. It never touches the hooks and status line Orca installs per host, and never credentials. It ends with a parity check that diffs the normalized user config of both machines (CLAUDE.md, plugin list, skill list, merged keys) and exits non-zero on any difference. `start-epic.sh` runs the same check and warns.
- **Auto mode** (`permissions.defaultMode: auto`) is a user setting: project settings can't set it. It reaches every machine through the mirrored user settings above.
- **Memory** is synced both ways with `unison`, because Claude keys a project's memory folder by its path, which differs per machine and per checkout. On tethys one canonical folder, `~/.claude/wattwise-memory/`, is symlinked as `memory` into every watt-wise project key (the main checkout, the Orca worktrees, the epic worktree that `start-epic.sh` adds). Unison syncs vidar's memory folder with that canonical folder. A systemd user timer on vidar runs it every 15 minutes while vidar is awake, and `start-epic.sh` syncs once at start when vidar is reachable. On a conflict unison keeps both copies (`-copyonconflict`) and the next session that reads them merges them.

## First-time setup

1. **`deploy/agent-host/setup-tethys.sh`**, run as `corvus` on tethys in an interactive terminal (it needs a TTY for sudo and the token prompts). It installs Docker CE and adds `corvus` to the `docker` group, plus tmux, jq, shellcheck and the Playwright system libraries, and runs `loginctl enable-linger corvus`. It walks you through creating the 1Password service account and the GitHub token (below) and checks that the SSH signing key is registered on GitHub as a signing key.
2. **`deploy/agent-host/agent-user.sh`**, as `corvus`. It sets up the main checkout (`REPO_DIR`, default `~/code/personal/watt-wise`), fast-forwarding `main` first when the tree is clean, whichever checkout the script is run from. It installs mise and the tool versions, runs `pnpm install` and `dotnet tool restore`, and writes `~/.config/wattwise/env`, which exports only `OP_SERVICE_ACCOUNT_TOKEN` from the token file and is sourced from `~/.bashrc`. Then it renders `deploy/.env` with `op inject`, starts the Development stack and runs `migrate`.
3. **`deploy/agent-host/sync-user-claude.sh`**, from vidar. It makes the user-level config match, sets up the memory sync and the timer, and prints the parity check, which must show no differences.

## Credentials

What each credential can do, and the risk that remains, is in [security.md](../../docs/security.md#agents).

| Credential                | Where it lives on tethys                                          | Scope                                                                                     |
| ------------------------- | ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| GitHub fine-grained token | `gh auth login --with-token`, in `gh`'s own store                 | This repository only: Contents and Pull requests read/write; no Workflows, Admin, Secrets |
| 1Password service account | `~/.config/wattwise/op-token` (mode 600), exported by `env` above | Read-only, `Watt Wise Development` vault only                                             |
| SSH signing key           | `corvus`'s `~/.ssh`, configured for `git` commit signing          | Signs commits on tethys only; registered on GitHub as a signing key                       |

## Rotating credentials

Rotate on suspicion, or when a credential nears its expiry. Nothing else on tethys needs to change.

1. **GitHub token.** Create a new fine-grained token with the same scope, run `gh auth login --with-token` as `corvus`, check `gh auth status`, then delete the old token on GitHub.
2. **1Password service account.** Create a new read-only service account on the `Watt Wise Development` vault, write its token to `~/.config/wattwise/op-token` (mode 600), check `op whoami` in a new shell, then delete the old service account.
3. **Signing key.** Generate a new key as `corvus`, point git's `user.signingkey` at it, register it on GitHub as a signing key, then remove the old key from GitHub. Commits signed with the old key stay verified.

## Watching a run

In Orca on vidar, open the tethys SSH target and a terminal in the epic worktree, then `tmux attach -t epic-<E>`. Detach with `Ctrl-b d`; the run continues. The orchestrator's questions appear in that session only.
