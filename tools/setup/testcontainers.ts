// The backend's integration tests start PostgreSQL through Testcontainers, which talks to the Docker daemon
// of the active context. Ryuk (its cleanup container) is switched off in the test fixtures, so a reachable
// daemon is the only thing to check.
import { run as defaultRun } from './run.ts'
import type { Env, Runner } from './run.ts'
import { fail, step } from './steps.ts'

export const checkTestcontainers = (runner: Runner = defaultRun, env: Env = process.env): void => {
  const info = runner('docker', ['info', '--format', '{{.ServerVersion}}'])
  if (info.status !== 0) {
    const context = runner('docker', ['context', 'show']).stdout.trim()
    const where =
      env.DOCKER_HOST !== undefined && env.DOCKER_HOST !== ''
        ? `DOCKER_HOST ${env.DOCKER_HOST}`
        : `the '${context === '' ? 'default' : context}' Docker context`
    const reason = info.stderr.trim().split('\n')[0] ?? ''
    fail(
      `the Docker daemon isn't reachable on ${where}, so the backend tests (Testcontainers) can't start PostgreSQL. Start Docker and run setup again.${reason === '' ? '' : ` Docker says: ${reason}`}`,
    )
  }
  step('testcontainers', 'unchanged')
}
