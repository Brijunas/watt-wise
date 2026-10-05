# Development

Everyday commands and workflow. For first-time machine setup, see [setup.md](setup.md). Every command runs from the repo root. Commands for the backend (E2) and the apps (E3) are added here by the stories that introduce them.

## Checks

```bash
pnpm lint
pnpm format:check
pnpm typecheck
pnpm test
pnpm build
```

Run all five before committing; CI enforces the same from E5. `typecheck` and `test` run in every package under `packages/`. Until the apps exist (E3), `build` has nothing to run and passes trivially.

## Commands

| Command                                  | What it does                                                                                            |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| `mise install`                           | Installs the tool versions from `mise.toml`. Rerun after pulling a change to it.                        |
| `pnpm install`                           | Installs the JS dependencies for the whole workspace. Rerun after a pull that changes `pnpm-lock.yaml`. |
| `pnpm lint`                              | ESLint over the whole repo, using the root `eslint.config.js`. Warnings fail.                           |
| `pnpm format`                            | Prettier rewrites every file it supports.                                                               |
| `pnpm format:check`                      | Prettier reports unformatted files without changing them.                                               |
| `pnpm typecheck`                         | Runs `typecheck` (`tsc`, TypeScript 7) in every workspace package that has one.                         |
| `pnpm test`                              | Runs `test` (`vitest run`) in every workspace package that has one.                                     |
| `pnpm build`                             | Runs `build` in every workspace package that has one.                                                   |
| `pnpm --filter @wattwise/core exec tsc6` | Type-checks one package with TypeScript 6, the version ESLint uses.                                     |

### Running a single test

Use `--filter` with the package name. Extra arguments go straight to Vitest.

| Command                                                           | What it runs                                 |
| ----------------------------------------------------------------- | -------------------------------------------- |
| `pnpm --filter @wattwise/core test`                               | One package's tests                          |
| `pnpm --filter @wattwise/core test src/index.test.ts`             | One test file (path relative to the package) |
| `pnpm --filter @wattwise/core test -t "exports its package name"` | Tests whose name matches                     |
| `pnpm --filter @wattwise/core exec vitest`                        | One package's tests in watch mode            |

## Updating dependencies

The policy (minimum release age, how majors are taken, Renovate later) is in "Dependency updates" in [technical.md](technical.md). Check [known-issues.md](known-issues.md) before touching TypeScript or typescript-eslint.

| Command                          | What it does                                                                  |
| -------------------------------- | ----------------------------------------------------------------------------- |
| `pnpm outdated -r`               | Lists outdated dependencies in every workspace package, including the root.   |
| `pnpm update -r`                 | Updates within the existing ranges, so no majors. Low risk.                   |
| `pnpm update -r -i --latest`     | Lets you pick bumps past the current range (majors) and rewrites the ranges.  |
| `pnpm update -r --latest <name>` | Bumps one package, or a pattern such as `"@eslint/*"`, to its latest version. |

If the newest release is younger than the minimum release age, pnpm picks the newest one that is old enough. After updating, run the [checks](#checks), then commit the `package.json` files and `pnpm-lock.yaml` together in their own commit.

## What happens when you commit

The pre-commit hook (`.husky/pre-commit`) runs two checks on the staged changes. Either one can stop the commit.

1. **Secret scan.** Betterleaks looks for credentials (API keys, tokens, passwords) in the staged changes. If it finds one, the commit is rejected and the finding is printed with the value redacted.
   - Remove the secret from the change. Secrets live in 1Password and reach the app as environment variables; see "Secrets and configuration" in [technical.md](technical.md).
   - Don't bypass the hook with `git commit --no-verify`, and don't add an exception without agreeing on it first.
   - If the hook says `betterleaks not found`, mise isn't active in the shell git runs in, or you haven't run `mise install`.
2. **Lint and format** (lint-staged, configured in `lint-staged.config.js`). ESLint `--fix` and Prettier run on the staged files; fixes are added to the commit automatically. Any remaining lint error stops the commit.

## Troubleshooting

| Problem                                                   | Fix                                                                                                                              |
| --------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Hook fails only in a GUI git client                       | The GUI doesn't load your shell config, so mise's tools aren't on its `PATH`. Commit from a terminal, or start the GUI from one. |
| ESLint says a file "was not found by the project service" | The `.ts`/`.tsx` file isn't included by any `tsconfig.json`. Add it to the nearest package's tsconfig `include`.                 |

For tool, install and hook-setup problems, see "Troubleshooting" in [setup.md](setup.md).
