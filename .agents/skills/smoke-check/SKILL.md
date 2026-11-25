---
name: smoke-check
description: Whole-system manual end-to-end check of a story through the browser, with the real Api, Jobs host, database and app dev servers. Use before merging a story's PR when a coordinator orchestrator exists (the coordinator runs it in the story worktree), otherwise before ship-pr (the story orchestrator runs it). Input is the story's flows from its Done criteria.
---

# Smoke check

Drive the story's flows in a real browser with the `playwright` MCP server (browser_navigate, browser_snapshot, browser_resize, browser_console_messages, browser_network_requests, browser_take_screenshot) against the running system. The coordinator runs this in the story's worktree after the story's QUEUED OP STEPS; without a coordinator the story orchestrator runs it before ship-pr. Commands are in [docs/development.md](../../../docs/development.md); Mailpit is in [deploy/docs/mailpit.md](../../../deploy/docs/mailpit.md); the account vocabulary is in [accounts.md](../../../backend/docs/features/accounts.md).

Tell the user before each step that runs `op`: they approve it in the 1Password app. Inside a coordinator window nobody is prompted: run each documented `op run --env-file <f> -- <cmd>` as `set -a; . $XDG_RUNTIME_DIR/agentic-window/<name>.env; set +a; <cmd>`, with `<name>` the file for `<f>` (`api`, `jobs`, `cli`, `frontend`, `admin`; window commands in `~/.config/git/delegated-signing.md`), and read the URLs and the smoke-account logins from those files.

## 1. Setup

1. Start the containerized Development stack through `op run` (the `op run --env-file deploy/development/compose.env -- docker compose ... up -d --wait` command in [development.md](../../../docs/development.md#running-the-system)). It builds and starts PostgreSQL, LGTM, Mailpit, the Api, the Jobs host, the frontend and the admin; nothing else has to be started. Run the `up` from this worktree: it replaces another worktree's app containers.
2. That `up` also migrated (the stack's one-shot `cli`). After a regenerated `Initial`, run the `reset-app-tables.sql` command first, then the `up`.
3. That `up` also seeded the fixed accounts: both exist, confirmed, unlocked, with the 1Password password and the right role. To reset them later, run `seed smoke-accounts` with the host-run command (`op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- seed smoke-accounts`) or another `up`; it is idempotent.
4. The Api, the Jobs host and the dev servers run in the stack: check that `docker compose -f deploy/docker-compose.development.yml ps` shows them healthy. Host-run commands ([development.md](../../../docs/development.md#running-on-the-host)) are only a fallback, after `stop`ping the same service in the stack. The Hangfire dashboard answers 401 in the container, which isn't a failure.
5. Read URLs with `op read "op://Watt Wise Development/<item>/url"`, or in a window from the variables of the matching file.
6. Before the walk, on each app's origin unregister stale service workers and clear caches, localStorage and cookies (`browser_evaluate`): a previous run's app can be served from the old service worker.

## 2. Accounts

- Fixed accounts (a `permissions.allow` rule `Bash(op read "op://Watt Wise Development/smoke-accounts/*")` in all five `settings.json` files allows exactly these reads; any other `op read` of a secret stays denied): `op read "op://Watt Wise Development/smoke-accounts/<user-email|user-password|admin-email|admin-password>"`. Inside a window, read them from the `cli` file instead (`SmokeAccounts__UserEmail`, `SmokeAccounts__UserPassword`, `SmokeAccounts__AdminEmail`, `SmokeAccounts__AdminPassword`), with no `op read`.
- Flows that need a new account (sign-up, confirmation) use a fresh `smoke-<timestamp>@example.test`. Take the email links from Mailpit's API: `<mailpit>/api/v1/search?query=to:<email>`, then `/api/v1/message/<ID>`.
- **Rerun the seed** (host-run command or another `up`) after any flow that changes a fixed account: password change or reset, lockout, sign-out-everywhere, admin revoke.

## 3. Check each flow

Mobile first: the Playwright `browser_resize` tool to 360px wide, then 900px, then desktop. At each width, each new screen in LT and EN. A flow passes at a width and language when:

- the flow completes as its Done criteria say;
- no raw translation keys show and no English is left in LT;
- `browser_console_messages` shows no errors;
- `browser_network_requests` shows no unexpected 4xx or 5xx (expected ones, such as a 423 on lockout, are named in the report).
- The one expected console error is the 401 from `/api/v1/auth/refresh` that each app makes on start without a cookie ("Failed to load resource ... 401"); it is also expected in the network check. Any other console error fails the flow.
- Each app keeps its own language choice (per origin): switch the language in each app separately.

- The Playwright MCP browser reports borders and outlines at 0.75x their CSS px (a 1px border reads 0.75px): judge widths by ratio.
- A theme or layout story also measures computed styles with `browser_evaluate` at three widths (for example 360, 900 and 1280), because jsdom can't see the cascade.

Save screenshots as `.playwright-mcp/smoke/<flow>-<width>-<lang>.png` (the Playwright MCP writes only inside the repo; `.playwright-mcp/` is git-ignored).

## 4. Report

A table flow x width x language -> pass or fail, with the failing detail (screen, text, request, console line). Bugs go back to whoever owns the story; the check is done when every cell is pass or has a named bug.

## 5. Clean up

Close the browser and stop only what this check started. Leave the containerized stack running: it is the machine's shared stack, and the next story's `up` replaces its app containers anyway. If the check ran a host-run fallback, stop those processes by PID or port (`pkill -P` on the recorded `op run` PIDs, then on the dotnet and vite children; never a broad `pkill -f`, which can kill your own shell), check that their ports are closed (read them from the items' URLs), and `docker compose ... up -d` the stopped service again through `op run`.
