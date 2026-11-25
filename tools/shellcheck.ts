// shellcheck over the shell scripts CI checks (the pull request workflow's `shell` job): every tracked
// *.sh file, the husky hooks (no shebang; each carries a shellcheck directive), and every other tracked
// file whose first line is a sh or bash shebang. `mise run shellcheck`.
import { spawnSync } from 'node:child_process'
import { closeSync, existsSync, openSync, readSync } from 'node:fs'

const HOOKS = new Set([
  '.husky/pre-commit',
  '.husky/post-merge',
  '.husky/post-checkout',
  '.husky/post-rewrite',
])

export const SHELL_SHEBANG = /^#!.*[/ ](ba)?sh( |$)/

// The tracked files to check; `firstLine` gives a file's first line.
export const selectScripts = (
  files: readonly string[],
  firstLine: (file: string) => string,
): string[] =>
  files.filter(
    (file) => file.endsWith('.sh') || HOOKS.has(file) || SHELL_SHEBANG.test(firstLine(file)),
  )

const readFirstLine = (file: string): string => {
  try {
    const fd = openSync(file, 'r')
    try {
      const buffer = Buffer.alloc(256)
      const length = readSync(fd, buffer, 0, buffer.length, 0)
      return buffer.toString('latin1', 0, length).split('\n')[0] ?? ''
    } finally {
      closeSync(fd)
    }
  } catch {
    // A directory (a submodule) or a file that can't be read has no shebang.
    return ''
  }
}

if (import.meta.main) {
  const listed = spawnSync('git', ['ls-files', '-z'], { encoding: 'utf8' })
  if (listed.status !== 0) {
    console.error('shellcheck: git ls-files failed.')
    process.exit(1)
  }
  // A file deleted in the work tree is still tracked until the deletion is staged.
  const tracked = listed.stdout.split('\0').filter((file) => file !== '' && existsSync(file))
  const scripts = selectScripts(tracked, readFirstLine)
  console.log(`shellcheck on ${String(scripts.length)} scripts`)
  if (scripts.length === 0) process.exit(0)
  const result = spawnSync('shellcheck', scripts, { stdio: 'inherit' })
  process.exit(result.status ?? 1)
}
