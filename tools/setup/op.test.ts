import assert from 'node:assert/strict'
import {
  chmodSync,
  existsSync,
  mkdirSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { afterEach, beforeEach, describe, it } from 'node:test'
import {
  aclIsOwnerOnly,
  aclPrincipals,
  applyKey,
  ensurePrivate,
  formatTokenEnv,
  legacyBashrcLine,
  legacyTokenPath,
  normalizeToken,
  parseTokenEnv,
  setupToken,
  withoutToken,
} from './op.ts'
import type { PrivateDeps } from './op.ts'
import type { Runner, RunResult } from './run.ts'
import { SetupError } from './steps.ts'

describe('applyKey', () => {
  it('adds a single printable character', () => {
    assert.deepEqual(applyKey('ab', 'c', { name: 'c' }), { value: 'abc', action: 'continue' })
    assert.deepEqual(applyKey('', '_', undefined), { value: '_', action: 'continue' })
  })

  it('drops escape sequences, control and meta keys and other non-printables', () => {
    for (const [text, key] of [
      ['\x1b[A', { name: 'up' }],
      ['\x1b', { name: 'escape' }],
      ['\t', { name: 'tab' }],
      ['\x7f', undefined],
      ['a', { name: 'a', ctrl: true }],
      ['a', { name: 'a', meta: true }],
      [undefined, { name: 'f1' }],
    ] as const) {
      assert.deepEqual(applyKey('ab', text, key), { value: 'ab', action: 'continue' })
    }
  })

  it('handles backspace, Enter, Ctrl-D and Ctrl-C', () => {
    assert.deepEqual(applyKey('abc', undefined, { name: 'backspace' }), {
      value: 'ab',
      action: 'continue',
    })
    assert.equal(applyKey('abc', '\r', { name: 'return' }).action, 'done')
    assert.equal(applyKey('abc', '\n', { name: 'enter' }).action, 'done')
    assert.equal(applyKey('abc', '\x04', { name: 'd', ctrl: true }).action, 'done')
    assert.equal(applyKey('abc', '\x03', { name: 'c', ctrl: true }).action, 'abort')
    assert.equal(applyKey('abc', '\r', { name: 'return' }).value, 'abc')
  })
})

describe('withoutToken', () => {
  it('drops only the service-account token', () => {
    const env = withoutToken({ OP_SERVICE_ACCOUNT_TOKEN: 'x', PATH: '/bin' })
    assert.deepEqual(env, { PATH: '/bin' })
  })
})

describe('the token file text', () => {
  it('round-trips through the dotenv format', () => {
    assert.equal(parseTokenEnv(formatTokenEnv('ops_placeholder')), 'ops_placeholder')
  })

  it('finds the line among others and ignores a blank value', () => {
    assert.equal(
      parseTokenEnv('A=1\nOP_SERVICE_ACCOUNT_TOKEN=ops_placeholder\nB=2\n'),
      'ops_placeholder',
    )
    assert.equal(parseTokenEnv('OP_SERVICE_ACCOUNT_TOKEN=\n'), undefined)
    assert.equal(parseTokenEnv('OTHER=1\n'), undefined)
  })

  it('accepts the base64 characters of a real token and refuses others', () => {
    assert.equal(normalizeToken('ops_aB3-_=.+/xyZ9'), 'ops_aB3-_=.+/xyZ9')
    assert.throws(() => normalizeToken('ops_abc\x1b[A'), /characters a token never has/)
    assert.throws(() => normalizeToken('ops_abc$def'), /characters a token never has/)
    assert.throws(() => normalizeToken('ops_'), /not a service-account token|never has/)
  })

  it('accepts a pasted token with whitespace and refuses anything else', () => {
    assert.equal(normalizeToken('  ops_placeholder \n'), 'ops_placeholder')
    assert.throws(() => normalizeToken('   '), /no token entered/)
    assert.throws(() => normalizeToken('not-a-token'), /start with ops_/)
  })
})

// A runner that answers `icacls` like Windows would, remembering what it was asked.
const icaclsRunner = (states: string[], calls: string[][], grantStatus = 0): Runner => {
  return (command, args): RunResult => {
    assert.equal(command, 'icacls')
    calls.push([...args])
    if (args.length === 1) {
      const stdout = states.length > 1 ? (states.shift() ?? '') : (states[0] ?? '')
      return { status: 0, stdout, stderr: '' }
    }
    return { status: grantStatus, stdout: '', stderr: grantStatus === 0 ? '' : 'Access is denied.' }
  }
}

const PATH = 'C:\\Users\\dev\\.config\\watt-wise\\op.env'
const USER = 'PC\\dev'
const ownerOnly = `${PATH} PC\\dev:(F)\n\nSuccessfully processed 1 files; Failed processing 0 files\n`
const shared = `${PATH} PC\\dev:(I)(F)\n       NT AUTHORITY\\SYSTEM:(I)(F)\n       BUILTIN\\Users:(I)(R)\n\nSuccessfully processed 1 files; Failed processing 0 files\n`

describe('the ACL text of icacls', () => {
  it('lists every principal, with names that hold spaces', () => {
    assert.deepEqual(aclPrincipals(shared, PATH), [
      'PC\\dev',
      'NT AUTHORITY\\SYSTEM',
      'BUILTIN\\Users',
    ])
  })

  it('is owner-only only when the user is the one principal', () => {
    assert.equal(aclIsOwnerOnly(ownerOnly, PATH, USER), true)
    assert.equal(aclIsOwnerOnly(shared, PATH, USER), false)
    assert.equal(aclIsOwnerOnly('Successfully processed 1 files\n', PATH, USER), false)
  })
})

describe('ensurePrivate on win32 (icacls stubbed)', () => {
  const deps = (runner: Runner): PrivateDeps => ({ platform: 'win32', runner, user: USER })

  it('changes nothing when only the user is in the ACL', () => {
    const calls: string[][] = []
    assert.equal(ensurePrivate(PATH, 'file', deps(icaclsRunner([ownerOnly], calls))), false)
    assert.deepEqual(calls, [[PATH]])
  })

  it('removes inheritance, grants the user alone and verifies', () => {
    const calls: string[][] = []
    const changed = ensurePrivate(PATH, 'file', deps(icaclsRunner([shared, ownerOnly], calls)))
    assert.equal(changed, true)
    assert.deepEqual(calls, [[PATH], [PATH, '/inheritance:r', '/grant:r', `${USER}:F`], [PATH]])
  })

  it('grants a folder to its files too', () => {
    const calls: string[][] = []
    ensurePrivate('C:\\dir', 'dir', deps(icaclsRunner([shared, ownerOnly], calls)))
    assert.deepEqual(calls[1], ['C:\\dir', '/inheritance:r', '/grant:r', `${USER}:(OI)(CI)F`])
  })

  it('stops when the ACL is still shared after the grant', () => {
    assert.throws(
      () => ensurePrivate(PATH, 'file', deps(icaclsRunner([shared], []))),
      (error: unknown) =>
        error instanceof SetupError && error.message.includes('still readable by another account'),
    )
  })

  it('stops when icacls refuses the grant', () => {
    assert.throws(
      () => ensurePrivate(PATH, 'file', deps(icaclsRunner([shared], [], 5))),
      /can't restrict/,
    )
  })
})

describe('ensurePrivate on POSIX', { skip: process.platform === 'win32' }, () => {
  let dir = ''
  const deps = (): PrivateDeps => ({
    platform: 'linux',
    runner: () => assert.fail('no command on POSIX'),
    user: 'dev',
    uid: process.getuid?.(),
  })

  beforeEach(() => {
    dir = mkdtempSync(join(tmpdir(), 'setup-op-'))
  })
  afterEach(() => {
    rmSync(dir, { recursive: true, force: true })
  })

  it('sets a file to 600 and a folder to 700, and then reports no change', () => {
    const file = join(dir, 'op.env')
    writeFileSync(file, formatTokenEnv('ops_placeholder'), { mode: 0o644 })
    assert.equal(ensurePrivate(file, 'file', deps()), true)
    assert.equal(statSync(file).mode & 0o777, 0o600)
    assert.equal(ensurePrivate(file, 'file', deps()), false)
    chmodSync(dir, 0o755)
    assert.equal(ensurePrivate(dir, 'dir', deps()), true)
    assert.equal(statSync(dir).mode & 0o777, 0o700)
    assert.equal(ensurePrivate(dir, 'dir', deps()), false)
  })

  it("stops on another user's file", () => {
    const file = join(dir, 'op.env')
    writeFileSync(file, 'x', { mode: 0o600 })
    const other = { ...deps(), uid: (process.getuid?.() ?? 0) + 1 }
    assert.throws(() => ensurePrivate(file, 'file', other), /belongs to another user/)
  })

  it('writes the token file owner-only in an owner-only folder, and reads it back', async () => {
    const home = join(dir, 'watt-wise')
    const file = join(home, 'op.env')
    const input = { dir: home, file, deps: deps(), hasTerminal: true }
    const first = await setupToken({ ...input, ask: () => Promise.resolve(' ops_placeholder\n') })
    assert.deepEqual(first, { token: 'ops_placeholder', changed: true })
    assert.equal(statSync(file).mode & 0o777, 0o600)
    assert.equal(statSync(home).mode & 0o777, 0o700)
    assert.equal(readFileSync(file, 'utf8'), 'OP_SERVICE_ACCOUNT_TOKEN=ops_placeholder\n')
    // A rerun reads the file, asks nothing and changes nothing.
    const again = await setupToken({
      ...input,
      ask: () => Promise.reject(new Error('asked again')),
    })
    assert.deepEqual(again, { token: 'ops_placeholder', changed: false })
  })

  it('repairs loose permissions on a rerun and reports the change', async () => {
    const home = join(dir, 'watt-wise')
    mkdirSync(home, { mode: 0o755 })
    const file = join(home, 'op.env')
    writeFileSync(file, formatTokenEnv('ops_placeholder'), { mode: 0o644 })
    const result = await setupToken({
      dir: home,
      file,
      deps: deps(),
      hasTerminal: false,
      ask: () => Promise.reject(new Error('no prompt')),
    })
    assert.equal(result.changed, true)
    assert.equal(statSync(file).mode & 0o777, 0o600)
    assert.equal(statSync(home).mode & 0o777, 0o700)
  })

  it('moves an old raw token file into op.env without asking, and deletes it', async () => {
    const home = join(dir, 'watt-wise')
    mkdirSync(home, { mode: 0o700 })
    const legacy = legacyTokenPath(home)
    const file = join(home, 'op.env')
    writeFileSync(legacy, 'ops_placeholder', { mode: 0o600 })
    const result = await setupToken({
      dir: home,
      file,
      deps: deps(),
      hasTerminal: false,
      ask: () => Promise.reject(new Error('no prompt')),
    })
    assert.deepEqual(result, { token: 'ops_placeholder', changed: true, migratedFrom: legacy })
    assert.equal(readFileSync(file, 'utf8'), 'OP_SERVICE_ACCOUNT_TOKEN=ops_placeholder\n')
    assert.equal(statSync(file).mode & 0o777, 0o600)
    assert.equal(existsSync(legacy), false)
    assert.equal(
      legacyBashrcLine(legacy),
      `[ -r "${legacy}" ] && export OP_SERVICE_ACCOUNT_TOKEN="$(cat "${legacy}")"`,
    )
  })

  it('does not migrate when op.env exists, and refuses a bad old token', async () => {
    const home = join(dir, 'watt-wise')
    mkdirSync(home, { mode: 0o700 })
    const legacy = legacyTokenPath(home)
    writeFileSync(legacy, 'not-a-token', { mode: 0o600 })
    await assert.rejects(
      setupToken({
        dir: home,
        file: join(home, 'op.env'),
        deps: deps(),
        hasTerminal: false,
        ask: () => Promise.resolve(''),
      }),
      /start with ops_/,
    )
    assert.equal(existsSync(legacy), true)
    writeFileSync(join(home, 'op.env'), formatTokenEnv('ops_placeholder'), { mode: 0o600 })
    const result = await setupToken({
      dir: home,
      file: join(home, 'op.env'),
      deps: deps(),
      hasTerminal: false,
      ask: () => Promise.resolve(''),
    })
    assert.equal(result.migratedFrom, undefined)
    assert.equal(existsSync(legacy), true)
    // op.env wins, the old file stays, and the result says so for the note setup prints.
    assert.equal(result.legacyLeft, legacy)
    assert.equal(result.token, 'ops_placeholder')
  })

  it('has no legacyLeft when only op.env exists', async () => {
    const home = join(dir, 'watt-wise')
    mkdirSync(home, { mode: 0o700 })
    const file = join(home, 'op.env')
    writeFileSync(file, formatTokenEnv('ops_placeholder'), { mode: 0o600 })
    const result = await setupToken({
      dir: home,
      file,
      deps: deps(),
      hasTerminal: false,
      ask: () => Promise.resolve(''),
    })
    assert.equal(result.legacyLeft, undefined)
  })

  it('stops without a terminal when the file is missing', async () => {
    await assert.rejects(
      setupToken({
        dir,
        file: join(dir, 'op.env'),
        deps: deps(),
        hasTerminal: false,
        ask: () => Promise.reject(new Error('no prompt')),
      }),
      /no terminal/,
    )
  })

  it('stops on a token file with no token line', async () => {
    const file = join(dir, 'op.env')
    writeFileSync(file, 'OTHER=1\n', { mode: 0o600 })
    await assert.rejects(
      setupToken({ dir, file, deps: deps(), hasTerminal: false, ask: () => Promise.resolve('') }),
      /no OP_SERVICE_ACCOUNT_TOKEN line/,
    )
  })
})
