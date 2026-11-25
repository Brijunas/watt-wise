import assert from 'node:assert/strict'
import { describe, it, mock } from 'node:test'
import type { Runner, RunResult } from './run.ts'
import { SetupError } from './steps.ts'
import { checkTestcontainers } from './testcontainers.ts'

const runnerOf =
  (answers: Record<string, Partial<RunResult>>): Runner =>
  (command, args) => {
    const answer = answers[[command, ...args].join(' ')] ?? { status: 1 }
    return { status: 0, stdout: '', stderr: '', ...answer }
  }

const INFO = 'docker info --format {{.ServerVersion}}'

describe('checkTestcontainers', () => {
  it('reports unchanged when the daemon answers', () => {
    const log = mock.method(console, 'log', () => undefined)
    try {
      checkTestcontainers(runnerOf({ [INFO]: { stdout: '29.0.0' } }), {})
      assert.deepEqual(log.mock.calls[0]?.arguments, ['setup: testcontainers: unchanged'])
    } finally {
      log.mock.restore()
    }
  })

  it('names the context and Docker own reason when the daemon is down', () => {
    const runner = runnerOf({
      [INFO]: { status: 1, stderr: 'Cannot connect to the Docker daemon\nmore\n' },
      'docker context show': { stdout: 'rootless\n' },
    })
    assert.throws(
      () => {
        checkTestcontainers(runner, {})
      },
      (error: unknown) =>
        error instanceof SetupError &&
        error.message.includes("the 'rootless' Docker context") &&
        error.message.includes('Docker says: Cannot connect to the Docker daemon') &&
        !error.message.includes('more'),
    )
  })

  it('names DOCKER_HOST when it is set', () => {
    assert.throws(
      () => {
        checkTestcontainers(runnerOf({}), { DOCKER_HOST: 'unix:///x.sock' })
      },
      (error: unknown) =>
        error instanceof SetupError && error.message.includes('DOCKER_HOST unix:///x.sock'),
    )
  })
})
