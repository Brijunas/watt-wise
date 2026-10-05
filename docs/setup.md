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
- `mise install` installs Node, pnpm, Betterleaks (the secret scanner used by the pre-commit hook) and the .NET SDK from the root `mise.toml`. How their versions are chosen is described under "Tool versions" in [technical.md](technical.md).

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

Run the [checks](development.md#checks) from development.md. All five should exit without errors. From here on, [development.md](development.md) covers everyday work.

## Editor

Any editor works. For VS Code, install the ESLint and Prettier extensions and turn on format-on-save with Prettier.

**TypeScript version.** The repo has TypeScript 7 (`tsc`) and TypeScript 6 (the `typescript` package) side by side, because typescript-eslint doesn't support TypeScript 7 yet. Editors pick up TypeScript 6 from `node_modules/typescript`, which matches what ESLint sees. [known-issues.md](known-issues.md) explains the setup and when it goes away.

## Claude Code

Project settings for Claude Code ship with the repo (`.claude/` and `CLAUDE.md`), so there is nothing to install. What they enforce is described under "Code style / linting" and "Secrets and configuration" in [technical.md](technical.md). Personal overrides go in `.claude/settings.local.json`, which stays out of git.

## Troubleshooting

| Problem                                                         | Fix                                                                                                                     |
| --------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `node`, `pnpm` or `betterleaks` not found, or the wrong version | mise isn't active in this shell. Check `mise doctor`, add `mise activate` to your shell config and open a new terminal. |
| mise ignores `mise.toml`                                        | Run `mise trust` in the repo root.                                                                                      |
| `pnpm install` fails with an engines error                      | Same as above: an old Node or pnpm from outside mise is on `PATH`.                                                      |
| Commits don't run the hook                                      | Run `pnpm install` again to rerun `prepare`, then check `git config core.hooksPath` prints `.husky/_`.                  |
