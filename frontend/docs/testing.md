# Frontend testing

How `frontend/`, `admin/` and the `@wattwise/*` packages are tested and where the tests live. The folders named here are described in [structure.md](structure.md); the commands are in [development.md](../../docs/development.md#running-a-single-test).

## Tools

- **Unit and component tests:** Vitest with React Testing Library, in every app and package. The shared Vitest config is under "Configuration" in [architecture.md](architecture.md#shared-packages).
- **API in tests:** MSW handlers stand in for the backend.
- **End to end:** Playwright, added once the core flow exists.

## Where tests go

- Unit and component tests sit next to the file: `plan-card.test.tsx` beside `plan-card.tsx`.
- A route's integration test sits next to its route module in `app/routes/`.
- `testing/test-utils.tsx` renders through `AppProvider` from `app/provider.tsx` with a fresh store, so tests use the real provider tree instead of a copy.
- MSW handlers live in `testing/mocks/handlers/`, one file per API resource, so features and routes reuse them.
- A test that depends on the screen width stubs `matchMedia` with `createMatchMedia({ width })` from `@wattwise/ui/testing` (in an app, through `stubMatchMedia({ width })` in `testing/match-media.ts`): 360 for a phone (`xs`), 900 for `md`. Layout that changes with the breakpoint therefore uses `useMediaQuery`, not CSS `display`, so jsdom renders only the variant under test.
- The locale key check is under "Translations" in [structure.md](structure.md#translations).
- Playwright end-to-end tests go in `<app>/e2e/` once they exist.
