---
paths:
  - '*/src/**/*.{ts,tsx}'
---

# Files and exports

- File and folder names are kebab-case; component names inside stay PascalCase. One component per file, named after the file.
- A file whose only export is a function (component, hook, helper) is a `const` arrow function ending with `export default Name`.
- A file with several exports uses named exports, and exported functions there are still const arrows. Exported types stay named.
- No exported function declarations (ESLint rejects them). A generic arrow in `.tsx` needs `extends`: `<Key extends string>(...) =>`.
- A package's `index.ts` re-exports defaults as `export { default as Name } from './name'`; no barrel files elsewhere.
- Inside a package use relative imports; apps use the `@/` alias.
- Never edit `packages/api-client/src/generated/`: `pnpm generate:api` writes it from the Api's OpenAPI document.

The full rules are in [conventions.md](../../../frontend/docs/conventions.md#files-and-naming).
