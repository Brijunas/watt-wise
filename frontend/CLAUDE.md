# CLAUDE.md (frontend)

Notes for Claude Code when working in `frontend/`, `admin/` or `packages/`. Both apps and the `@wattwise/*` packages share one set of docs in [docs/](docs/): read [docs/structure.md](docs/structure.md) and [docs/architecture.md](docs/architecture.md) before writing code, and [docs/testing.md](docs/testing.md) before writing tests.

## TypeScript files

- Follow [docs/conventions.md](docs/conventions.md#files-and-naming): kebab-case file names, one component per file, no barrel files inside an app, the `@/` alias across folders, and arrow functions with a default export for single-function files.
- Put each thing where "Where things go" in [docs/structure.md](docs/structure.md#where-things-go) says. When the import rules block an import, move the code down (shared folder or package) instead of working around the rule.
- No literal user-facing text in a component: see "Translations" in [docs/structure.md](docs/structure.md#translations).

## Before committing

Run the JS [checks](../docs/development.md#checks) (`pnpm lint`, `pnpm format:check`, `pnpm typecheck`, `pnpm test`, `pnpm build`); all must pass with no warnings.
