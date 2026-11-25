import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, it, mock } from 'node:test'
import {
  installDependencies,
  miseSelfUpdate,
  selfUpdateAvailable,
  toolCommand,
  toolEnv,
  toolsHash,
} from './dependencies.ts'
import { readSettings, writeSettings } from './settings.ts'
import type { Env, RunOptions, Runner, RunResult } from './run.ts'

interface Call {
  line: string
  options: RunOptions | undefined
}

// A runner that records every call and answers by the command line; anything else succeeds with no output.
const recorder = (
  answers: Record<string, Partial<RunResult> | (() => Partial<RunResult>)>,
): { runner: Runner; calls: Call[] } => {
  const calls: Call[] = []
  const runner: Runner = (command, args, options) => {
    const line = [command, ...args].join(' ')
    calls.push({ line, options })
    const answer = answers[line]
    const result = typeof answer === 'function' ? answer() : answer
    return { status: 0, stdout: '', stderr: '', ...result }
  }
  return { runner, calls }
}

const captureSteps = (action: () => void): string[] => {
  const log = mock.method(console, 'log', () => undefined)
  try {
    action()
    return log.mock.calls.map((call) => String(call.arguments[0]))
  } finally {
    log.mock.restore()
  }
}

describe('selfUpdateAvailable', () => {
  it('reads the flag of mise doctor', () => {
    const answer = (stdout: string): Runner => recorder({ 'mise doctor --json': { stdout } }).runner
    assert.equal(selfUpdateAvailable(answer('{"self_update_available":true}')), true)
    assert.equal(selfUpdateAvailable(answer('{"self_update_available":false}')), false)
    assert.equal(selfUpdateAvailable(answer('{"self_update_available":"no"}')), false)
    assert.equal(selfUpdateAvailable(answer('{}')), undefined)
    assert.equal(
      selfUpdateAvailable(recorder({ 'mise doctor --json': { status: 1 } }).runner),
      undefined,
    )
  })
})

describe('miseSelfUpdate', () => {
  const available = { 'mise doctor --json': { stdout: '{"self_update_available":true}' } }

  it('skips, without trying, an install that cannot update itself', () => {
    const { runner, calls } = recorder({
      'mise doctor --json': { stdout: '{"self_update_available":false}' },
    })
    const steps = captureSteps(() => {
      miseSelfUpdate(runner, '/repo')
    })
    assert.deepEqual(steps, [
      'setup: mise: skipped (self-update is not available for this install)',
    ])
    assert.ok(!calls.some((call) => call.line.startsWith('mise self-update')))
  })

  it('also recognises mise own message when doctor said nothing', () => {
    const { runner } = recorder({
      'mise self-update --yes': {
        status: 1,
        stderr: 'mise ERROR self-update is disabled for this install, update mise the same way',
      },
    })
    assert.deepEqual(
      captureSteps(() => {
        miseSelfUpdate(runner, '/repo')
      }),
      ['setup: mise: skipped (self-update is not available for this install)'],
    )
  })

  it('reports a real failure with its first line', () => {
    const { runner } = recorder({
      ...available,
      'mise self-update --yes': {
        status: 1,
        stderr: '\nerror sending request: connection refused\nmore',
      },
    })
    assert.deepEqual(
      captureSteps(() => {
        miseSelfUpdate(runner, '/repo')
      }),
      ['setup: mise: skipped (self-update failed: error sending request: connection refused)'],
    )
  })

  it('reports changed only when the version moved', () => {
    let version = '1'
    const moving = recorder({
      ...available,
      'mise --version': () => ({ stdout: version }),
      'mise self-update --yes': () => {
        version = '2'
        return {}
      },
    })
    assert.deepEqual(
      captureSteps(() => {
        miseSelfUpdate(moving.runner, '/repo')
      }),
      ['setup: mise: changed'],
    )
    const still = recorder({ ...available, 'mise --version': { stdout: '1' } })
    assert.deepEqual(
      captureSteps(() => {
        miseSelfUpdate(still.runner, '/repo')
      }),
      ['setup: mise: unchanged'],
    )
  })
})

describe('toolEnv', () => {
  it('merges mise env over the base and drops the token', () => {
    const { runner } = recorder({
      'mise env --json': {
        stdout: JSON.stringify({ PATH: '/tools', OP_SERVICE_ACCOUNT_TOKEN: 'from-mise', N: 1 }),
      },
    })
    const env = toolEnv(runner, '/repo', {
      HOME: '/h',
      PATH: '/old',
      OP_SERVICE_ACCOUNT_TOKEN: 'base',
    })
    assert.deepEqual(env, { HOME: '/h', PATH: '/tools' })
  })

  it('removes a variable that mise env prints as null', () => {
    const { runner } = recorder({ 'mise env --json': { stdout: '{"GONE":null,"KEEP":"1"}' } })
    assert.deepEqual(toolEnv(runner, '/repo', { GONE: 'x', OTHER: 'y' }), { OTHER: 'y', KEEP: '1' })
  })

  it('stops when mise env fails or prints no JSON', () => {
    assert.throws(
      () => toolEnv(recorder({ 'mise env --json': { status: 1 } }).runner, '/r', {}),
      /mise env failed/,
    )
    assert.throws(
      () => toolEnv(recorder({ 'mise env --json': { stdout: 'x' } }).runner, '/r', {}),
      /not JSON/,
    )
  })
})

describe('toolCommand', () => {
  it('runs a .cmd or .bat shim through cmd.exe on Windows only', () => {
    assert.deepEqual(toolCommand('C:\\t\\pnpm.CMD', ['install'], 'win32', 'C:\\cmd.exe'), {
      command: 'C:\\cmd.exe',
      args: ['/d', '/s', '/c', '""C:\\t\\pnpm.CMD" install"'],
      verbatim: true,
    })
    assert.deepEqual(toolCommand('C:\\t\\pnpm.exe', ['install'], 'win32'), {
      command: 'C:\\t\\pnpm.exe',
      args: ['install'],
    })
    assert.deepEqual(toolCommand('/t/pnpm.cmd', ['x'], 'linux'), {
      command: '/t/pnpm.cmd',
      args: ['x'],
    })
  })
})

describe('toolCommand quoting', () => {
  it('quotes a path with spaces, and one with & ^ or %, inside the outer quotes', () => {
    for (const path of ['C:\\Program Files\\pnpm\\pnpm.cmd', 'C:\\a&b\\x^y%z%\\pnpm.cmd']) {
      const command = toolCommand(path, ['install', '--frozen-lockfile'], 'win32')
      assert.deepEqual(command.args.slice(0, 3), ['/d', '/s', '/c'])
      assert.equal(command.args[3], `""${path}" install --frozen-lockfile"`)
      assert.equal(command.args.length, 4)
      assert.equal(command.verbatim, true)
    }
  })
})

describe('toolsHash', () => {
  it('ignores order and blank lines, and changes with the tools', () => {
    assert.equal(toolsHash('node 1\npnpm 2\n'), toolsHash('\npnpm 2\nnode 1'))
    assert.notEqual(toolsHash('node 1\npnpm 2'), toolsHash('node 2\npnpm 2'))
  })
})

describe('installDependencies', () => {
  const run = (
    listing: string,
    base: Env = {},
    settingsDir = mkdtempSync(join(tmpdir(), 'setup-deps-settings-')),
  ): { steps: string[]; calls: Call[]; settingsDir: string } => {
    const root = mkdtempSync(join(tmpdir(), 'setup-deps-'))
    try {
      const { runner, calls } = recorder({
        'mise doctor --json': { stdout: '{"self_update_available":false}' },
        'mise ls --installed': { stdout: listing },
        'mise env --json': {
          stdout: JSON.stringify({ PATH: '/tools', OP_SERVICE_ACCOUNT_TOKEN: 'from-mise' }),
        },
        'mise which pnpm': { stdout: '/t/pnpm\n' },
        'mise which dotnet': { stdout: '/t/dotnet\n' },
      })
      const steps = captureSteps(() => {
        installDependencies({
          runner,
          root,
          platform: 'linux',
          env: base,
          nugetPackages: join(root, 'nuget'),
          settingsDir,
        })
      })
      return { steps, calls, settingsDir }
    } finally {
      rmSync(root, { recursive: true, force: true })
    }
  }

  it('runs the steps in order; a first run (nothing remembered) reports tools as changed', () => {
    const { steps, settingsDir } = run('node 1\ndotnet 10')
    rmSync(settingsDir, { recursive: true, force: true })
    assert.deepEqual(steps, [
      'setup: mise: skipped (self-update is not available for this install)',
      'setup: tools: changed',
      'setup: pnpm: unchanged',
      'setup: dotnet-tools: unchanged',
    ])
  })

  it('remembers the tools: a rerun is unchanged until the listing changes', () => {
    const first = run('node 1\ndotnet 10')
    try {
      assert.ok(first.steps.includes('setup: tools: changed'))
      assert.ok(readSettings(first.settingsDir).tools !== undefined)
      const again = run('dotnet 10\nnode 1\n', {}, first.settingsDir)
      assert.ok(again.steps.includes('setup: tools: unchanged'))
      const moved = run('node 2\ndotnet 10', {}, first.settingsDir)
      assert.ok(moved.steps.includes('setup: tools: changed'))
      const rerun = run('node 2\ndotnet 10', {}, first.settingsDir)
      assert.ok(rerun.steps.includes('setup: tools: unchanged'))
    } finally {
      rmSync(first.settingsDir, { recursive: true, force: true })
    }
  })

  it('keeps the other saved settings when it remembers the tools', () => {
    const settingsDir = mkdtempSync(join(tmpdir(), 'setup-deps-settings-'))
    try {
      writeSettings(settingsDir, { polling: 'on' })
      run('node 1', {}, settingsDir)
      const saved = readSettings(settingsDir)
      assert.equal(saved.polling, 'on')
      assert.ok(saved.tools !== undefined)
    } finally {
      rmSync(settingsDir, { recursive: true, force: true })
    }
  })

  it('starts pnpm and dotnet directly, without the token, from mise own PATH', () => {
    const { calls, settingsDir } = run('node 1', { OP_SERVICE_ACCOUNT_TOKEN: 'base', HOME: '/h' })
    rmSync(settingsDir, { recursive: true, force: true })
    const pnpm = calls.find((call) => call.line === '/t/pnpm install --frozen-lockfile')
    const dotnet = calls.find((call) => call.line === '/t/dotnet tool restore')
    assert.ok(pnpm && dotnet, 'both were started by their path')
    for (const call of [pnpm, dotnet]) {
      const env: Env = call.options?.env ?? { MISSING: 'the call had no env' }
      assert.equal('OP_SERVICE_ACCOUNT_TOKEN' in env, false)
      assert.deepEqual(env, { PATH: '/tools', HOME: '/h' })
    }
    assert.ok(!calls.some((call) => call.line.startsWith('mise exec')))
  })
})
