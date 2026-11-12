---
paths:
  - 'src/**/*.test.{ts,tsx}'
  - 'src/testing/**'
---

# Tests

- A test sits next to the file it tests; a route's integration test sits next to its route module in `app/routes/` (route tests go by route module).
- Render through `testing/test-utils` / `AppProvider` with a fresh store per test, never a copy of the provider tree.
- MSW handlers live in `testing/mocks/handlers/`, one file per API resource. Use `createGate` for races and `problemResponse` for errors (both from `@wattwise/core/testing`).
- Width-dependent tests call `stubMatchMedia`: 360 for a phone, 900 for `md`.
- Every UI test runs in LT and EN, with hand-written literal expectations, not values read from the locale files.
- Query like a user: `getByRole`, labels, `userEvent`. No snapshots; a test id is the last resort.
- `vi.mock` of internal modules is allowed. Use fast-check for pure rules that must hold for all inputs.
- Every new page and shared component gets a `vitest-axe` test.

The full rules are in [testing.md](../../docs/testing.md).
