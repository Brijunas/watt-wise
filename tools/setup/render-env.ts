// deploy/.env: the Development stack's non-secret settings, rendered from 1Password with `op inject`,
// checked, and extended with the apps' published address and port and the file-watching mode. Compared
// with the file on disk and written only when different.
import { randomBytes } from 'node:crypto'
import { existsSync, readFileSync, renameSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { run as defaultRun } from './run.ts'
import type { Env, Runner } from './run.ts'
import { fail, step } from './steps.ts'

export const APPS = ['API', 'JOBS', 'FRONTEND', 'ADMIN'] as const

// The five infrastructure bind addresses and the 1Password item each one comes from.
export const BIND_ITEMS: Readonly<Record<string, string>> = {
  POSTGRES_BIND_ADDRESS: 'postgres-admin',
  GRAFANA_BIND_ADDRESS: 'grafana',
  OTLP_BIND_ADDRESS: 'otlp',
  MAILPIT_SMTP_BIND_ADDRESS: 'mailpit-smtp',
  MAILPIT_BIND_ADDRESS: 'mailpit',
}

export interface Published {
  bindAddress: string
  port: string
}

// Derives the host side of an app's published port from its 1Password `url` field. Only a bare
// loopback origin passes: http://localhost, http://127.0.0.1 or http://[::1] (lowercase) and a port,
// nothing after it, not even a slash, because the same value is the apps' API_BASE_URL, APP_URL and
// the CORS origins, which accept only an origin. So a wrong value can't publish an app on a public
// address. localhost becomes 127.0.0.1 and [::1] becomes ::1, because Docker binds an address, not a
// name. The port is 1 to 65535 with no leading zero.
export const derivePublished = (app: string, url: string): Published => {
  if (!(APPS as readonly string[]).includes(app)) {
    return fail(`unknown app '${app}': expected API, JOBS, FRONTEND or ADMIN.`)
  }
  const item = app.toLowerCase()
  const match = /^http:\/\/(localhost|127\.0\.0\.1|\[::1\]):([0-9]+)$/.exec(url)
  if (match === null) {
    return fail(
      `the ${app}_URL value (the 1Password '${item}' item's url) is not exactly an http origin on localhost, 127.0.0.1 or [::1] with a port: lowercase, no path, no trailing slash. Fix the item.`,
    )
  }
  const host = match[1] ?? ''
  const port = match[2] ?? ''
  if (!/^[1-9][0-9]{0,4}$/.test(port) || Number(port) > 65535) {
    return fail(
      `the port of ${app}_URL (the 1Password '${item}' item's url) is not a number from 1 to 65535 without a leading zero.`,
    )
  }
  const bindAddress = host === 'localhost' ? '127.0.0.1' : host === '[::1]' ? '::1' : host
  return { bindAddress, port }
}

// The two deploy/.env lines of an app.
export const formatPublished = (app: string, published: Published): string =>
  `${app}_BIND_ADDRESS="${published.bindAddress}"\n${app}_PORT="${published.port}"`

// Stops unless a bind address of the infrastructure services is a loopback address: 127.x.x.x
// (octets 0 to 255, no leading zero) or ::1. These values come straight from the vault and nothing
// else checks them, so a wrong one would publish PostgreSQL, Grafana or Mailpit on every interface.
export const checkBind = (key: string, address: string): void => {
  const item = BIND_ITEMS[key]
  if (item === undefined) {
    return fail(
      `unknown key '${key}': expected POSTGRES_BIND_ADDRESS, GRAFANA_BIND_ADDRESS, OTLP_BIND_ADDRESS, MAILPIT_SMTP_BIND_ADDRESS or MAILPIT_BIND_ADDRESS.`,
    )
  }
  if (address === '::1') return
  const match = /^127\.(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})$/.exec(address)
  if (match?.slice(1).every((octet) => Number(octet) <= 255) === true) return
  fail(
    `the ${key} value (the 1Password '${item}' item's server) is not a loopback address: 127.x.x.x or ::1. Fix the item.`,
  )
}

// The value of KEY="value" (or KEY=value) in rendered dotenv text; undefined when the key is missing.
export const renderedValue = (rendered: string, key: string): string | undefined => {
  const line = rendered.split(/\r?\n/).find((candidate) => candidate.startsWith(`${key}=`))
  if (line === undefined) return undefined
  let value = line.slice(key.length + 1)
  if (value.startsWith('"')) value = value.slice(1)
  if (value.endsWith('"')) value = value.slice(0, -1)
  return value
}

// Checks what op inject rendered and returns the text for deploy/.env: the rendered settings, the
// four apps' published bind address and port derived from their URLs, and the file-watching mode.
// The extra lines are part of the text that is compared, so an unchanged vault stays "unchanged".
export const composeEnv = (rendered: string, polling: boolean): string => {
  for (const key of Object.keys(BIND_ITEMS)) {
    const value = renderedValue(rendered, key)
    if (value === undefined) return fail(`op inject rendered no ${key}.`)
    checkBind(key, value)
  }
  let text = rendered.endsWith('\n') ? rendered : `${rendered}\n`
  for (const app of APPS) {
    const url = renderedValue(rendered, `${app}_URL`)
    if (url === undefined) return fail(`op inject rendered no ${app}_URL.`)
    text += `${formatPublished(app, derivePublished(app, url))}\n`
  }
  return `${text}WATCH_POLLING="${polling ? 'true' : 'false'}"\n`
}

export interface RenderOptions {
  root: string
  polling: boolean
  tokenFile: string
  env: Env
  runner?: Runner
}

export const renderEnv = (options: RenderOptions): void => {
  const runner = options.runner ?? defaultRun
  const target = join(options.root, 'deploy', '.env')
  const template = join(options.root, 'deploy', 'development', 'compose.settings.tpl')
  const result = runner('op', ['inject', '-i', template], { env: options.env })
  if (result.status !== 0) {
    return fail(
      `op inject failed: check the service-account token in ${options.tokenFile} (it must read the Watt Wise Development vault).`,
    )
  }
  const text = composeEnv(result.stdout, options.polling)
  if (existsSync(target) && readFileSync(target, 'utf8') === text) {
    step('deploy/.env', 'unchanged')
    return
  }
  // Written beside the target and renamed over it, owner-only like the file it replaces.
  const temp = `${target}.${randomBytes(6).toString('hex')}`
  writeFileSync(temp, text, { mode: 0o600 })
  renameSync(temp, target)
  step('deploy/.env', 'changed')
}
