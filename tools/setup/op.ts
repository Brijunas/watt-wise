// How this machine signs in to 1Password: one flow on every OS, a service-account token. It is asked for
// once with a hidden prompt and kept as an owner-only dotenv file, op.env, that mise.toml loads into every
// mise environment of the repo; it is never an argument and never printed.
import {
  chmodSync,
  existsSync,
  mkdirSync,
  readFileSync,
  renameSync,
  rmSync,
  statSync,
  writeFileSync,
} from 'node:fs'
import { userInfo } from 'node:os'
import { dirname, join } from 'node:path'
import { emitKeypressEvents } from 'node:readline'
import { run as defaultRun } from './run.ts'
import type { Env, Runner } from './run.ts'
import { fail } from './steps.ts'

// The environment without the service-account token, for programs that have no use for it (pnpm's and
// dotnet's dependency scripts).
export const withoutToken = (env: Env): Env => {
  const copy = { ...env }
  delete copy.OP_SERVICE_ACCOUNT_TOKEN
  return copy
}

// --- The token file ---------------------------------------------------------------------------

const TOKEN_KEY = 'OP_SERVICE_ACCOUNT_TOKEN'

export const parseTokenEnv = (text: string): string | undefined => {
  const match = new RegExp(`^${TOKEN_KEY}=(.+)$`, 'm').exec(text)
  const value = match?.[1]?.trim()
  return value === undefined || value === '' ? undefined : value
}

export const formatTokenEnv = (token: string): string => `${TOKEN_KEY}=${token}\n`

// What was typed or pasted becomes a token: whitespace goes, and it must look like a service-account token.
export const normalizeToken = (input: string): string => {
  const token = input.replace(/\s+/g, '')
  if (token === '') return fail('no token entered.')
  if (!token.startsWith('ops_')) {
    return fail('that is not a service-account token (they start with ops_).')
  }
  // The rest is base64 text (letters, digits, - _ + / = and .); anything else was typed or pasted by mistake.
  if (!/^ops_[A-Za-z0-9_\-=.+/]+$/.test(token)) {
    return fail('that is not a service-account token: it has characters a token never has.')
  }
  return token
}

// --- Owner-only files and folders -------------------------------------------------------------

export type PrivateKind = 'file' | 'dir'

export interface PrivateDeps {
  platform: string
  runner: Runner
  // The signed-in user, as icacls names it: DOMAIN\name, or just name.
  user: string
  // POSIX only: the owner's user id.
  uid?: number
}

// The principals icacls lists for a path: every line ends in `<principal>:(<flags>)...`, and the first
// line starts with the path itself.
export const aclPrincipals = (output: string, path: string): string[] => {
  const principals: string[] = []
  for (const raw of output.split(/\r?\n/)) {
    const line = (raw.startsWith(path) ? raw.slice(path.length) : raw).trim()
    const match = /^(.+?):((?:\([A-Za-z,]+\))+)$/.exec(line)
    if (match?.[1] !== undefined) principals.push(match[1])
  }
  return principals
}

// True when the only principal in the ACL is the user.
export const aclIsOwnerOnly = (output: string, path: string, user: string): boolean => {
  const name = (user.split('\\').pop() ?? user).toLowerCase()
  const principals = aclPrincipals(output, path)
  return (
    principals.length > 0 &&
    principals.every(
      (principal) => (principal.split('\\').pop() ?? principal).toLowerCase() === name,
    )
  )
}

const ensurePrivateWindows = (path: string, kind: PrivateKind, deps: PrivateDeps): boolean => {
  const listed = (): string => {
    const result = deps.runner('icacls', [path])
    if (result.status !== 0)
      return fail(`icacls can't read the permissions of ${path}: ${result.stderr.trim()}`)
    return result.stdout
  }
  if (aclIsOwnerOnly(listed(), path, deps.user)) return false
  // Drop what the folder passes down, then grant the user alone (a folder's grant passes down to its files).
  const grant = kind === 'dir' ? `${deps.user}:(OI)(CI)F` : `${deps.user}:F`
  const applied = deps.runner('icacls', [path, '/inheritance:r', '/grant:r', grant])
  if (applied.status !== 0)
    return fail(`icacls can't restrict ${path} to your user: ${applied.stderr.trim()}`)
  if (!aclIsOwnerOnly(listed(), path, deps.user)) {
    return fail(
      `${path} is still readable by another account after icacls. Fix its permissions by hand.`,
    )
  }
  return true
}

// A file is mode 600 and its folder mode 700, both owned by the user; on Windows only the user is in the
// ACL (icacls), which is checked after it is set. Another user's file or folder stops setup. Returns
// true when it had to change something.
export const ensurePrivate = (path: string, kind: PrivateKind, deps: PrivateDeps): boolean => {
  if (deps.platform === 'win32') return ensurePrivateWindows(path, kind, deps)
  const stats = statSync(path)
  if (deps.uid !== undefined && stats.uid !== deps.uid) {
    return fail(`${path} belongs to another user. Fix its owner or remove it.`)
  }
  const wanted = kind === 'dir' ? 0o700 : 0o600
  if ((stats.mode & 0o777) === wanted) return false
  chmodSync(path, wanted)
  return true
}

export const defaultPrivateDeps = (runner: Runner = defaultRun): PrivateDeps => ({
  platform: process.platform,
  runner,
  user:
    process.platform === 'win32' && process.env.USERDOMAIN !== undefined
      ? `${process.env.USERDOMAIN}\\${userInfo().username}`
      : userInfo().username,
  uid: typeof process.getuid === 'function' ? process.getuid() : undefined,
})

// --- The hidden prompt ------------------------------------------------------------------------

export interface Key {
  name?: string
  ctrl?: boolean
  meta?: boolean
}

export interface KeyResult {
  value: string
  action: 'continue' | 'done' | 'abort'
}

// What one keypress does to the line being typed. Only a single printable character is added: arrow
// keys, function keys and other escape sequences arrive as multi-character text and are dropped.
export const applyKey = (
  value: string,
  text: string | undefined,
  key: Key | undefined,
): KeyResult => {
  if (key?.ctrl === true && key.name === 'c') return { value, action: 'abort' }
  if (key?.name === 'return' || key?.name === 'enter' || (key?.ctrl === true && key.name === 'd')) {
    return { value, action: 'done' }
  }
  if (key?.name === 'backspace') return { value: value.slice(0, -1), action: 'continue' }
  const modified = key?.ctrl === true || key?.meta === true
  if (text?.length !== 1 || modified || text < ' ' || text === '\x7f') {
    return { value, action: 'continue' }
  }
  return { value: value + text, action: 'continue' }
}

// Reads one line from the terminal without echoing it. Never from argv or the environment.
export const readHidden = (prompt: string): Promise<string> =>
  new Promise((resolve) => {
    const input = process.stdin
    emitKeypressEvents(input)
    input.setRawMode(true)
    input.resume()
    process.stderr.write(prompt)
    let value = ''
    const finish = (): void => {
      input.removeListener('keypress', onKey)
      input.setRawMode(false)
      input.pause()
      process.stderr.write('\n')
      resolve(value)
    }
    const onKey = (text: string | undefined, key: Key | undefined): void => {
      const next = applyKey(value, text, key)
      value = next.value
      if (next.action === 'abort') {
        input.setRawMode(false)
        process.stderr.write('\n')
        process.exit(130)
      }
      if (next.action === 'done') finish()
    }
    input.on('keypress', onKey)
  })

// --- Setting the token up ---------------------------------------------------------------------

export interface TokenResult {
  token: string
  changed: boolean
  // The old raw token file that was converted into op.env and deleted.
  migratedFrom?: string
  // An old raw token file that is still there next to op.env (op.env wins; the user removes the old file).
  legacyLeft?: string
}

export const legacyTokenPath = (dir: string): string => join(dir, 'op-service-account-token')

// The line the earlier setup appended to ~/.bashrc; the user removes it, setup never edits a profile.
export const legacyBashrcLine = (legacyFile: string): string =>
  `[ -r "${legacyFile}" ] && export OP_SERVICE_ACCOUNT_TOKEN="$(cat "${legacyFile}")"`

// Makes sure the token file exists, owner-only in an owner-only folder, and returns the token. Asks for
// it once when the file is missing. `ask` is the hidden prompt; it is only called when there is a terminal.
export const setupToken = async (input: {
  dir: string
  file: string
  deps: PrivateDeps
  hasTerminal: boolean
  ask: () => Promise<string>
}): Promise<TokenResult> => {
  const { dir, file, deps } = input
  let changed = false
  // The folder is private whether or not the file exists yet; another user's folder stops setup.
  if (existsSync(dir) && ensurePrivate(dir, 'dir', deps)) changed = true
  const write = (token: string): void => {
    if (!existsSync(dir)) mkdirSync(dir, { recursive: true, mode: 0o700 })
    ensurePrivate(dir, 'dir', deps)
    // Written in the private folder through a temp file and a rename.
    const temp = join(dirname(file), '.op.env.tmp')
    writeFileSync(temp, formatTokenEnv(token), { mode: 0o600 })
    renameSync(temp, file)
    ensurePrivate(file, 'file', deps)
  }
  if (!existsSync(file)) {
    // A token file of an earlier setup (the raw token, read by a ~/.bashrc line): converted once, no prompt.
    const legacy = legacyTokenPath(dir)
    if (existsSync(legacy)) {
      ensurePrivate(legacy, 'file', deps)
      const token = normalizeToken(readFileSync(legacy, 'utf8'))
      write(token)
      rmSync(legacy)
      return { token, changed: true, migratedFrom: legacy }
    }
    if (!input.hasTerminal) {
      return fail(
        `the token file ${file} is missing and there is no terminal to ask for it. Run setup again in a terminal, or put the token there yourself (${TOKEN_KEY}=<token>, readable by you only).`,
      )
    }
    const token = normalizeToken(await input.ask())
    write(token)
    return { token, changed: true }
  }
  if (ensurePrivate(file, 'file', deps)) changed = true
  const token = parseTokenEnv(readFileSync(file, 'utf8'))
  if (token === undefined) {
    return fail(`the token file ${file} has no ${TOKEN_KEY} line. Remove it and run setup again.`)
  }
  const legacyLeft = legacyTokenPath(dir)
  if (existsSync(legacyLeft)) return { token, changed, legacyLeft }
  return { token, changed }
}
