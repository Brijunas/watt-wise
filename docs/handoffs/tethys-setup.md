# Handoff: set up tethys

Temporary. Read by the Claude agent in the Orca worktree `tethys-watt-wise-setup` on tethys (`/home/corvus/orca/workspaces/watt-wise/featherduster`). Deleted when the experiments handoff ([tethys-experiments.md](tethys-experiments.md)) is done. Background: [deploy/docs/agent-host.md](../../deploy/docs/agent-host.md), [agentic-delivery.md](../agentic-delivery.md).

**This agent only sets up and arranges tethys. It never tests, implements or runs epics or stories here. Do not start `start-epic.sh`, `/plan-epic` or `/run-epic` in this worktree.**

## How to work

- Tick a box only after its verification command gave the expected result. Paste nothing secret into this session or into this doc.
- **Agent** steps you run yourself. **Human** steps need a terminal prompt (sudo password, pasted token) or a browser; ask the human to run them, either in your terminal or over their own SSH session as `corvus`, then you run the verification. `setup-tethys.sh` and `agent-user.sh` both run as `corvus` on tethys, in an interactive terminal (sudo and the token prompts need a TTY).
- Commands run from the repo checkout you are in unless a step says otherwise; `agent-user.sh` acts on the main checkout whichever you run it from. Scripts are safe to rerun: every stage checks first.

## Steps

### 1. [ ] Update the checkout (agent)

```bash
git fetch origin && git switch main && git pull --ff-only
```

If git says `main` is checked out in another worktree, use `git switch --detach origin/main` instead. Verify:

```bash
git log -1 --oneline && ls deploy/agent-host docs/agentic-delivery.md
```

Expected: the latest main commit (it contains the epic-run PR), and the six `deploy/agent-host` files plus `docs/agentic-delivery.md` listed.

Also check the prerequisites: `claude --version`, `orca --version`, `mise --version` and `ssh vidar true` (or whatever name reaches vidar). Note any that fail under the step; setup does not install them.

### 2. [ ] Dry run of the host setup (agent)

```bash
deploy/agent-host/setup-tethys.sh --dry-run
```

Verify: exits 0, prints what each of the 7 stages would do, changes and asks nothing. Read it; it shows which stages are already done.

### 3. [ ] Host setup, packages, Docker, 1Password, GitHub, signing (human, agent verifies)

Human runs, as `corvus`, in an interactive terminal:

```bash
deploy/agent-host/setup-tethys.sh
```

Stages and what the human does:

1. Preflight and packages (tmux, jq, ShellCheck, unison, gh, Playwright libraries): enter the sudo password.
2. Docker CE, the `corvus` docker group, `loginctl enable-linger corvus`: sudo password.
3. **1Password service account:** the script opens the page. Create a service account `Watt Wise Development` with **Read** access to the `Watt Wise Development` vault only; paste the `ops_` token when asked (hidden). It is saved to `~/.config/wattwise/op-token`, mode 600.
4. **GitHub fine-grained token:** create it for `Brijunas/watt-wise` only (Contents and Pull requests read/write, Metadata read; no Workflows, Administration, Secrets), 90 days or less; paste the `github_pat_` token (hidden).
5. **Signing key:** if none is configured, the script offers `ssh-keygen -t ed25519 -f ~/.ssh/tethys_signing -C 'tethys signing'` and sets `gpg.format ssh`, `commit.gpgsign true`, `user.signingkey`. The human **registers the public key on GitHub as a Signing Key** (not Authentication) at https://github.com/settings/ssh/new.

When it finishes, **log out and back in** (or `newgrp docker`) so the docker group applies. Verify, in a new shell:

```bash
docker info >/dev/null && echo docker-ok
loginctl show-user corvus -p Linger --value
stat -c %a ~/.config/wattwise/op-token
OP_SERVICE_ACCOUNT_TOKEN="$(cat ~/.config/wattwise/op-token)" op whoami | head -3
env -u GH_TOKEN -u GITHUB_TOKEN gh api repos/Brijunas/watt-wise --jq .full_name
git config --global --get-regexp '^(gpg.format|commit.gpgsign|user.signingkey)$'
deploy/agent-host/setup-tethys.sh --dry-run | grep -c '✓'
```

Expected: `docker-ok`; `yes`; `600`; a service account `op whoami` (not a user); `Brijunas/watt-wise`; `gpg.format ssh`, `commit.gpgsign true` and a `user.signingkey`; every stage reports checks passed in the dry run (no `✗`, no `⚠`). Ask the human to confirm in 1Password that the service account is read-only (the CLI cannot show it).

Sub-checks:

- [ ] Packages and Docker
- [ ] Linger
- [ ] 1Password service account
- [ ] GitHub token (the script warns if it sees other repositories; recreate it if so)
- [ ] Signing key registered as a signing key

### 4. [ ] Agent user: tools, env file, Development stack (agent)

Needs the docker group active in this shell (step 3).

```bash
deploy/agent-host/agent-user.sh --dry-run
deploy/agent-host/agent-user.sh
```

It installs mise and the tool versions, runs `pnpm install --frozen-lockfile` and `dotnet tool restore`, writes `~/.config/wattwise/env` (exports only `OP_SERVICE_ACCOUNT_TOKEN`) and sources it from `~/.bashrc`, renders `deploy/.env`, starts the Development stack (PostgreSQL, pgAdmin, Grafana LGTM, Mailpit), applies `bootstrap.sql`, sets the five role passwords from 1Password (first time only) and runs `migrate`. It always works on the main checkout `REPO_DIR` (default `~/code/personal/watt-wise`; override with the env var or `--repo`), not the checkout holding the script, so `deploy/.env` and the Dev stack belong to the permanent main checkout, not a worktree. It first runs `git pull --ff-only` there if the tree is clean and warns otherwise. Verify in a new shell:

```bash
test "$(stat -c %a ~/.config/wattwise/env)" = 600 && grep -c wattwise ~/.bashrc
op whoami | head -3
docker compose -f deploy/docker-compose.development.yml ps --format '{{.Service}} {{.Health}}'
mise exec -- dotnet --version && mise exec -- pnpm --version
```

Expected: `1` (or more); a service account; every service `healthy`; both versions print. If `migrate` failed, the script stops with the error: write it under the step.

### 5. [ ] Parity with vidar: `sync-user-claude.sh` (human on vidar, or agent over ssh)

**This script must run on vidar**, not on tethys: it reads vidar's `~/.claude` and pushes it to tethys over ssh. Ask the human to run it on vidar from the Watt-Wise checkout, or run it yourself if `ssh vidar` works from tethys and vidar can reach tethys by the host name `tethys`:

```bash
deploy/agent-host/sync-user-claude.sh --dry-run
deploy/agent-host/sync-user-claude.sh
deploy/agent-host/sync-user-claude.sh --check
```

It copies `~/.claude/CLAUDE.md`, adds the marketplaces and plugins, installs the Orca skills, merges a fixed list of `settings.json` keys (including `permissions.defaultMode`) on tethys after backing it up, and links tethys's canonical `~/.claude/wattwise-memory/` as `memory` into every Watt-Wise project folder. It never touches hooks, the status line or credentials. Verify with the last command (`--check`), run on vidar:

Expected: exit code 0 and a report with no differences (CLAUDE.md hash, marketplaces, plugins, skills and merged keys match). Any difference exits 1: copy the printed diff under this step and stop. Then, on tethys:

```bash
ls -ld ~/.claude/wattwise-memory ~/.claude/projects/*watt-wise*/memory
jq -r .permissions.defaultMode ~/.claude/settings.json
```

Expected: the canonical folder exists and each project `memory` is a symlink to it. The second command prints the user-level `defaultMode`; it must be `auto`, the same as on vidar (experiment 3 depends on it).

### 6. [ ] Memory sync timer on vidar (human)

The first sync and the timer both run on vidar, with `unison` installed on both machines in the same version (`unison -version` on each). Human runs on vidar:

```bash
mkdir -p ~/.config/systemd/user
cp deploy/agent-host/wattwise-memory-sync.{service,timer} ~/.config/systemd/user/
# the unit runs %h/code/personal/watt-wise/deploy/agent-host/sync-memory.sh; edit ExecStart if the checkout is elsewhere
systemctl --user daemon-reload
systemctl --user enable --now wattwise-memory-sync.timer
```

Verify on vidar:

```bash
systemctl --user list-timers wattwise-memory-sync.timer
```

Expected: the timer is listed with a next run (every 15 minutes, 2 minutes after boot).

### 7. [ ] First memory sync (human on vidar, agent verifies)

On vidar:

```bash
deploy/agent-host/sync-memory.sh --dry-run
deploy/agent-host/sync-memory.sh
```

The script exits 0 silently when tethys is unreachable, so check the result rather than the exit code. Verify on tethys:

```bash
ls ~/.claude/wattwise-memory | head
test -f ~/.claude/wattwise-memory/MEMORY.md && echo memory-index-present
```

Expected: the memory files that exist on vidar (for example `github-via-gh-cli.md`) and `memory-index-present`. Then make a harmless test: on vidar `touch` a file `~/.claude/projects/-home-caleb-code-personal-watt-wise/memory/_sync-probe.md`, run `systemctl --user start wattwise-memory-sync.service`, check that it appears on tethys, and delete it on vidar and run the service again so the deletion syncs back. Conflicts, if any, appear as `*.conflict.md`; list them under this step.

### 8. [ ] Finish (agent)

All boxes above ticked. Commit the ticked doc and push it; no PR yet (the experiments come first, and their branch ships the cleanup).

```bash
git switch -c chore/tethys-setup-done
git add docs/handoffs/tethys-setup.md
git commit -S -m "Tick the tethys setup handoff"
git push -u origin chore/tethys-setup-done
```

Verify: `git log -1 --show-signature` shows a good signature, and `git ls-remote --heads origin chore/tethys-setup-done` lists the branch. The pre-commit hook runs; do not use `--no-verify`. Tell the human that setup is done and that the experiments handoff starts with `.claude/scripts/start-epic.sh X0` in a new worktree.

## If something fails

Stop. Write what failed under that step: the command, the exit code and the relevant output with secrets removed. Do not improvise outside setup: do not edit scripts, `.claude/` or settings, do not install other tools, and do not work around a missing credential. Tell the human which step stopped; they decide whether to fix the script on vidar or the host here.
