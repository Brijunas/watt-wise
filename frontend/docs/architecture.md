# Frontend architecture

The stack of `frontend/` and `admin/` and how the shared `@wattwise/*` packages are built and consumed. How code is laid out inside an app is in [structure.md](structure.md); how the frontend fits into the whole system is under "Frontend" in [technical.md](../../docs/technical.md#frontend).

## Stack

- **Stack:** React + TypeScript, built with Vite, delivered as a PWA.
- **PWA scope:** installable with a manifest; the app shell is cached by a service worker (`vite-plugin-pwa`). API data is always fetched online; no offline data and no push notifications in MVP.
- **Component library:** MUI.
- **Responsive design:** mobile-first. Layouts are built for the smallest MUI breakpoint first and enhanced upward; navigation, tables and charts must be usable on a phone.
- **Data fetching / server state:** RTK Query (Redux Toolkit).
- **Routing:** React Router 8 in data mode: `createBrowserRouter` with lazy page routes, `RouterProvider` from `react-router/dom` and everything else from `react-router`. The app shell (app bar, bottom navigation on a phone, side navigation from the `md` breakpoint up) and the not-found page come from `@wattwise/ui`; how they get their links is under "Import direction" in [structure.md](structure.md#import-direction).
- **Forms and validation:** react-hook-form with zod schemas.
- **Charts (consumption chart, catalog UI):** MUI X Charts.
- **API client:** generated from the backend OpenAPI document with `@rtk-query/codegen-openapi` into `packages/api-client`, consumed by both apps. Regenerated whenever the API changes; generated code is committed.
- **Tests:** Vitest with React Testing Library; see [testing.md](testing.md).

## Appearance

- **Modes:** light, dark and auto using MUI's CSS-variables theme with `colorSchemes` and `useColorScheme`. Auto follows `prefers-color-scheme`; the user's choice is stored in `localStorage` under `wattwise-mode` and applied before first paint to avoid a flash. The apps have no server rendering, so a Vite plugin from `@wattwise/ui/vite` writes MUI's own `InitColorSchemeScript` into each `index.html` `<head>` at build time.
- **Palette and font:** the brand palette lives in `packages/ui` (the theme): a blue-violet primary, a neutral slate secondary and tinted neutrals, every colour meeting WCAG AA (4.5:1) as text on both backgrounds, checked by the theme's tests. Green, red and amber are reserved for cheaper, pricier and caution, so they are never used as brand or decoration. The font is Roboto, bundled with the apps (`@fontsource-variable/roboto`), so no request goes to a font CDN.

## i18n

react-i18next with `i18next-browser-languagedetector`. The language is the one stored in `localStorage` (`wattwise-language`), else the first Lithuanian or English entry in the browser's language list, else Lithuanian; a key missing in Lithuanian falls back to English. The detected or chosen language is stored on every change, so after the first visit the stored one wins. The user switches it in the app bar. Keys are type-checked; how namespaces are laid out and registered is in [structure.md](structure.md#translations).

## Admin application

Same stack as `frontend/` (Vite PWA with the same manifest and app-shell caching, MUI, RTK Query, generated API client, React Router, react-hook-form with zod, react-i18next, Vitest), the same mobile-first layout rules and light/dark/auto appearance, and the same folder structure. It is served on its own subdomain. What it is for is under "Admin application" in [technical.md](../../docs/technical.md#admin-application).

## Shared packages

- Shared code lives in `packages/<name>` as private workspace packages (`"private": true`, name `@wattwise/<name>`), consumed by the apps as `"@wattwise/<name>": "workspace:*"`.
- **Consumed from source.** Each package's entry point is `src/index.ts`, exposed through `"exports": { ".": "./src/index.ts" }`; there is no build step and no `dist/`. Vite compiles shared code as part of each app build, HMR works across packages, and TypeScript sees live types. A package may add a second entry for code that runs at build time instead of in the browser: `@wattwise/ui/vite` (`src/vite.ts`) holds the Vite plugin the apps' `vite.config.ts` imports. Node loads such a file directly, so its relative imports carry the `.ts` extension (`allowImportingTsExtensions` in `tsconfig.base.json`).
- **Single React/MUI instance.** Packages declare `react`, `react-dom`, `@mui/material` and other framework libraries as `peerDependencies`; only the apps own those versions. A package adds a peer (plus a matching devDependency for its own tests) when its code first imports the library, not ahead of time.
- **Configuration.** One root `tsconfig.base.json` extended by every app and package. ESLint and Prettier each have one root config (`eslint.config.js`, `.prettierrc.json`) and run once from the root across all apps and packages ([conventions.md](conventions.md)). Vitest is a root devDependency and its config is shared through the root `vitest.base.js`; each app and package has a `vitest.config.js` that merges it with `mergeConfig(base, defineProject({ ... }))` and adds its own options (name, environment); an app also merges its `vite.config.ts`, for the React plugin and the `@/` alias. Every app and package has `test` (`vitest run`) and `typecheck` (`tsc`, TypeScript 7) scripts, fanned out by the root `pnpm test` and `pnpm typecheck`. TypeScript project references are added only if type-checking becomes slow.
- **Initial packages:** `api-client`, `ui`, `core`, `i18n`. New packages are created only when code is genuinely needed by more than one app. Which package may import which is under "Import direction" in [structure.md](structure.md#import-direction).
