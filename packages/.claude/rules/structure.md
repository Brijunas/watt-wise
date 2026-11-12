---
paths:
  - '*/src/**'
---

# Structure

- KISS: write the simplest code that meets the spec. No speculative abstraction, options or layers nobody asked for.
- DRY: look in `@wattwise/*` and the app's shared folders for what exists before writing new code. When a second place needs code, move it down to a shared folder or package instead of copying it.
- Put each thing where "Where things go" says; a package never imports an app, and imports other packages only in the graph direction (i18n <- core <- api-client; ui imports core and i18n, never api-client).
- Imports flow shared -> features -> app. A feature never imports another feature; the route module composes them or passes props and slots.
- When an import rule blocks an import, move the code down; don't work around the rule.
- No literal user-facing text in a component: it comes from a namespace in both LT and EN (`frontend-translations` skill).

The full rules are in [structure.md](../../../frontend/docs/structure.md#where-things-go).
