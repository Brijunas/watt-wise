# Setup

How to get a working Watt-Wise checkout on a new machine. This guide covers what exists today: the root workspace, the shared JS tooling and the git hooks. Sections for the backend, local database and secrets are added by the stories that introduce them (E2, E5).

## 1. Prerequisites

Install these yourself. Everything else comes from the repository.

| Tool                                              | Why                                                                         | Notes                                                                                                                                                                  |
| ------------------------------------------------- | --------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| git                                               | Version control                                                             | Any recent version                                                                                                                                                     |
| [mise](https://mise.jdx.dev/getting-started.html) | Installs Node, pnpm, .NET and Betterleaks at the versions the repo asks for | Must be [activated in your shell](https://mise.jdx.dev/getting-started.html#activate-mise) so the tools are on `PATH` in every terminal, including when git runs hooks |
| [gh](https://cli.github.com/) (optional)          | Pull requests from the command line                                         | Run `gh auth login` once                                                                                                                                               |

Don't install Node, pnpm or .NET globally for this project. mise provides them per directory, and a global copy can shadow the right one.

To activate mise in bash, add this to `~/.bashrc`, then open a new terminal. For zsh, put the same line in `~/.zshrc` with `zsh` instead of `bash`.

```bash
eval "$(mise activate bash)"
```

## 2. Clone and install the tools

```bash
git clone git@github.com:Brijunas/watt-wise.git
cd watt-wise
mise trust
mise install
```

- `mise trust` is needed once per clone: mise doesn't load a project's `mise.toml` until you trust it.
- `mise install` installs everything listed in the root `mise.toml`:

  | Tool        | Version policy                                                                  |
  | ----------- | ------------------------------------------------------------------------------- |
  | Node        | latest LTS                                                                      |
  | pnpm        | latest                                                                          |
  | Betterleaks | latest (secret scanner used by the pre-commit hook)                             |
  | .NET SDK    | pinned to the .NET 11 RC until .NET 11 is released (November 2026), then latest |

Versions float on purpose so the project stays current. Rerun `mise install` (or `mise upgrade`) after pulling a change to `mise.toml` or whenever you want the newest releases.

Check what's active:

```bash
mise current
```

## 3. Install the JS dependencies

```bash
pnpm install
```

- pnpm is the only supported package manager. `npm install` and `yarn` are rejected by a `preinstall` check.
- The install fails if your Node or pnpm is older than the `engines` floors in `package.json`. That usually means mise isn't active in your shell.
- The `prepare` script installs the git hooks (Husky). `git config core.hooksPath` should now print `.husky/_`.

## 4. Check that everything works

```bash
pnpm lint
pnpm format:check
pnpm test
pnpm build
```

All four should exit without errors. Until the apps and packages exist (S1.3, E3), `test` and `build` have nothing to run and pass trivially.

## Everyday commands

| Command                       | What it does                                                                                          |
| ----------------------------- | ----------------------------------------------------------------------------------------------------- |
| `pnpm lint`                   | ESLint over the whole repo, using the root `eslint.config.js`. Warnings fail.                         |
| `pnpm format`                 | Prettier rewrites every file it supports.                                                             |
| `pnpm format:check`           | Prettier reports unformatted files without changing them.                                             |
| `pnpm test`                   | Runs `test` in every workspace package that has one.                                                  |
| `pnpm build`                  | Runs `build` in every workspace package that has one.                                                 |
| `pnpm exec tsc -p <package>`  | Type-checks a package with TypeScript 7. A root `typecheck` script arrives with the packages in S1.3. |
| `pnpm exec tsc6 -p <package>` | Same with TypeScript 6, the version ESLint uses.                                                      |

Code style: single quotes, no semicolons, 100-column lines. Prettier and ESLint enforce it, so you don't have to remember it.

## What happens when you commit

The pre-commit hook (`.husky/pre-commit`) runs two checks on the staged changes. Either one can stop the commit.

1. **Secret scan.** Betterleaks looks for credentials (API keys, tokens, passwords) in the staged changes. If it finds one, the commit is rejected and the finding is printed with the value redacted.
   - Remove the secret from the change. Secrets live in 1Password and reach the app as environment variables; see "Secrets and configuration" in [technical.md](technical.md).
   - Don't bypass the hook with `git commit --no-verify`, and don't add an exception without agreeing on it first.
   - If the hook says `betterleaks not found`, mise isn't active in the shell git runs in, or you haven't run `mise install`.
2. **Lint and format** (lint-staged, configured in `lint-staged.config.js`). ESLint `--fix` and Prettier run on the staged files; fixes are added to the commit automatically. Any remaining lint error stops the commit.

## Editor

Any editor works. For VS Code, install the ESLint and Prettier extensions and turn on format-on-save with Prettier.

**TypeScript version.** The repo has TypeScript 7 (`tsc`) and TypeScript 6 (the `typescript` package) side by side, because typescript-eslint doesn't support TypeScript 7 yet. Editors pick up TypeScript 6 from `node_modules/typescript`, which matches what ESLint sees. [known-issues.md](known-issues.md) explains the setup and when it goes away.

## Claude Code

The repo ships project settings for Claude Code in `.claude/settings.json`. Nothing to install; they apply when you start Claude Code in the repo.

- Every file Claude writes or edits is formatted with Prettier (`.claude/hooks/prettier-write.sh`).
- Claude is denied reading or editing local secret files (`.env*` except `.env.example`, keys and certificates) and reading build output. Claude Code has no `.claudeignore`; these deny rules are the supported way to do that.
- `.claude/skills/ship-pr` is a project skill for opening, merging and cleaning up pull requests.
- `CLAUDE.md` at the repo root gives Claude the project context.

Personal overrides go in `.claude/settings.local.json`, which stays out of git.

## Troubleshooting

| Problem                                                         | Fix                                                                                                                              |
| --------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `node`, `pnpm` or `betterleaks` not found, or the wrong version | mise isn't active in this shell. Check `mise doctor`, add `mise activate` to your shell config and open a new terminal.          |
| mise ignores `mise.toml`                                        | Run `mise trust` in the repo root.                                                                                               |
| `pnpm install` fails with an engines error                      | Same as above: an old Node or pnpm from outside mise is on `PATH`.                                                               |
| Commits don't run the hook                                      | Run `pnpm install` again to rerun `prepare`, then check `git config core.hooksPath` prints `.husky/_`.                           |
| Hook fails only in a GUI git client                             | The GUI doesn't load your shell config, so mise's tools aren't on its `PATH`. Commit from a terminal, or start the GUI from one. |
| ESLint says a file "was not found by the project service"       | The `.ts`/`.tsx` file isn't included by any `tsconfig.json`. Add it to the nearest package's tsconfig `include`.                 |
