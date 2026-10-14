# Frontend conventions

Code style, naming and lint rules for `frontend/`, `admin/` and the `@wattwise/*` packages. The rules shared by every project (Prettier, the pre-commit hook) are under "Code style" in [technical.md](../../docs/technical.md#cross-cutting).

## Lint

ESLint runs once from the root `eslint.config.js` across both apps and every package:

- typescript-eslint `strictTypeChecked` + `stylisticTypeChecked`, `@eslint-react` (`eslint-plugin-react` doesn't support ESLint 10), `eslint-plugin-react-hooks`, and `eslint-config-prettier` last.
- ESLint runs on TypeScript 6 while `tsc` is TypeScript 7; why, and what to watch for, is in [known-issues.md](../../docs/known-issues.md).

## Files and naming

- File and folder names are kebab-case: `plan-card.tsx`, `use-plan-filter.ts`, `plans-slice.ts`. Component names inside stay PascalCase.
- One component per file, named after the file.
- **Arrow functions with a default export.** A file whose only export is a function (component, hook or helper) declares it as a `const` arrow function and ends with `export default Name`; importers use `import Name from '...'`, and lazy routes read `.default`. A file with several exports (`lib/i18n.ts`, `config/routes.ts`, `router.tsx`, a package's `create-i18n.ts`) keeps named exports, as do exported types and interfaces. A package's `index.ts` re-exports defaults as `export { default as Name } from './name'`. A generic component needs a trailing comma in `.tsx`: `const IconMenu = <Key extends string,>(...) => {`.
- **No barrel files** (`index.ts` re-exports) inside an app. Import from the file itself. Packages are imported through their entry point ("Consumed from source" in [architecture.md](architecture.md#shared-packages)).
- Imports inside an app use the `@/` alias for `src/` (`@/features/plans/components/plan-card`); relative imports only within the same feature folder.

## Import rules

The root `eslint.config.js` checks the import rules of [structure.md](structure.md#import-direction) for both apps with `eslint-plugin-import-x` (`eslint-plugin-import` doesn't support ESLint 10):

- A TypeScript import resolver that reads the tsconfig `paths`, so the rules see through `@/` and the `@wattwise/*` source exports. Without it they silently skip those imports.
- `import-x/no-restricted-paths` zones: shared folders can't import `features/` or `app/`, except `testing/` importing `app/provider.tsx`; features can't import `app/`; and no feature can import another. One exception: `lib/i18n.ts` may import `features/<f>/locales/*.json`, and nothing else from a feature (see "Translations" in [structure.md](structure.md#translations)). The feature zones are generated from each app's `src/features/` listing, so a new feature is covered without editing the config. Further zones hold the package graph, and keep the packages from importing an app and one app from importing the other.
- `no-restricted-imports` by package name for the same graph. A zone silently skips an import the resolver can't follow, and a workspace package that isn't a declared dependency isn't guaranteed to resolve; the by-name rule doesn't depend on resolution.
- `import-x/no-cycle`.
- `no-restricted-imports` for `useSelector` and `useDispatch` from `react-redux`, off for `stores/hooks.ts`.
