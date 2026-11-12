# Shared Package Guidelines

## Scope & Hierarchy

Follow the [root guide](../AGENTS.md), then the [shared web guide](../frontend/AGENTS.md). These cover every `packages/*` workspace. Packages have no separate docs; their path rules are in [.claude/rules](.claude/rules/) (adjusted copies of the frontend ones), and [Claude notes](CLAUDE.md) point to shared frontend guidance.

## Required Documentation

- Before changing package boundaries, dependencies, exports, or the generated API client: [architecture](../frontend/docs/architecture.md).
- Before changing shared module organization, state, or translations: [structure](../frontend/docs/structure.md).
- Before TypeScript/React changes: [conventions](../frontend/docs/conventions.md).
- Before tests or shared test helpers: [testing](../frontend/docs/testing.md).
- Before UI changes in `@wattwise/ui`: [accessibility](../frontend/docs/accessibility.md).
- For commands and checks: [development](../docs/development.md); use `@wattwise/<name>` in pnpm filters.

Keep package decisions in the shared frontend docs and preserve the documented dependency graph. For authentication or personal data handling, read root [security](../docs/security.md) first. If a package later gains local documentation or rules, link them here and keep them subordinate to the shared specifications.
