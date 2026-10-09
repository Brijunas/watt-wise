# Frontend folder structure

How code is laid out inside `frontend/` and `admin/`. Both apps use the same structure: Bulletproof React's feature folders with the Redux Toolkit store conventions inside them. What lives in the shared `@wattwise/*` packages instead is under "Shared frontend packages" in [technical.md](technical.md#shared-frontend-packages).

## Layout

```
<app>/src/
├── main.tsx             entry point Vite's index.html loads; renders app/app.tsx
├── app/                 composition root: wires features together, nothing imports it
│   ├── app.tsx          root component
│   ├── provider.tsx     store, theme, i18n and router providers
│   ├── router.tsx       createBrowserRouter, lazy route modules
│   └── routes/          one module per route; composes features into pages
├── features/
│   └── <feature>/       one business area (plans, account, catalog-review…)
│       ├── api/         enhanceEndpoints, selectors, composed hooks over the generated client
│       ├── components/  the feature's UI
│       ├── hooks/
│       ├── model/       createSlice files for client state the feature owns
│       ├── schemas/     zod schemas for the feature's forms
│       ├── types/
│       └── utils/
├── components/          app-only shared UI (layouts, app shell pieces)
├── hooks/               app-only shared hooks
├── stores/              root reducer, store factory call, typed hooks
├── lib/                 configured app-level libraries
├── config/              app constants, typed import.meta.env access
├── types/               app-wide types not owned by one feature
├── utils/               app-wide pure helpers
├── assets/              app-wide static files
└── testing/             test-utils.tsx, mocks/ (MSW server, handlers/<resource>.ts)
```

A feature creates only the subfolders it needs. A subfolder that would hold one file isn't created; the file sits directly in the feature folder until a second one joins it.

## Import direction

Code flows one way: **shared → features → app**. "Shared folders" means every folder in `src/` except `app/` and `features/`.

| From            | May import                                       |
| --------------- | ------------------------------------------------ |
| `app/`          | everything in the app, `@wattwise/*`             |
| `features/<f>/` | its own files, the shared folders, `@wattwise/*` |
| shared folders  | other shared folders, `@wattwise/*`              |
| `@wattwise/*`   | other `@wattwise/*` packages only, never an app  |

- **No cross-feature imports.** `features/a` never imports `features/b`. When a page needs both, the route module in `app/routes/` composes them. When a feature's component needs another feature's behaviour, the route passes it in as a prop or slot.
- **Data shared between features goes through RTK Query**, not imports: a mutation's tags invalidate the other feature's queries.
- **Promote, don't reach across.** Code a second feature needs moves down to a shared folder; code the other app needs moves to a `@wattwise/*` package.
- ESLint enforces these rules (see "Enforcement").

## Where things go

| Thing                           | Place                                                                                                    |
| ------------------------------- | -------------------------------------------------------------------------------------------------------- |
| Generic or themed UI            | `@wattwise/ui`. Only layouts and pieces used by one app go in `<app>/src/components/`.                   |
| A page                          | A route module in `app/routes/` that renders feature components. Pages hold no business logic.           |
| A component used by one feature | `features/<f>/components/`                                                                               |
| Endpoints                       | Generated into `@wattwise/api-client`. Never call `createApi` in an app.                                 |
| Tags with IDs, selectors        | `features/<f>/api/`, using `enhanceEndpoints` and `endpoint.select()` on the generated API.              |
| Client-only state               | A `createSlice` file in `features/<f>/model/`, injected into the root reducer (see "Store").             |
| Form state                      | react-hook-form, never Redux. Schema in `features/<f>/schemas/`; shared zod helpers in `@wattwise/core`. |
| Translations                    | `@wattwise/i18n`, with keys namespaced per feature.                                                      |
| Env and constants               | `config/`                                                                                                |
| Static files                    | With the feature that uses them, or `assets/` when shared.                                               |

## Store

- `@wattwise/core` provides the store factory and the base query; `@wattwise/api-client` provides the single API slice. Each app owns its `RootState` and typed hooks because the two apps' state differs.
- `stores/` holds:
  - the root reducer, `combineSlices(api).withLazyLoadedSlices<LazyLoadedSlices>()`, with `RootState` derived from it;
  - the app's store function, which calls the `@wattwise/core` factory with that reducer, and the `AppStore` and `AppDispatch` types;
  - `useAppDispatch` and `useAppSelector`, made with `.withTypes()`.
- Features import the typed hooks from `stores/`, which keeps the one-way rule.
- A feature slice extends `LazyLoadedSlices` with `declare module` and injects itself with `slice.injectInto(rootReducer)`, so the root reducer never imports a feature.
- `app/provider.tsx` calls the store function once; it is the only place the app's store instance is created. Tests create a fresh store per test with the same function.
- Raw `useSelector` and `useDispatch` are never imported; use the typed hooks.

## Files and naming

- File and folder names are kebab-case: `plan-card.tsx`, `use-plan-filter.ts`, `plans-slice.ts`. Component names inside stay PascalCase.
- One component per file, named after the file.
- **No barrel files** (`index.ts` re-exports) inside an app. Import from the file itself. Each `@wattwise/*` package has one entry point.
- Imports inside an app use the `@/` alias for `src/` (`@/features/plans/components/plan-card`); relative imports only within the same feature folder.

## Tests

- Unit and component tests sit next to the file: `plan-card.test.tsx` beside `plan-card.tsx`.
- A route's integration test sits next to its route module in `app/routes/`.
- `testing/test-utils.tsx` provides a render with every provider and a fresh store.
- MSW handlers live in `testing/mocks/handlers/`, one file per API resource, so features and routes reuse them.
- Playwright end-to-end tests go in `<app>/e2e/` once they exist.

## Enforcement

The root `eslint.config.js` checks the import rules for both apps with `eslint-plugin-import-x` (`eslint-plugin-import` doesn't support ESLint 10):

- `import-x/no-restricted-paths` zones: shared folders can't import `features/` or `app/`, features can't import `app/`, and no feature can import another. The feature zones are generated from each app's `src/features/` listing, so a new feature is covered without editing the config.
- `import-x/no-cycle`.
- `no-restricted-imports` for `useSelector` and `useDispatch` from `react-redux`.

## Why this structure

Chosen in October 2026 over Feature-Sliced Design v2.1 and the plain Redux Style Guide layout:

- **Feature-Sliced Design** checks the architecture with its own linter (Steiger) and leaves the most room to grow, but its layer vocabulary (feature, entity, widget) costs more than an MVP with a thin admin app needs, it has no Redux guide, and it requires an `index.ts` barrel per slice.
- **The Redux layout** covers only state: it has no import boundaries and nothing on routes, forms, i18n or tests.
- **Bulletproof React** keeps the one-way rule with folder names a newcomer reads in minutes. Its known gap, shared UI that needs feature behaviour, is closed by construction here: `@wattwise/ui` can't import an app, so behaviour comes in through props or slots.

Moving to Feature-Sliced Design later stays cheap: `app/routes/` maps to its `pages`, `features/` to `features`, and the shared folders to `shared`.
