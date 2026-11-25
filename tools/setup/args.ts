// setup's command line.
import type { PollingFlag } from './polling.ts'

export const USAGE = `Usage: mise run setup [-- <options>]
       node tools/setup/setup.ts [<options>]
       node tools/setup/setup.ts --derive-url <APP> <url>
       node tools/setup/setup.ts --check-bind <KEY> <address>

  --polling on|off|auto   Whether the containers poll for file changes (macOS, and Windows outside WSL,
                          need it). on|off is saved; auto drops the saved choice and uses the rule.
  --skip-secrets          Skip the 1Password service-account token and deploy/.env (CI, or a machine
                          without vault access).
  --derive-url            Print the published bind address and port derived from an app's url, or stop
                          with a message (what the deploy/.env step does for each app).
  --check-bind            Stop with a message unless the address is a loopback address (what the
                          deploy/.env step does for the postgres, grafana, otlp, mailpit-smtp and
                          mailpit bind addresses). Prints nothing when it passes.
  -h, --help              Show this help.
`

export type Parsed =
  | { kind: 'help' }
  | { kind: 'derive-url'; app: string; url: string }
  | { kind: 'check-bind'; key: string; address: string }
  | { kind: 'run'; polling?: PollingFlag; skipSecrets: boolean }
  | { kind: 'usage-error'; message: string }

export const parseArgs = (argv: readonly string[]): Parsed => {
  if (argv[0] === '--derive-url') {
    const [, app, url] = argv
    if (argv.length !== 3 || app === undefined || url === undefined) {
      return {
        kind: 'usage-error',
        message: 'usage: node tools/setup/setup.ts --derive-url <APP> <url>',
      }
    }
    return { kind: 'derive-url', app, url }
  }
  if (argv[0] === '--check-bind') {
    const [, key, address] = argv
    if (argv.length !== 3 || key === undefined || address === undefined) {
      return {
        kind: 'usage-error',
        message: 'usage: node tools/setup/setup.ts --check-bind <KEY> <address>',
      }
    }
    return { kind: 'check-bind', key, address }
  }
  let polling: PollingFlag | undefined
  let skipSecrets = false
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index] ?? ''
    const [name, inline] = arg.startsWith('--') ? arg.split(/=(.*)/s, 2) : [arg, undefined]
    // The value of an option: after `=` or the next argument.
    const value = (): string | undefined => {
      if (inline !== undefined) return inline
      index += 1
      return argv[index]
    }
    if (name === '-h' || name === '--help') return { kind: 'help' }
    if (name === '--skip-secrets') {
      skipSecrets = true
    } else if (name === '--polling') {
      const given = value()
      if (given !== 'on' && given !== 'off' && given !== 'auto') {
        return { kind: 'usage-error', message: '--polling takes on, off or auto.' }
      }
      polling = given
    } else {
      return { kind: 'usage-error', message: `unknown option: ${arg}` }
    }
  }
  return { kind: 'run', polling, skipSecrets }
}
