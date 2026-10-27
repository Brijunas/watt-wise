---
name: update-deps
description: Refresh every Watt-Wise dependency to its latest version and prove the repo still works. Covers pnpm packages (catalog included), NuGet packages in backend/Directory.Packages.props, the .NET SDK pin (mise.toml, global.json, dotnet-ef in .config/dotnet-tools.json), the other mise tools, Docker image tags, and a recheck of docs/known-issues.md; then keeps doc references in sync and runs every build and test. Use this skill whenever the user says things like "update packages", "update deps", "bump dependencies", "check for outdated packages", "upgrade to latest", "new .NET RC is out", "refresh the lockfile", or asks whether a known-issues workaround can be dropped, even if they don't name the skill.
---

# Update dependencies

One run takes every dependency source in the repo to its newest version, majors included, fixes what breaks, and ends with all checks green and the changes left uncommitted for review. The policy behind it is "Tool versions" and "Dependency updates" in [technical.md](../../../docs/technical.md); the commands are in [development.md](../../../docs/development.md).

## Hard rules

These come from upstream problems or deliberate decisions, not taste. A "latest" bump never overrides them:

- **TypeScript stays split** while the TS 7 entry is open in [known-issues.md](../../../docs/known-issues.md): `typescript` is `npm:@typescript/typescript6@^6`, `@typescript/native` is `npm:typescript@^7`. `pnpm update --latest` rewrites these specifiers (to a pinned caret like `^7.0.2`, or the `typescript` alias to TS 7, which breaks ESLint), so restore both lines exactly as known-issues.md shows them.
- **PostgreSQL never gets a major bump** ([postgres.md](../../../deploy/docs/postgres.md#version)): a major needs a dump and restore. Newest minor of the current major only, same tag in `deploy/docker-compose.development.yml` and `backend/tests/WattWise.Testing/PostgresContainerFixture.cs`.
- **The .NET SDK version moves in `mise.toml` and `global.json` together**, so mise and the `dotnet` CLI never disagree ([backend/docs/conventions.md](../../../backend/docs/conventions.md)).
- **Nothing younger than one day.** pnpm enforces it through `minimumReleaseAge`; for NuGet, Docker and the SDK, the survey marks such releases `too new`. Take the newest version that is old enough instead. A compromised release is usually pulled within hours.
- **Never `dotnet ef database update`** and never edit a generated migration to make a bump compile. The `ProductVersion` annotations in `Persistence/Migrations/` record the EF build that generated them; leave them.

## 1. Preflight

- `git status --short` must be clean, otherwise ask: the report has to show only this run's changes.
- On `main`, `git pull --ff-only`, then `git switch -c deps-<YYYY-MM-DD>`.
- Docker must be running (`docker info`): the backend integration tests start PostgreSQL through Testcontainers. If it isn't, start it (or ask the user to) before going on, rather than finding out at the end.
- Read `docs/known-issues.md`. Every open entry is a constraint for this run, and step 6 rechecks each one.

## 2. Survey

```bash
python3 .claude/skills/update-deps/scripts/outdated.py
```

It is read-only and prints one section per source: mise tools, .NET SDK, pnpm (`pnpm outdated -r --long`, latest including majors), NuGet, Docker images. `MAJOR` flags a major bump; `too new` flags a release under one day old. Show the user the result as one short table before changing anything, so they see the scope of the run.

## 3. Apply the updates

Work through the sources in this order. The SDK goes first because the NuGet choices depend on it.

**.NET SDK and tools**

- Set the survey's SDK version in `mise.toml` (`dotnet = "<version>"`) and `global.json` (`sdk.version`), then `mise install`.
- At a GA release, follow "At .NET 11 GA" in [conventions.md](../../../backend/docs/conventions.md): `dotnet = "latest"` in mise.toml, the GA SDK and `"allowPrerelease": false` in global.json, and the comment in mise.toml removed.
- Set `dotnet-ef` in `.config/dotnet-tools.json` to the same build as the EF Core packages, then `dotnet tool restore`.
- Set every other tool in `.config/dotnet-tools.json` (e.g. `dotnet-stryker`) to its newest stable version that is old enough, as the survey shows, then `dotnet tool restore`.
- `mise upgrade` for the floating tools (node lts, pnpm, betterleaks). Nothing in the repo changes for those; they matter because the checks run with them.

**JS (pnpm)**

- `pnpm update -r --latest` updates the manifests, the catalog in `pnpm-workspace.yaml` and `pnpm-lock.yaml`.
- Check `git diff package.json` for the TypeScript aliases (hard rules) and restore them if rewritten, then `pnpm install`.

**NuGet** (`backend/Directory.Packages.props`)

- Edit the `PackageVersion` lines to the survey's versions. A package on a prerelease follows the newest prerelease or stable; a package on a stable stays on stable.
- Move related packages as one group, because they are built and tested against each other: all `Microsoft.*` packages of the .NET 11 line to the same build; EF Core and the Npgsql EF provider; OpenTelemetry; Serilog; the three `xunit.v3.*` packages; FluentValidation and its DI extensions.
- **The Npgsql EF provider decides the EF Core version.** It pins EF Core to one exact build (`[11.0.0-rc.1.…]` in its nuspec). If it hasn't shipped for the new EF build yet, keep `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` and `dotnet-ef` on the EF build the provider depends on (see its nuspec), and say so in the report. A newer EF next to an older provider gives NU1605/NU1608 or runtime `MissingMethodException`.

**Docker images**

- postgres: the survey's tag (same major), changed in both files named in the hard rules.
- pgAdmin and Grafana LGTM: the survey's tag in `deploy/docker-compose.development.yml`.

## 4. Fix what the majors broke

For every `MAJOR` the survey listed, before deciding it's done:

- Read the release notes or migration guide. Query Context7 first, then the project's GitHub releases.
- Apply the required changes to code and config, following the project's own docs (`backend/docs/`, `frontend/docs/`).
- If a major can't be made to work with a reasonable change, or it is blocked upstream (a plugin's peer range, a missing provider release), revert just that package to the newest version of its old major. If upstream blocks it, add an entry to `docs/known-issues.md` in the existing format; then the next run rechecks it.

## 5. Keep doc references in sync

Docs that describe the current state must name the versions now in use. Search for every old version string and old major you changed:

```bash
git grep -n -e '<old version>' -e '<Name> <old major>' -- '*.md' '*.toml' '*.json' '*.yml' '*.cs'
```

- Update current-state references: e.g. "React Router 8" in `CLAUDE.md`, the SDK version in `backend/docs/conventions.md`, the PostgreSQL tag in `deploy/docs/postgres.md`.
- Leave history alone: the `_Recorded …_` lines in known-issues.md, `docs/implemented.md`, and story text in `docs/epics.md`.
- If a bump changes a command or how a new machine is set up, update `docs/development.md` or `docs/setup.md` too.

## 6. Recheck known issues

For each open entry in `docs/known-issues.md`, run the check under "Where to follow it" (a `curl` or `npm view`).

- If the issue is resolved, follow its "Resolved when" steps and delete the entry.
- If it's still open, leave the entry, including its recorded versions, which are history.
- An entry with no automatic check (e.g. one waiting for real data) stays as is.

## 7. Verify

Run from the repo root, in this order, and fix failures before moving on:

```bash
pnpm install
pnpm lint
pnpm format:check
pnpm typecheck
pnpm test
pnpm build
dotnet tool restore
dotnet build backend/WattWise.slnx
dotnet format backend/WattWise.slnx --verify-no-changes
dotnet test --solution backend/WattWise.slnx
dotnet ef migrations has-pending-model-changes --project backend/src/WattWise.Infrastructure
```

- A new SDK or tool can start generating files the JS tools then pick up (SDK 11 rc.2 writes `.js` into `backend/**/obj/`, which ESLint flagged). Fix the ignore config (`eslint.config.js`, `.prettierignore`), not the generated file.
- The backend build must end with **0 warnings**. A new analyzer in an SDK or package bump often adds some; fix them in code rather than suppressing them.
- `has-pending-model-changes` must pass: a provider or EF bump must not change the model. If it does, stop and ask the user; a migration is a story decision, not part of an update.
- Run `dotnet test` in the background, because it can take a few minutes with the containers.

## 8. Report

Leave everything uncommitted on the branch. Don't commit or open a PR unless the user asks; ship-pr does that.

Report as:

| Source | Package | Old | New | Note |
| ------ | ------- | --- | --- | ---- |

- Mark majors and say what code changed for each.
- List what was **held back** and why (hard rule, too new, upstream blocker, provider lag).
- Give the known-issues outcome per entry, and the result of each check.
- Suggest one dedicated commit for all of it, manifests and `pnpm-lock.yaml` together, e.g. `Deps: refresh to latest (.NET 11 rc.2, xunit.v3 4)`.
