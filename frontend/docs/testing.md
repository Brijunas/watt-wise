# Frontend testing

How `frontend/`, `admin/` and the `@wattwise/*` packages are tested and where the tests live. The folders named here are described in [structure.md](structure.md); the commands are in [development.md](../../docs/development.md#running-a-single-test).

## Tools

- **Unit and component tests:** Vitest with React Testing Library, in every app and package. The shared Vitest config is under "Configuration" in [architecture.md](architecture.md#shared-packages).
- **API in tests:** MSW handlers stand in for the backend.
- **Property-based tests:** fast-check ([below](#property-based-tests)).
- **Accessibility:** vitest-axe ([below](#accessibility-tests)).
- **Mutation testing:** StrykerJS ([below](#mutation-testing)).
- **End to end:** Playwright, added once the core flow exists. Until then the story's `/smoke-check` drives the real system by hand.

## Where tests go

- Unit and component tests sit next to the file: `plan-card.test.tsx` beside `plan-card.tsx`.
- A route's integration test sits next to its route module in `app/routes/`.
- `testing/test-utils.tsx` renders through `AppProvider` from `app/provider.tsx` with a fresh store, so tests use the real provider tree instead of a copy. It takes `session` (`signedIn` or `signedOut`; omitted, the store starts `restoring`) and `language`; `renderRoutesAt(routes, path, { width, session, language })` renders the `routes` it is given (route tests import them from `../router`) in a memory router.
- MSW handlers live in `testing/mocks/handlers/`, one file per API resource, so features and routes reuse them. `testing/mocks/server.ts` is the `msw/node` server; `testing/setup.ts` (Vitest `setupFiles`) starts it with unhandled requests failing the test, resets handlers and runs RTL `cleanup` after each test. The API base URL is the fixed `https://api.test` from `testing/runtime-config.ts` ([architecture.md](architecture.md#runtime-config)), and the handlers build their URLs from `env.apiBaseUrl`.
- The deferred-answer and ProblemDetails helpers, `createGate` and `problemResponse`, come from `@wattwise/core/testing` (`msw` is a peer of core); each app's `testing/mocks/handlers/` still holds its own handlers.
- A test that depends on the screen width stubs `matchMedia` with `createMatchMedia({ width })` from `@wattwise/ui/testing` (in an app, through `stubMatchMedia({ width })` in `testing/match-media.ts`): 360 for a phone (`xs`), 900 for `md`. Layout that changes with the breakpoint therefore uses `useMediaQuery`, not CSS `display`, so jsdom renders only the variant under test.
- The locale key check is under "Translations" in [structure.md](structure.md#translations). `flattenKeys` from `@wattwise/i18n/testing` ignores the plural suffixes (`_zero`, `_one`, `_two`, `_few`, `_many`, `_other`), so Lithuanian's extra forms don't fail the parity check.
- Playwright end-to-end tests go in `<app>/e2e/` once they exist.

## Writing tests that catch bugs

- **Lowest layer that reaches the behaviour.** A pure rule gets a unit test, a component's behaviour a component test, a flow across screens a route test.
- **Both languages.** Every UI test runs in LT and EN. Expectations are literals written by hand, never values read from the locale files.
- **Query like a user.** `getByRole` and labels, `userEvent`. No snapshots; a test id is the last resort.
- **Breakpoints.** A layout that changes with the width is tested at 360 first, then at 900.
- **Mocks.** `vi.mock` of internal modules is allowed. Fake only the boundaries: MSW for the Api, `createGate` for races.
- **Races.** A late answer after navigation, unmount or sign-out changes nothing ([architecture.md](architecture.md#session)); each such path has a `createGate` test.
- **Errors.** Every mapped ProblemDetails error path shows its localized message.

## Property-based tests

fast-check covers a pure rule that must hold for all inputs: zod schemas against the backend's limits, formatters, plural selection, parsing. When a run fails, keep the failing seed as a plain regression example next to the property.

## Accessibility tests

Rules and tools are in [accessibility.md](accessibility.md). Each new page, and each `@wattwise/ui` component in every state that changes its markup, gets a `vitest-axe` test. `toHaveNoViolations` is registered once in the root `vitest.setup.js` (through `vitest.base.js`); each workspace holds its typings in `src/vitest-axe.d.ts`. jsdom can't check contrast.

## Mutation testing

StrykerJS (the Vitest runner) checks that the tests fail when the code changes. The `frontend-test-reviewer` agent runs it scoped to the changed files and treats a surviving mutant as a should-fix unless it is equivalent.

- Run `pnpm --filter <workspace> mutation [--mutate src/<file>.ts]`. The config is the root `stryker.config.json`: it mutates `src/**/*.{ts,tsx}` except tests, `testing/` files and `generated/`, so pass `--mutate` explicitly to mutate a `testing.ts`. Thresholds are 80 (high), 60 (low) and 50 (break). Reports go to `<workspace>/reports/mutation/mutation.html`.
- **Run it alone.** `inPlace: true` mutates the source files in place and restores them afterwards, so no other edit, build or test run may go on at the same time. After an aborted run check `git status` and restore the files it touched; a leftover `stryker-setup-*.js` is git-ignored.
