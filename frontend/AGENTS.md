# Web Application Guidelines

## Scope & Hierarchy

Follow the [root guide](../AGENTS.md) and its specifications first. This guide supplies shared guidance for `frontend/`, `admin/`, and `packages/`. The [frontend README](README.md) introduces the public app. [Claude notes](CLAUDE.md) summarize implementation lessons; the project specifications below own the rules.

There is no local rules directory. Read and apply the relevant sections of these documents explicitly; admin and packages use the same sources rather than copies.

## Required Documentation

| Read before changing                                                        | Source                                                                |
| --------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| App composition, stack, runtime configuration, PWA, or package dependencies | [Architecture](docs/architecture.md)                                  |
| Features, routes, folders, import direction, Redux, or translations         | [Structure](docs/structure.md)                                        |
| TypeScript/React code, filenames, exports, or lint configuration            | [Conventions](docs/conventions.md)                                    |
| Unit/component tests, route integration tests, fixtures, or MSW handlers    | [Testing](docs/testing.md)                                            |
| Authentication, authorization, tokens, or personal data                     | Root [security](../docs/security.md)                                  |
| Installing tools, starting apps, or running checks                          | Root [setup](../docs/setup.md), [development](../docs/development.md) |
| Tool versions or compatibility workarounds                                  | Root [known issues](../docs/known-issues.md)                          |

Read architecture, structure, and conventions before implementation, then the task-specific sources. Keep frontend-only decisions in these docs, shared system decisions in root docs, and security decisions in the security specification.

## Development & Verification

Run commands from the root. `pnpm --filter frontend test` runs this app's tests; substitute `admin` or `@wattwise/<name>` for other workspaces. Follow the five JavaScript [checks](../docs/development.md#checks) before committing.

Start apps through the documented [1Password development command](../docs/development.md#running-the-apps); notify the user before authorization. API addresses use runtime `/config.js` as defined in architecture.

For generated API changes, follow the [API-client contract](docs/architecture.md#api-client) and run `pnpm generate:api`. Keep generated output and the backend OpenAPI document together. For public assets, use the documented icon-generation command. Test placement, rendering helpers, translation checks, and HTTP mocks follow the testing and structure documents above.
