---
paths:
  - 'src/**'
---

# Structure

- KISS: write the simplest code that meets the spec. No speculative abstraction, options or layers nobody asked for.
- DRY: look in `@wattwise/*` and the app's shared folders for what exists before writing new code. When a second place needs code, move it down to a shared folder or package instead of copying it.
- Put each thing where "Where things go" says: pages in `app/routes/`, a feature's UI in `features/<f>/components/`, generic UI in `@wattwise/ui`, endpoints generated into `@wattwise/api-client`.
- Imports flow shared -> features -> app. A feature never imports another feature; the route module composes them or passes props and slots.
- When an import rule blocks an import, move the code down; don't work around the rule.
- Links and redirects use `config/routes.ts`, never `app/router.tsx`.
- No literal user-facing text in a component: it comes from a namespace in both LT and EN (`frontend-translations` skill).

The full rules are in [structure.md](../../docs/structure.md#where-things-go).
