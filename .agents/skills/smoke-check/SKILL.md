---
name: smoke-check
description: Whole-system manual end-to-end check of a story through the browser, with the real Api, Jobs host, database and app dev servers. Use before merging a story's PR when a coordinator orchestrator exists (the coordinator runs it in the story worktree), otherwise before ship-pr (the story orchestrator runs it). Input is the story's flows from its Done criteria.
---

# Smoke check

Drive the story's flows in a real browser with the `playwright` MCP server (browser_navigate, browser_snapshot, browser_resize, browser_console_messages, browser_network_requests, browser_take_screenshot) against the running system. The coordinator runs this in the story's worktree after the story's QUEUED OP STEPS; without a coordinator the story orchestrator runs it before ship-pr. Commands are in [docs/development.md](../../../docs/development.md); Mailpit is in [deploy/docs/mailpit.md](../../../deploy/docs/mailpit.md); the account vocabulary is in [accounts.md](../../../backend/docs/features/accounts.md).

Tell the user before each step that runs `op`: they approve it in the 1Password app.

## 1. Setup

1. Start the Development stack (the `docker compose ... up -d --wait` command).
2. `migrate`. After a regenerated `Initial`, run the `reset-app-tables.sql` command first.
3. Seed the fixed accounts: `op run --env-file backend/src/WattWise.Cli/.env.development -- dotnet run --project backend/src/WattWise.Cli -- seed smoke-accounts`. It is idempotent: both accounts exist, confirmed, unlocked, with the 1Password password and the right role.
4. Start the Api, the Jobs host (only when a flow sends email) and the dev servers the flows need (`frontend`, `admin`), each through `op run` in the background.
5. Read URLs with `op read "op://Watt Wise Development/<item>/url"`.

## 2. Accounts

- Fixed accounts (a `permissions.allow` rule `Bash(op read "op://Watt Wise Development/smoke-accounts/*")` in all five `settings.json` files allows exactly these reads; any other `op read` of a secret stays denied): `op read "op://Watt Wise Development/smoke-accounts/<user-email|user-password|admin-email|admin-password>"`.
- Flows that need a new account (sign-up, confirmation) use a fresh `smoke-<timestamp>@example.test`. Take the email links from Mailpit's API: `<mailpit>/api/v1/search?query=to:<email>`, then `/api/v1/message/<ID>`.
- **Rerun the seed** after any flow that changes a fixed account: password change or reset, lockout, sign-out-everywhere, admin revoke.

## 3. Check each flow

Mobile first: the Playwright `browser_resize` tool to 360px wide, then 900px, then desktop. At each width, each new screen in LT and EN. A flow passes at a width and language when:

- the flow completes as its Done criteria say;
- no raw translation keys show and no English is left in LT;
- `browser_console_messages` shows no errors;
- `browser_network_requests` shows no unexpected 4xx or 5xx (expected ones, such as a 423 on lockout, are named in the report).
- The one expected console error is the 401 from `/api/v1/auth/refresh` that each app makes on start without a cookie ("Failed to load resource ... 401"); it is also expected in the network check. Any other console error fails the flow.
- Each app keeps its own language choice (per origin): switch the language in each app separately.

Save screenshots as `.playwright-mcp/smoke/<flow>-<width>-<lang>.png` (the Playwright MCP writes only inside the repo; `.playwright-mcp/` is git-ignored).

## 4. Report

A table flow x width x language -> pass or fail, with the failing detail (screen, text, request, console line). Bugs go back to whoever owns the story; the check is done when every cell is pass or has a named bug.

## 5. Clean up

Stop every process this check started and close the browser: `pkill -P` on the recorded `op run` PIDs, then on the dotnet and vite children. Check that the ports are closed (the Api and the app dev servers; read them from the items' URLs, e.g. 5062, 5173, 5174). Leave the Development stack running.
