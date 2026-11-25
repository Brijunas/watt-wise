import assert from 'node:assert/strict'
import { describe, it } from 'node:test'
import {
  checkPrereqs,
  composeIsRecent,
  hasIcu,
  miseIsActivated,
  parseComposeVersion,
  findOnPath,
  prerequisiteProblems as problemsWithOptions,
} from './prereqs.ts'
import type { Env } from './run.ts'
import type { Runner, RunResult } from './run.ts'
import { SetupError } from './steps.ts'

// A runner that answers by the command line; anything else fails (status 1).
const runnerOf =
  (answers: Record<string, Partial<RunResult>>): Runner =>
  (command, args) => {
    const answer = answers[[command, ...args].join(' ')] ?? { status: 1 }
    return { status: 0, stdout: '', stderr: '', ...answer }
  }

describe('compose version', () => {
  it('parses the forms Docker prints', () => {
    assert.deepEqual(parseComposeVersion('v2.40.3\n'), [2, 40])
    assert.deepEqual(parseComposeVersion('2.22.0-desktop.1'), [2, 22])
    assert.deepEqual(parseComposeVersion('2.22'), [2, 22])
    assert.equal(parseComposeVersion('docker-compose version'), undefined)
    assert.equal(parseComposeVersion(''), undefined)
  })

  it('needs 2.22 or newer', () => {
    assert.equal(composeIsRecent([2, 21]), false)
    assert.equal(composeIsRecent([2, 22]), true)
    assert.equal(composeIsRecent([2, 23]), true)
    assert.equal(composeIsRecent([3, 0]), true)
    assert.equal(composeIsRecent([1, 99]), false)
  })
})

describe('miseIsActivated', () => {
  it('trusts what mise doctor says', () => {
    assert.equal(
      miseIsActivated(runnerOf({ 'mise doctor --json': { stdout: '{"activated":true}' } }), {}),
      true,
    )
    assert.equal(
      miseIsActivated(runnerOf({ 'mise doctor --json': { stdout: '{"activated":false}' } }), {
        __MISE_SESSION: 'x',
      }),
      false,
    )
  })

  it('falls back to the variables activation sets when doctor fails or is unreadable', () => {
    const failing = runnerOf({ 'mise doctor --json': { status: 1 } })
    assert.equal(miseIsActivated(failing, { __MISE_SESSION: 'x' }), true)
    assert.equal(miseIsActivated(failing, { __MISE_DIFF: 'x' }), true)
    assert.equal(miseIsActivated(failing, {}), false)
    const garbled = runnerOf({ 'mise doctor --json': { stdout: 'not json' } })
    assert.equal(miseIsActivated(garbled, {}), false)
  })
})

describe('hasIcu', () => {
  it('accepts .NET invariant mode', () => {
    assert.equal(hasIcu(runnerOf({}), { DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: '1' }), true)
    assert.equal(hasIcu(runnerOf({}), { DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: 'true' }), true)
  })

  it('finds libicuuc in the loader cache', () => {
    const runner = runnerOf({
      'ldconfig -p': { stdout: '\tlibicuuc.so.76 (libc6,x86-64) => /usr/lib64/libicuuc.so.76\n' },
    })
    assert.equal(hasIcu(runner, {}), true)
  })
})

// Without the 1Password CLI check, which has its own tests below.
const prerequisiteProblems = (platform: string, runner: Runner, env: Env): string[] =>
  problemsWithOptions(platform, runner, env, { op: false })

describe('findOnPath', () => {
  it('finds the first match in PATH order', () => {
    const exists = (path: string): boolean => path === '/usr/bin/op' || path === '/opt/op'
    assert.equal(findOnPath('op', { PATH: '/opt:/usr/bin' }, 'linux', exists), '/opt/op')
    assert.equal(findOnPath('op', { PATH: '/bin:/usr/bin' }, 'linux', exists), '/usr/bin/op')
    assert.equal(findOnPath('op', { PATH: '/bin' }, 'linux', exists), undefined)
    assert.equal(findOnPath('op', {}, 'linux', exists), undefined)
  })

  it('uses PATHEXT and a Path variable of any case on Windows', () => {
    const exists = (path: string): boolean => path === 'C:\\Program Files\\1Password\\op.exe'
    assert.equal(
      findOnPath(
        'op',
        { Path: 'C:\\Windows;C:\\Program Files\\1Password', PATHEXT: '.com;.exe' },
        'win32',
        exists,
      ),
      'C:\\Program Files\\1Password\\op.exe',
    )
  })
})

describe('the 1Password CLI prerequisite', () => {
  const good = {
    'git --version': { stdout: 'git version 2.50.0\n' },
    'docker --version': { stdout: 'Docker version 29.0.0\n' },
    'docker compose version --short': { stdout: '2.40.0\n' },
    'mise --version': { stdout: '2026.11.12\n' },
    'mise doctor --json': { stdout: '{"activated":true}' },
  }
  const env = (path: string): Env => ({
    PATH: path,
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: '1',
  })

  it('passes when op is on PATH outside mise', () => {
    const problems = problemsWithOptions('linux', runnerOf(good), env('/usr/bin'), {
      exists: (path) => path === '/usr/bin/op',
    })
    assert.deepEqual(problems, [])
  })

  it('asks for the official install when op is missing', () => {
    const problems = problemsWithOptions('linux', runnerOf(good), env('/usr/bin'), {
      exists: () => false,
    })
    assert.equal(problems.length, 1)
    assert.match(
      problems[0] ?? '',
      /not installed: https:\/\/developer\.1password\.com\/docs\/cli\/get-started\//,
    )
  })

  it('accepts any op on PATH: token sign-in needs no setgid and no particular install', () => {
    const mise = '/home/dev/.local/share/mise/shims'
    const problems = problemsWithOptions('linux', runnerOf(good), env(mise), {
      exists: (path) => path === `${mise}/op`,
    })
    assert.deepEqual(problems, [])
  })

  it('does not look for op when the secrets steps are skipped', () => {
    const problems = problemsWithOptions('linux', runnerOf(good), env('/usr/bin'), {
      op: false,
      exists: () => false,
    })
    assert.deepEqual(problems, [])
  })
})

describe('prerequisiteProblems', () => {
  const good = {
    'git --version': { stdout: 'git version 2.50.0\n' },
    'docker --version': { stdout: 'Docker version 29.0.0\n' },
    'docker compose version --short': { stdout: '2.40.0\n' },
    'mise --version': { stdout: '2026.11.12\n' },
    'mise doctor --json': { stdout: '{"activated":true}' },
  }
  const env = { DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: '1' }

  it('finds nothing wrong on a complete machine', () => {
    assert.deepEqual(prerequisiteProblems('linux', runnerOf(good), env), [])
  })

  it('lists every missing prerequisite with an install link', () => {
    const problems = prerequisiteProblems('linux', runnerOf({}), env)
    assert.equal(problems.length, 3)
    assert.ok(problems.every((problem) => problem.includes('https://')))
  })

  it('names an old Compose and a failing Compose differently', () => {
    const old = prerequisiteProblems(
      'linux',
      runnerOf({ ...good, 'docker compose version --short': { stdout: '2.21.9' } }),
      env,
    )
    assert.equal(old.length, 1)
    assert.match(old[0] ?? '', /2\.21; 2\.22 or newer/)
    const missing = prerequisiteProblems(
      'linux',
      runnerOf({ ...good, 'docker compose version --short': { status: 1 } }),
      env,
    )
    assert.match(missing[0] ?? '', /Compose plugin/)
  })

  it('asks for mise to be activated', () => {
    const problems = prerequisiteProblems(
      'linux',
      runnerOf({ ...good, 'mise doctor --json': { stdout: '{"activated":false}' } }),
      env,
    )
    assert.match(problems[0] ?? '', /not activated/)
  })

  it('wants Git for Windows on win32 and any git elsewhere', () => {
    const plainGit = runnerOf(good)
    assert.match(prerequisiteProblems('win32', plainGit, env)[0] ?? '', /Git for Windows/)
    const windowsGit = runnerOf({
      ...good,
      'git --version': { stdout: 'git version 2.50.0.windows.1\n' },
    })
    assert.deepEqual(prerequisiteProblems('win32', windowsGit, env), [])
    assert.deepEqual(prerequisiteProblems('darwin', plainGit, env), [])
  })

  it('stops with all the problems in one SetupError', () => {
    assert.throws(
      () => {
        checkPrereqs('linux', runnerOf({}), env, { op: false })
      },
      (error: unknown) =>
        error instanceof SetupError &&
        error.message.startsWith('these prerequisites are missing or need a fix:') &&
        error.message.split('\n').length === 4 &&
        error.message
          .split('\n')
          .slice(1)
          .every((line) => line.startsWith('setup:   - ')),
    )
  })
})
