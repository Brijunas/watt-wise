// Called by the husky hooks post-merge, post-checkout and post-rewrite: rebuild the Development stack
// when it runs from the checkout where the git command ran (deploy/docs/development-stack.md, "Following git").
// Never blocks git: it always exits 0. With --skip-mid-rebase (post-checkout) it also stays silent while
// a rebase, cherry-pick, revert or bisect is in progress; post-merge and post-rewrite fire with that state still set.
import { spawnSync } from 'node:child_process'
import { existsSync, realpathSync } from 'node:fs'
import { isAgentSession, isMidOperation, sameCheckout } from './follow-stack-rules.ts'

const container = 'wattwise-development-api-1'
const hint = 'mise run up'

const capture = (command: string, args: string[]): string | undefined => {
  const result = spawnSync(command, args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] })
  return result.status === 0 ? result.stdout.trim() : undefined
}

const real = (path: string): string | undefined => {
  try {
    return realpathSync(path)
  } catch {
    return undefined
  }
}

const followStack = (): void => {
  if (capture('docker', ['--version']) === undefined) return
  const top = capture('git', ['rev-parse', '--show-toplevel'])
  const gitDir = capture('git', ['rev-parse', '--absolute-git-dir'])
  if (top === undefined || gitDir === undefined) return

  if (process.argv[2] === '--skip-mid-rebase' && isMidOperation(gitDir, existsSync)) return

  // The bind source of /src; empty or an error means no stack to follow.
  const source = capture('docker', [
    'inspect',
    '--format',
    '{{range .Mounts}}{{if eq .Destination "/src"}}{{.Source}}{{end}}{{end}}',
    container,
  ])
  if (!sameCheckout(source, top, real)) return

  // An agent session must not run `op`: it prints the command instead.
  if (isAgentSession(process.env)) {
    console.error('follow-stack: the Development stack runs from this checkout; rebuild it with:')
    console.error(`  ${hint}`)
    return
  }

  console.error(
    'follow-stack: the Development stack runs from this checkout; rebuilding (Ctrl-C to skip)',
  )
  const rebuild = spawnSync(
    'op',
    [
      'run',
      '--env-file',
      'deploy/development/compose.env',
      '--',
      'docker',
      'compose',
      '-f',
      'deploy/docker-compose.development.yml',
      'up',
      '-d',
      '--wait',
    ],
    { cwd: top, stdio: ['ignore', 'inherit', 'inherit'] },
  )
  if (rebuild.status !== 0) {
    console.error('follow-stack: rebuild failed; run it yourself:')
    console.error(`  ${hint}`)
  }
}

try {
  followStack()
} catch {
  // Never blocks git.
}
process.exitCode = 0
