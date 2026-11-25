// A command runner: every step that starts a program goes through one, so the unit tests can stub it.
import { spawnSync } from 'node:child_process'

export type Env = Record<string, string | undefined>

export interface RunOptions {
  cwd?: string
  env?: Env
  // Pass the terminal through (output shows live, nothing is captured).
  inherit?: boolean
  timeoutMs?: number
  // Windows: pass the arguments to the program as they are, without Node's quoting.
  windowsVerbatimArguments?: boolean
}

export interface RunResult {
  status: number | null
  stdout: string
  stderr: string
}

export type Runner = (command: string, args: readonly string[], options?: RunOptions) => RunResult

export const run: Runner = (command, args, options = {}) => {
  const result = spawnSync(command, [...args], {
    cwd: options.cwd,
    env: options.env ?? process.env,
    encoding: 'utf8',
    timeout: options.timeoutMs,
    windowsVerbatimArguments: options.windowsVerbatimArguments,
    stdio: options.inherit === true ? 'inherit' : ['ignore', 'pipe', 'pipe'],
  })
  const stderr = typeof result.stderr === 'string' ? result.stderr : ''
  return {
    status: result.status,
    stdout: typeof result.stdout === 'string' ? result.stdout : '',
    // A program that can't start (not installed) has no status; its error is the message.
    stderr: stderr === '' && result.error ? result.error.message : stderr,
  }
}

export const succeeds = (runner: Runner, command: string, args: readonly string[]): boolean =>
  runner(command, args).status === 0
