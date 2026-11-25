// The tools and dependencies steps: mise itself, the tool versions in mise.toml, `pnpm install` and
// `dotnet tool restore`. pnpm and dotnet run their dependencies' lifecycle scripts, which have no use
// for the 1Password token, so they start without it (see toolEnv).
import { createHash } from 'node:crypto'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { nugetDir, nugetState } from './nuget.ts'
import { withoutToken } from './op.ts'
import type { Env, Runner } from './run.ts'
import { readSettings, writeSettings } from './settings.ts'
import { fail, step } from './steps.ts'

const firstLine = (text: string): string =>
  text
    .split(/\r?\n/)
    .find((line) => line.trim() !== '')
    ?.trim() ?? ''

// --- mise itself ------------------------------------------------------------------------------

const NOT_AVAILABLE = 'self-update is not available for this install'

// `mise doctor` says whether this install can update itself (a package-manager install can't).
export const selfUpdateAvailable = (runner: Runner): boolean | undefined => {
  const doctor = runner('mise', ['doctor', '--json'])
  if (doctor.status !== 0) return undefined
  try {
    const value = (JSON.parse(doctor.stdout) as { self_update_available?: unknown })
      .self_update_available
    if (typeof value === 'boolean') return value
    if (value === 'yes' || value === 'no') return value === 'yes'
  } catch {
    // unknown
  }
  return undefined
}

export const miseSelfUpdate = (runner: Runner, cwd: string): void => {
  const version = (): string => runner('mise', ['--version']).stdout.trim()
  if (selfUpdateAvailable(runner) === false) {
    step('mise', `skipped (${NOT_AVAILABLE})`)
    return
  }
  const before = version()
  const update = runner('mise', ['self-update', '--yes'], { cwd })
  if (update.status !== 0) {
    const output = `${update.stderr}\n${update.stdout}`
    if (/self-update is disabled for this install/i.test(output)) {
      step('mise', `skipped (${NOT_AVAILABLE})`)
    } else {
      step(
        'mise',
        `skipped (self-update failed: ${firstLine(update.stderr) || firstLine(update.stdout)})`,
      )
    }
    return
  }
  step('mise', version() === before ? 'unchanged' : 'changed')
}

// --- pnpm and dotnet without the token --------------------------------------------------------

// mise's environment (the tools' PATH and variables) without the service-account token. mise applies
// `[env]`, and with it the token file, to everything run through `mise exec`, so these two programs are
// started directly with this environment instead.
export const toolEnv = (runner: Runner, cwd: string, base: Env): Env => {
  const result = runner('mise', ['env', '--json'], { cwd })
  if (result.status !== 0) return fail(`mise env failed: ${firstLine(result.stderr)}`)
  let vars: Record<string, unknown>
  try {
    vars = JSON.parse(result.stdout) as Record<string, unknown>
  } catch {
    return fail('mise env printed something that is not JSON.')
  }
  const env: Env = { ...base }
  const unset = new Set<string>()
  for (const [key, value] of Object.entries(vars)) {
    if (typeof value === 'string') env[key] = value
    // mise prints null for a variable its config unsets.
    else if (value === null) unset.add(key)
  }
  return withoutToken(Object.fromEntries(Object.entries(env).filter(([key]) => !unset.has(key))))
}

export interface Command {
  command: string
  args: string[]
  // Windows: the arguments are already one quoted command line and must reach cmd.exe as they are.
  verbatim?: boolean
}

// A .cmd or .bat shim on Windows can't be spawned directly: cmd.exe runs it, with arguments that are fixed
// text of this script. Everything else is spawned as it is.
// The command line is built here and handed over verbatim: `cmd /d /s /c ""<path>" <args>"`, with /s
// removing the outer pair of quotes, so a path with spaces, & or ^ reaches cmd.exe inside its own quotes.
// cmd.exe still expands %VAR% inside quotes, so a path containing %...% would be changed; a mise install
// path doesn't contain one.
export const toolCommand = (
  path: string,
  args: readonly string[],
  platform: string,
  comspec?: string,
): Command =>
  platform === 'win32' && /\.(cmd|bat)$/i.test(path)
    ? {
        command: comspec ?? 'cmd.exe',
        args: ['/d', '/s', '/c', `""${path}" ${args.join(' ')}"`],
        verbatim: true,
      }
    : { command: path, args: [...args] }

const runTool = (
  runner: Runner,
  name: string,
  args: readonly string[],
  context: { cwd: string; env: Env; platform: string },
  capture: boolean,
): { status: number | null; stdout: string; stderr: string } => {
  const which = runner('mise', ['which', name], { cwd: context.cwd })
  const path = which.stdout.trim()
  if (which.status !== 0 || path === '')
    return fail(`mise can't find ${name}: run setup again after mise install.`)
  const {
    command,
    args: commandArgs,
    verbatim,
  } = toolCommand(path, args, context.platform, context.env.ComSpec)
  return runner(command, commandArgs, {
    cwd: context.cwd,
    env: context.env,
    inherit: !capture,
    windowsVerbatimArguments: verbatim,
  })
}

// --- The steps --------------------------------------------------------------------------------

export interface DependencyOptions {
  runner: Runner
  root: string
  platform: string
  // The environment setup itself runs in.
  env: Env
  nugetPackages?: string
  // Where setup.json is: the installed tools are remembered there.
  settingsDir: string
}

// A hash of what `mise ls --installed` lists (sorted, so the order doesn't matter).
export const toolsHash = (listing: string): string =>
  createHash('sha256')
    .update(
      listing
        .split(/\r?\n/)
        .filter((line) => line.trim() !== '')
        .sort()
        .join('\n'),
    )
    .digest('hex')

const fingerprint = (path: string): string =>
  existsSync(path) ? createHash('sha256').update(readFileSync(path)).digest('hex') : ''

export const installDependencies = (options: DependencyOptions): void => {
  const { runner, root, platform } = options
  miseSelfUpdate(runner, root)

  // `mise run` installs the missing tools before the task starts, so a before/after listing never sees a
  // change. The state is remembered instead: changed when the installed tools differ from the last run.
  if (runner('mise', ['install'], { cwd: root, inherit: true }).status !== 0)
    fail('mise install failed.')
  const installed = toolsHash(runner('mise', ['ls', '--installed'], { cwd: root }).stdout)
  const saved = readSettings(options.settingsDir)
  if (saved.tools === installed) {
    step('tools', 'unchanged')
  } else {
    writeSettings(options.settingsDir, { ...saved, tools: installed })
    step('tools', 'changed')
  }

  const env = toolEnv(runner, root, options.env)
  const context = { cwd: root, env, platform }

  // pnpm's `prepare` script installs the Husky hooks. Its installed lockfile copy changes when packages do.
  const pnpmState = join(root, 'node_modules', '.pnpm', 'lock.yaml')
  const pnpmBefore = fingerprint(pnpmState)
  const pnpm = runTool(runner, 'pnpm', ['install', '--frozen-lockfile'], context, false)
  if (pnpm.status !== 0) fail('pnpm install failed.')
  step('pnpm', pnpmBefore === fingerprint(pnpmState) ? 'unchanged' : 'changed')

  const nuget = nugetDir({ ...env, NUGET_PACKAGES: options.nugetPackages ?? env.NUGET_PACKAGES })
  const nugetBefore = nugetState(nuget)
  const restore = runTool(runner, 'dotnet', ['tool', 'restore'], context, true)
  process.stdout.write(restore.stdout)
  process.stderr.write(restore.stderr)
  if (restore.status !== 0) fail('dotnet tool restore failed.')
  step('dotnet-tools', nugetBefore === nugetState(nuget) ? 'unchanged' : 'changed')
}
