# CLAUDE.md (frontend)

Notes for Claude Code when working in `frontend/`, `admin/` or `packages/`. Both apps and the `@wattwise/*` packages share one set of docs in [docs/](docs/): read [docs/structure.md](docs/structure.md) and [docs/architecture.md](docs/architecture.md) before writing code, and [docs/testing.md](docs/testing.md) before writing tests.

## TypeScript files

- Follow [docs/conventions.md](docs/conventions.md#files-and-naming): kebab-case file names, one component per file, no barrel files inside an app, the `@/` alias across folders.
- Put each thing where "Where things go" in [docs/structure.md](docs/structure.md#where-things-go) says. When the import rules block an import, move the code down (shared folder or package) instead of working around the rule.
- Text the user sees goes through i18n in both Lithuanian and English, never as a literal in a component.

## Before committing

Run the JS [checks](../docs/development.md#checks) (`pnpm lint`, `pnpm format:check`, `pnpm typecheck`, `pnpm test`, `pnpm build`); all must pass with no warnings.
