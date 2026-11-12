# CLAUDE.md (frontend)

Notes for Claude Code when working in `frontend/`, `admin/` or `packages/`. Both apps and the `@wattwise/*` packages share one set of docs in [docs/](docs/): read [docs/structure.md](docs/structure.md) and [docs/architecture.md](docs/architecture.md) before writing code, and [docs/testing.md](docs/testing.md) before writing tests.

## TypeScript files

- Follow [docs/conventions.md](docs/conventions.md#files-and-naming): kebab-case file names, one component per file, no barrel files inside an app, the `@/` alias across folders, and arrow functions with a default export for single-function files.
- Put each thing where "Where things go" in [docs/structure.md](docs/structure.md#where-things-go) says. When the import rules block an import, move the code down (shared folder or package) instead of working around the rule.
- No literal user-facing text in a component: see "Translations" in [docs/structure.md](docs/structure.md#translations).
- Never edit `packages/api-client/src/generated/` by hand: `pnpm generate:api` writes it from the Api's OpenAPI document ([docs/architecture.md](docs/architecture.md#api-client)).

## Async UI code

The session rules are under [Session](docs/architecture.md#session); check each of these when code awaits something.

- After every await that changes the session, check the session revision is unchanged before applying the answer.
- Check the component is still mounted before `setState` or a refetch after an await.
- Show a loader, not the sign-in form or a redirect, while the session is `restoring`.
- Tests use deferred responses (`createGate`): a late answer after navigation, unmount or sign-out changes nothing.
- RTK Query's `refetchOnMountOrArgChange` attaches to a request already pending for the same args instead of starting a new one: decide freshness by `requestId`, not by comparing `fulfilledTimeStamp` with `Date.now()`.

## Before committing

Run the JS [checks](../docs/development.md#checks) (`pnpm lint`, `pnpm format:check`, `pnpm typecheck`, `pnpm test`, `pnpm build`); all must pass with no warnings.
