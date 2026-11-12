# CLAUDE.md (frontend)

Notes for Claude Code when working in `frontend/`, `admin/` or `packages/`. Both apps and the `@wattwise/*` packages share one set of docs in [docs/](docs/): read [docs/structure.md](docs/structure.md) and [docs/architecture.md](docs/architecture.md) before writing code, and [docs/testing.md](docs/testing.md) before writing tests.

## TypeScript files

- Follow [docs/conventions.md](docs/conventions.md#files-and-naming): kebab-case file names, one component per file, no barrel files inside an app, the `@/` alias across folders, and arrow functions with a default export for single-function files.
- Put each thing where "Where things go" in [docs/structure.md](docs/structure.md#where-things-go) says. When the import rules block an import, move the code down (shared folder or package) instead of working around the rule.
- No literal user-facing text in a component: see "Translations" in [docs/structure.md](docs/structure.md#translations).
- Never edit `packages/api-client/src/generated/` by hand: `pnpm generate:api` writes it from the Api's OpenAPI document ([docs/architecture.md](docs/architecture.md#api-client)).

## Async UI code

Check [.claude/rules/async-session.md](.claude/rules/async-session.md) whenever code awaits something.

## Claude Code tooling

Sessions may start in `frontend/`, `admin/`, `packages/` or the root; each has its own `.claude/settings.json` (changed together with the root's). Hook scripts are in the root `.claude/hooks/`.

- `frontend-lint` runs after every edit of a `.ts` or `.tsx` file and reports ESLint findings on it. Fix them before moving on.
- `frontend-generated-guard` blocks edits under `packages/api-client/src/generated/`. Run `pnpm generate:api`.
- Path rules in `.claude/rules/` load with the files they name: `files-and-exports`, `structure`, `async-session`, `tests` and `accessibility`. `admin/.claude/rules` is a symlink to them; `packages/.claude/rules/` holds adjusted copies. The docs own the rules; keep both in step.
- The `frontend-developer` agent writes production code; its brief names the files it owns. Don't use a general-purpose agent for frontend code.
- The `frontend-wcag` agent audits and fixes accessibility after `frontend-developer`, alone, when the story orchestrator decides ([docs/accessibility.md](docs/accessibility.md)).
- The `frontend-test-writer` agent writes tests and owns `testing/` (MSW handlers, test-utils). Its reviewers are `frontend-reviewer` (code) and `frontend-test-reviewer` (tests, scoped StrykerJS), or Codex's reviewer in a Codex review (root `CLAUDE.md`, "Claude Code setup").
- `/frontend-translations` holds the steps for adding, changing, moving or removing text.
- `/smoke-check` drives a story's flows in a browser against the running system (root `CLAUDE.md`).
- The Playwright MCP drives the apps for manual checks.

## Testing rules

The rules are in [docs/testing.md](docs/testing.md#writing-tests-that-catch-bugs). In short:

- Lowest layer that reaches the behaviour. Every UI test runs in LT and EN with literal expectations written by hand.
- Query like a user (`getByRole`, labels, `userEvent`); no snapshots.
- Races use `createGate`; each mapped ProblemDetails error shows its localized message.
- fast-check for pure rules; a `vitest-axe` test per new page and per `@wattwise/ui` component state.
- Run StrykerJS alone: it mutates source files in place. Command in [docs/testing.md](docs/testing.md#mutation-testing).

## Before committing

Run the JS [checks](../docs/development.md#checks) (`pnpm lint`, `pnpm format:check`, `pnpm typecheck`, `pnpm test`, `pnpm build`); all must pass with no warnings.
