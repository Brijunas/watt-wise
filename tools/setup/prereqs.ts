// What setup can't install for you: git, Docker with Compose 2.22 or newer, the 1Password CLI (`op`,
// from 1Password's own package, never from mise), mise activated in the shell,
// ICU on Linux (.NET needs it), Git for Windows on Windows. Each missing one is listed with the official
// install page, and setup stops after listing them all.
import { existsSync, readdirSync } from 'node:fs'
import { posix, win32 } from 'node:path'
import { run as defaultRun } from './run.ts'
import type { Env, Runner } from './run.ts'
import { fail } from './steps.ts'

export const MIN_COMPOSE: readonly [number, number] = [2, 22]

export const LINKS = {
  git: 'https://git-scm.com/downloads',
  gitForWindows: 'https://gitforwindows.org',
  dockerLinux: 'https://docs.docker.com/engine/install/',
  dockerDesktop: 'https://docs.docker.com/desktop/',
  compose: 'https://docs.docker.com/compose/install/',
  miseInstall: 'https://mise.jdx.dev/getting-started.html',
  miseActivate: 'https://mise.jdx.dev/getting-started.html#activate-mise',
  icu: 'https://learn.microsoft.com/dotnet/core/install/linux',
  op: 'https://developer.1password.com/docs/cli/get-started/',
}

// 'v2.40.3', '2.22.0-desktop.1' and '2.22' all parse; anything else gives undefined.
export const parseComposeVersion = (output: string): [number, number] | undefined => {
  const match = /^v?(\d+)\.(\d+)/.exec(output.trim())
  if (match?.[1] === undefined || match[2] === undefined) return undefined
  return [Number(match[1]), Number(match[2])]
}

export const composeIsRecent = (version: readonly [number, number]): boolean =>
  version[0] > MIN_COMPOSE[0] || (version[0] === MIN_COMPOSE[0] && version[1] >= MIN_COMPOSE[1])

// Is a libicuuc shared library installed? Looks in the loader cache, then in the usual folders.
// .NET's invariant mode (DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1) needs none.
export const hasIcu = (runner: Runner, env: Env): boolean => {
  if (
    env.DOTNET_SYSTEM_GLOBALIZATION_INVARIANT === '1' ||
    env.DOTNET_SYSTEM_GLOBALIZATION_INVARIANT === 'true'
  ) {
    return true
  }
  if (runner('ldconfig', ['-p']).stdout.includes('libicuuc.so')) return true
  const folders = [
    '/usr/lib',
    '/usr/lib64',
    '/lib64',
    '/usr/local/lib',
    '/usr/lib/x86_64-linux-gnu',
    '/usr/lib/aarch64-linux-gnu',
  ]
  return folders.some(
    (folder) =>
      existsSync(folder) && readdirSync(folder).some((name) => name.startsWith('libicuuc.so')),
  )
}

// Is mise activated in this shell? `mise doctor` knows; the variables its activation sets are the fallback.
export const miseIsActivated = (runner: Runner, env: Env): boolean => {
  const doctor = runner('mise', ['doctor', '--json'])
  if (doctor.status === 0) {
    try {
      const report = JSON.parse(doctor.stdout) as { activated?: unknown }
      if (typeof report.activated === 'boolean') return report.activated
    } catch {
      // fall through to the variables
    }
  }
  return env.__MISE_SESSION !== undefined || env.__MISE_DIFF !== undefined
}

// The first file called `name` in the PATH folders (PATHEXT on Windows), as the shell would find it.
export const findOnPath = (
  name: string,
  env: Env,
  platform: string,
  exists: (path: string) => boolean = existsSync,
): string | undefined => {
  const pathKey = Object.keys(env).find((key) => key.toLowerCase() === 'path')
  const folders = (pathKey === undefined ? '' : (env[pathKey] ?? '')).split(
    platform === 'win32' ? ';' : ':',
  )
  const extensions = platform === 'win32' ? (env.PATHEXT ?? '.EXE;.CMD;.BAT;.COM').split(';') : ['']
  const joiner = platform === 'win32' ? win32 : posix
  for (const folder of folders) {
    if (folder === '') continue
    for (const extension of extensions) {
      const candidate = joiner.join(folder, `${name}${extension}`)
      if (exists(candidate)) return candidate
    }
  }
  return undefined
}

export interface PrereqOptions {
  // The 1Password CLI is needed unless the secrets steps are skipped (--skip-secrets).
  op?: boolean
  exists?: (path: string) => boolean
}

export const prerequisiteProblems = (
  platform: string,
  runner: Runner = defaultRun,
  env: Env = process.env,
  options: PrereqOptions = {},
): string[] => {
  const problems: string[] = []
  if (options.op ?? true) {
    if (findOnPath('op', env, platform, options.exists) === undefined) {
      problems.push(`the 1Password CLI ('op') is not installed: ${LINKS.op}`)
    }
  }
  const gitVersion = runner('git', ['--version'])
  if (gitVersion.status !== 0) {
    problems.push(`git is not installed: ${platform === 'win32' ? LINKS.gitForWindows : LINKS.git}`)
  } else if (platform === 'win32' && !/windows/i.test(gitVersion.stdout)) {
    problems.push(
      `git is not Git for Windows (the repo's hooks and line endings expect it): ${LINKS.gitForWindows}`,
    )
  }
  const dockerInstall = platform === 'linux' ? LINKS.dockerLinux : LINKS.dockerDesktop
  if (runner('docker', ['--version']).status !== 0) {
    problems.push(`Docker is not installed (the 'docker' command is missing): ${dockerInstall}`)
  } else {
    const compose = runner('docker', ['compose', 'version', '--short'])
    const version = compose.status === 0 ? parseComposeVersion(compose.stdout) : undefined
    if (version === undefined) {
      problems.push(
        `'docker compose version' fails; install the Compose plugin (2.22 or newer): ${LINKS.compose}`,
      )
    } else if (!composeIsRecent(version)) {
      problems.push(
        `Docker Compose is ${version.join('.')}; 2.22 or newer is needed: ${LINKS.compose}`,
      )
    }
  }
  if (runner('mise', ['--version']).status !== 0) {
    problems.push(`mise is not installed: ${LINKS.miseInstall}`)
  } else if (!miseIsActivated(runner, env)) {
    problems.push(
      `mise is not activated in this shell (add its activate line to your shell's startup file): ${LINKS.miseActivate}`,
    )
  }
  if (platform === 'linux' && !hasIcu(runner, env)) {
    problems.push(
      `the ICU library (libicu) that .NET needs is missing; install your distribution's ICU package: ${LINKS.icu}`,
    )
  }
  return problems
}

export const checkPrereqs = (
  platform: string,
  runner: Runner = defaultRun,
  env: Env = process.env,
  options: PrereqOptions = {},
): void => {
  const problems = prerequisiteProblems(platform, runner, env, options)
  if (problems.length > 0) {
    fail(
      // Every line carries the `setup:` prefix (the first one is added when the error is printed).
      `these prerequisites are missing or need a fix:\n${problems.map((problem) => `setup:   - ${problem}`).join('\n')}`,
    )
  }
}
