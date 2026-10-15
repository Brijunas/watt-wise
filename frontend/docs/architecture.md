# Frontend architecture

The stack of `frontend/` and `admin/` and how the shared `@wattwise/*` packages are built and consumed. How code is laid out inside an app is in [structure.md](structure.md); how the frontend fits into the whole system is under "Frontend" in [technical.md](../../docs/technical.md#frontend).

## Stack

- **Stack:** React + TypeScript, built with Vite, delivered as a PWA.
- **PWA scope:** installable with a manifest; the app shell is cached by a service worker (`vite-plugin-pwa`). API data is always fetched online; no offline data and no push notifications in MVP. How it is set up is under [PWA](#pwa).
- **Component library:** MUI.
- **Responsive design:** mobile-first. Layouts are built for the smallest MUI breakpoint first and enhanced upward; navigation, tables and charts must be usable on a phone.
- **Data fetching / server state:** RTK Query (Redux Toolkit).
- **Routing:** React Router 8 in data mode: `createBrowserRouter` with lazy page routes, `RouterProvider` from `react-router/dom` and everything else from `react-router`. The app shell (app bar, bottom navigation on a phone, side navigation from the `md` breakpoint up) and the not-found page come from `@wattwise/ui`; how they get their links is under "Import direction" in [structure.md](structure.md#import-direction).
- **Forms and validation:** react-hook-form with zod schemas; see [Forms](#forms).
- **Charts (consumption chart, catalog UI):** MUI X Charts.
- **API client:** generated from the backend OpenAPI document with `@rtk-query/codegen-openapi` into `packages/api-client`, consumed by both apps; how it is generated is under [API client](#api-client).
- **Tests:** Vitest with React Testing Library; see [testing.md](testing.md).

## Appearance

- **Modes:** light, dark and auto using MUI's CSS-variables theme with `colorSchemes` and `useColorScheme`. Auto follows `prefers-color-scheme`; the user's choice is stored in `localStorage` under `wattwise-mode` and applied before first paint to avoid a flash. The apps have no server rendering, so a Vite plugin from `@wattwise/ui/vite` writes MUI's own `InitColorSchemeScript` into each `index.html` `<head>` at build time.
- **Palette and font:** the brand palette lives in `packages/ui` (the theme): a blue-violet primary, a neutral slate secondary and tinted neutrals, every colour meeting WCAG AA (4.5:1) as text on both backgrounds, checked by the theme's tests. Green, red and amber are reserved for cheaper, pricier and caution, so they are never used as brand or decoration. The font is Roboto, bundled with the apps (`@fontsource-variable/roboto`), so no request goes to a font CDN.

## i18n

react-i18next with `i18next-browser-languagedetector`. The language is the one stored in `localStorage` (`wattwise-language`), else the first Lithuanian or English entry in the browser's language list, else Lithuanian; a key missing in Lithuanian falls back to English. The detected or chosen language is stored on every change, so after the first visit the stored one wins. The user switches it in the app bar. Keys are type-checked; how namespaces are laid out and registered is in [structure.md](structure.md#translations).

## API client

- **Source document.** `dotnet build` of the Api writes its OpenAPI document to the committed `backend/openapi/wattwise-api.json` (`Microsoft.Extensions.ApiDescription.Server`, [backend architecture](../../backend/docs/architecture.md#http-surface)). Codegen reads that file, so regenerating needs no running API, database or 1Password, and gives the same output on every machine.
- **Pipeline.** `pnpm generate:api` builds the Api, then runs the package's `generate` script: `@rtk-query/codegen-openapi` with `packages/api-client/openapi-config.json` writes `src/generated/api.ts`, and Prettier formats it. The generated file is committed and never edited by hand; ESLint skips it, `tsc` checks it. Run it after any change to an endpoint and commit the document and the client together. Regenerating without an API change produces no diff.
- **Layers inside the package.** `empty-api.ts` is the one `createApi` call (`reducerPath: 'api'`, the base query from `@wattwise/core`, no endpoints); the codegen injects the endpoints into it. `api.ts` enhances the generated API with the cache tags ([structure.md](structure.md#where-things-go)) and is the only file that imports `generated/`; `index.ts` exports the enhanced `api`, its hooks and the response types. Apps import only `@wattwise/api-client`.
- **Base URL.** The base query reads the URL from the store's thunk extra argument, which the store factory in `@wattwise/core` sets from the app's `config/env.ts` ([Runtime config](#runtime-config)); see "Store" in [structure.md](structure.md#store).

## Runtime config

- **The file.** `index.html` loads `/config.js` with a plain `<script>` before the app's module, and it sets `window.__WATTWISE_CONFIG__ = {"apiBaseUrl": "…"}`. Each app's `config/env.ts` reads it with `readRuntimeConfig()` from `@wattwise/core`, which stops the app with a clear error unless the value is an `https` origin (or `http` on a loopback host) with no path, query, hash or credentials. Nothing in a build holds the URL ([technical.md](../../docs/technical.md#frontend)).
- **Who serves it.** In a deployed image nginx renders it at container start from the container's `API_BASE_URL` (S5.2), and the apps' Content-Security-Policy takes the API origin from the same variable (S6.2). On the dev server and `vite preview` the `runtimeConfig()` plugin from `@wattwise/core/vite` answers `/config.js` from the process environment; without `API_BASE_URL` it returns a script that throws and says how to start the server. It is served `no-cache`. Tests set it in `testing/runtime-config.ts`, the first Vitest setup file, so it exists before any module that reads it is imported.
- **Development.** Each app has a committed `.env.op.development` holding only 1Password references: `API_BASE_URL` (the `api` item's `url`) and `APP_URL` (the app's own `frontend` or `admin` item's `url`, the origin the Api's CORS allows). Its name keeps Vite from loading it by itself. Start the server through `op run --env-file <app>/.env.op.development`; `vite.config.ts` takes the dev and preview host and port from `APP_URL` through `serverAddress()` from `@wattwise/core/vite` (the scheme's default port when the URL has none), with `strictPort`, so the app never moves to a port the Api would refuse. Without `APP_URL` Vite's defaults apply. `.env.example` lists both variables.
- **Service worker.** `/config.js` is never precached; a `NetworkFirst` rule serves it, so an online start always gets the current file and an offline start uses the last one.

## PWA

- **Plugin.** `VitePWA` in each app's `vite.config.ts` generates the service worker (Workbox) and `manifest.webmanifest` at build time. There is no service worker on the dev server; try it on a build served by `vite preview`.
- **Caching.** The build's app shell (JS, CSS, HTML, icons, the bundled font) is precached, and navigations fall back to `index.html` for the router. The API is on its own subdomain and is never cached: a `NetworkOnly` rule matches `/api/` on any origin, and `/api/` is excluded from the navigation fallback.
- **Updates.** `registerType: 'prompt'`: a new service worker waits until the user accepts. Each app's `components/pwa-update-prompt.tsx` reads `useRegisterSW` from `virtual:pwa-register/react` and shows `UpdatePrompt` from `@wattwise/ui` (text in the `shell` namespace); "Reload" activates the new worker and reloads. Tests mock the virtual module.
- **Manifest and icons.** Each app has its own name, colours and icon: a white bolt on a rounded square, the brand primary for `frontend` and the slate secondary for `admin`, so the two installed apps are told apart. The PNG sizes, maskable and Apple touch icons and `favicon.ico` are generated by `@vite-pwa/assets-generator` (`<app>/pwa-assets.config.ts`) from `<app>/public/favicon.svg`, which is also the SVG favicon, and committed. To change the icon, edit that SVG and run `pnpm --filter <app> generate:icons`.

## Forms

- **Pieces.** A form is react-hook-form with `zodResolver` over a zod schema in `features/<f>/schemas/` ([structure.md](structure.md#where-things-go)). Shared field rules are helpers in `@wattwise/core` (`requiredText()`, `email()`, `password()`); the fields are `FormTextField` and `FormPasswordField` in `@wattwise/ui`, MUI text fields bound with `useController` that show the field's error under it.
- **Messages are keys, not text.** A schema never holds a message. Each issue's message is an encoded key of the shared `validation` namespace in `@wattwise/i18n`, with its parameters (`encodeValidationMessage('password.tooShort', { min: 14 })`); the field decodes it with `decodeValidationMessage` and translates it. So schemas stay language-free, and an error already on screen switches language with the UI. A message that isn't an encoded key, such as zod's own English text from a rule written without one, shows the generic `invalid` text, so a schema rule always passes its key.
- **Password rules** follow [security.md](../../docs/security.md#accounts) and the server's ASP.NET Core Identity checks: the minimum length (`passwordMinLength`) and Unicode-aware lowercase, uppercase, digit and non-alphanumeric classes matching .NET's `char.IsLower`, `IsUpper`, `IsDigit` and `IsLetterOrDigit`, each with its own message.

## Admin application

Same stack as `frontend/` (Vite PWA with the same manifest and app-shell caching, MUI, RTK Query, generated API client, React Router, react-hook-form with zod, react-i18next, Vitest), the same mobile-first layout rules and light/dark/auto appearance, and the same folder structure. What it is for and where it is served are under "Admin application" in [technical.md](../../docs/technical.md#admin-application).

## Shared packages

- Shared code lives in `packages/<name>` as private workspace packages (`"private": true`, name `@wattwise/<name>`), consumed by the apps as `"@wattwise/<name>": "workspace:*"`.
- **Consumed from source.** Each package's entry point is `src/index.ts`, exposed through `"exports": { ".": "./src/index.ts" }`; there is no build step and no `dist/`. Vite compiles shared code as part of each app build, HMR works across packages, and TypeScript sees live types. A package may add a second entry for code that runs at build time instead of in the browser: `@wattwise/ui/vite` (`src/vite.ts`) holds the Vite plugin the apps' `vite.config.ts` imports. Node loads such a file directly, so its relative imports carry the `.ts` extension (`allowImportingTsExtensions` in `tsconfig.base.json`).
- **Single React/MUI instance.** Packages declare `react`, `react-dom`, `@mui/material` and other framework libraries as `peerDependencies`; only the apps own those versions. A package adds a peer (plus a matching devDependency for its own tests) when its code first imports the library, not ahead of time.
- **Configuration.** One root `tsconfig.base.json` extended by every app and package. ESLint and Prettier each have one root config (`eslint.config.js`, `.prettierrc.json`) and run once from the root across all apps and packages ([conventions.md](conventions.md)). Vitest is a root devDependency and its config is shared through the root `vitest.base.js`; each app and package has a `vitest.config.js` that merges it with `mergeConfig(base, defineProject({ ... }))` and adds its own options (name, environment); an app also merges its `vite.config.ts`, for the React plugin and the `@/` alias. Every app and package has `test` (`vitest run`) and `typecheck` (`tsc`, TypeScript 7) scripts, fanned out by the root `pnpm test` and `pnpm typecheck`. TypeScript project references are added only if type-checking becomes slow.
- **Initial packages:** `api-client`, `ui`, `core`, `i18n`. New packages are created only when code is genuinely needed by more than one app. Which package may import which is under "Import direction" in [structure.md](structure.md#import-direction).
