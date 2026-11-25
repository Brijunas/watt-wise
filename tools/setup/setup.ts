// Sets up a machine for Watt-Wise from a fresh `git clone`: the tool versions in mise.toml, the JS and
// .NET dependencies, the 1Password service-account token and deploy/.env. Run it with `mise run setup`; it uses
// Node's built-in modules only and runs as TypeScript through Node's type stripping.
// It is safe to rerun: every step prints `setup: <step>: changed|unchanged|skipped`, and a rerun changes
// only what is missing or out of date. What it does and why is in docs/setup.md.
import { resolve } from 'node:path'
import { parseArgs, USAGE } from './args.ts'
import { installDependencies } from './dependencies.ts'
import { fedoraProfile, isFedora, readOsRelease } from './fedora.ts'
import { defaultPrivateDeps, legacyBashrcLine, readHidden, setupToken } from './op.ts'
import { resolvePolling } from './polling.ts'
import { checkPrereqs } from './prereqs.ts'
import { checkBind, derivePublished, formatPublished, renderEnv } from './render-env.ts'
import { tokenEnvPath, configDir } from './paths.ts'
import { run } from './run.ts'
import type { Env } from './run.ts'
import { readSettings, writeSettings } from './settings.ts'
import type { Settings } from './settings.ts'
import { SetupError, fail, note, step } from './steps.ts'
import { checkTestcontainers } from './testcontainers.ts'

const root = resolve(import.meta.dirname, '..', '..')

const main = async (): Promise<void> => {
  const parsed = parseArgs(process.argv.slice(2))
  switch (parsed.kind) {
    case 'help':
      process.stdout.write(USAGE)
      return
    case 'usage-error':
      process.stderr.write(`setup: ${parsed.message}\n\n${USAGE}`)
      process.exitCode = 2
      return
    case 'derive-url':
      process.stdout.write(
        `${formatPublished(parsed.app, derivePublished(parsed.app, parsed.url))}\n`,
      )
      return
    case 'check-bind':
      checkBind(parsed.key, parsed.address)
      return
    case 'run':
      break
  }

  // --- Preflight ----------------------------------------------------------------------------
  if (typeof process.getuid === 'function' && process.getuid() === 0) {
    fail('run as your own user, not as root.')
  }
  process.chdir(root)
  const platform = process.platform

  // The one distribution-specific step, before the prerequisite check: it installs what the tools need.
  if (platform === 'linux' && isFedora(readOsRelease())) fedoraProfile()
  // op is only needed when the 1Password steps run.
  checkPrereqs(platform, undefined, undefined, { op: !parsed.skipSecrets })
  step('prereqs', 'unchanged')

  // --- Tools and dependencies ---------------------------------------------------------------
  installDependencies({
    runner: run,
    root,
    platform,
    env: { ...process.env },
    settingsDir: configDir(),
  })

  // --- 1Password sign-in and deploy/.env ----------------------------------------------------
  const dir = configDir()
  const saved = readSettings(dir)
  const polling = resolvePolling({
    flag: parsed.polling,
    saved: saved.polling,
    platform,
    repoPath: root,
  })
  const env: Env = { ...process.env }

  // What this run decides is saved at the end, and the sign-in only after it worked.
  const nextSaved: Settings = { ...saved }
  if (parsed.polling === 'auto') delete nextSaved.polling
  else if (parsed.polling !== undefined) nextSaved.polling = parsed.polling

  if (parsed.skipSecrets) {
    step('token', 'skipped (--skip-secrets)')
    step('deploy/.env', 'skipped (--skip-secrets)')
  } else {
    // One flow on every OS: the Development service-account token, asked for once.
    const tokenFile = tokenEnvPath(dir)
    const token = await setupToken({
      dir,
      file: tokenFile,
      deps: defaultPrivateDeps(run),
      hasTerminal: process.stdin.isTTY,
      ask: () => readHidden('Paste the Development service-account token (hidden): '),
    })
    step('token', token.changed ? 'changed' : 'unchanged')
    if (token.migratedFrom !== undefined) {
      note(`token: moved ${token.migratedFrom} into ${tokenFile} and deleted the old file.`)
      note('token: remove this line from your shell profile (~/.bashrc), setup never edits it:')
      console.log(`  ${legacyBashrcLine(token.migratedFrom)}`)
    }
    if (token.legacyLeft !== undefined) {
      note(`token: ${tokenFile} is used; delete the old file ${token.legacyLeft} yourself.`)
      note('token: and remove this line from your shell profile (~/.bashrc), setup never edits it:')
      console.log(`  ${legacyBashrcLine(token.legacyLeft)}`)
    }
    renderEnv({
      root,
      polling: polling.enabled,
      tokenFile,
      env: { ...env, OP_SERVICE_ACCOUNT_TOKEN: token.token },
    })
  }
  if (!parsed.skipSecrets || parsed.polling !== undefined) writeSettings(dir, nextSaved)
  note(`polling: ${polling.enabled ? 'on' : 'off'} (${polling.source})`)

  // --- Testcontainers -----------------------------------------------------------------------
  checkTestcontainers()

  note('done.')
  note('next: the first start is `mise run up` (docs/setup.md).')
}

try {
  await main()
} catch (error) {
  if (error instanceof SetupError) {
    process.stderr.write(`setup: ${error.message}\n`)
    process.exitCode = 1
  } else {
    throw error
  }
}
