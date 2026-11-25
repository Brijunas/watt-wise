import { existsSync, readFileSync, statSync } from 'node:fs'
import { resolve, sep } from 'node:path'
import type { Plugin } from 'vite'
import { checkOrigin, describeOriginProblem } from './runtime-config/origin.ts'

const configPath = '/config.js'

const missingBody = (setting: string): string =>
  `throw new Error(${JSON.stringify(
    `${setting} is not set. Start the server through 1Password: ` +
      'op run --env-file <app>/.env.op.development -- pnpm --filter <app> dev',
  )})`

export interface RuntimeConfigOptions {
  /** The admin app needs FRONTEND_URL too: a missing one answers 500 like a missing API_BASE_URL. */
  frontendUrl?: boolean
}

// Serves `/config.js` from the process environment in `vite dev` and `vite preview`. Deployed images
// render the same file with nginx. Nothing is written to `dist/`, so a build never needs the settings.
// `FRONTEND_URL` is served when set; with `frontendUrl: true` it is required.
export const runtimeConfig = (options: RuntimeConfigOptions = {}): Plugin => {
  const middleware = (
    req: { url?: string; method?: string },
    res: {
      statusCode: number
      setHeader: (name: string, value: string) => unknown
      end: (body: string) => unknown
    },
    next: () => void,
  ) => {
    const path = req.url?.split('?')[0]
    if (path !== configPath || (req.method !== 'GET' && req.method !== 'HEAD')) {
      next()
      return
    }
    const apiBaseUrl = process.env.API_BASE_URL
    const frontendUrl = process.env.FRONTEND_URL
    res.setHeader('Content-Type', 'text/javascript; charset=utf-8')
    res.setHeader('Cache-Control', 'no-cache')
    const missing = !apiBaseUrl
      ? 'API_BASE_URL'
      : options.frontendUrl && !frontendUrl
        ? 'FRONTEND_URL'
        : undefined
    if (missing) {
      res.statusCode = 500
      res.end(missingBody(missing))
      return
    }
    res.statusCode = 200
    const config = { apiBaseUrl, ...(frontendUrl && { frontendUrl }) }
    res.end(`window.__WATTWISE_CONFIG__ = ${JSON.stringify(config)};`)
  }

  return {
    name: 'wattwise:runtime-config',
    configureServer(server) {
      server.middlewares.use(middleware)
    },
    configurePreviewServer(server) {
      server.middlewares.use(middleware)
    },
  }
}

const appUrlPlaceholder = '${APP_URL}'

const appUrlMissing =
  'APP_URL is not set. Start the server through 1Password: ' +
  'op run --env-file <app>/.env.op.development -- pnpm --filter <app> dev'

// The validated origin of APP_URL, or the message that says why there is none (unset or invalid).
const readAppUrl = (): { origin: string } | { error: string } => {
  const value = process.env.APP_URL
  if (!value) return { error: appUrlMissing }
  const checked = checkOrigin(value)
  if ('problem' in checked) {
    return {
      error: `APP_URL is not valid: ${describeOriginProblem(checked.problem, 'APP_URL', value)}`,
    }
  }
  return checked
}

const wantsHtml = (accept: string | undefined): boolean =>
  accept === undefined || accept === '' || accept.includes('text/html') || accept.includes('*/*')

// Fills the `${APP_URL}` placeholder of `index.html` (the `og:url` tag) in `vite dev` and
// `vite preview`. `vite build` never substitutes: the deployed image renders the same placeholder
// with nginx's envsubst at container start. Without APP_URL the HTML request answers an error that
// names the command; the server still starts, because Vitest merges `vite.config.ts` without it.
// An invalid APP_URL throws when the plugin is created; one that turns invalid later answers like
// an unset one, with its own message.
export const appUrl = (): Plugin => {
  const initial = readAppUrl()
  if (process.env.APP_URL && 'error' in initial) throw new Error(initial.error)
  return {
    name: 'wattwise:app-url',
    transformIndexHtml: {
      order: 'pre',
      handler(html, ctx) {
        // Only the dev server has `ctx.server`; a build keeps the placeholder.
        if (!ctx.server) return html
        const resolved = readAppUrl()
        if ('error' in resolved) throw new Error(resolved.error)
        return html.replaceAll(appUrlPlaceholder, resolved.origin)
      },
    },
    // Preview doesn't run `transformIndexHtml`: it serves the built file as it is. The middleware
    // runs before Vite's static one and answers what Vite would answer with `index.html`: `/`,
    // `/index.html` and, as in Vite's SPA fallback, a GET or HEAD that accepts HTML (or anything)
    // for a path that is not a file in `outDir`.
    configurePreviewServer(server) {
      const outDir = resolve(server.config.root, server.config.build.outDir)
      server.middlewares.use((req, res, next) => {
        if ((req.method !== 'GET' && req.method !== 'HEAD') || !wantsHtml(req.headers.accept)) {
          next()
          return
        }
        let pathname: string
        try {
          pathname = decodeURIComponent((req.url ?? '/').split(/[?#]/)[0] ?? '/')
        } catch {
          next()
          return
        }
        const file = resolve(outDir, `.${pathname}`)
        const inside = file === outDir || file.startsWith(outDir + sep)
        const isFile = inside && existsSync(file) && statSync(file).isFile()
        const isIndex = pathname === '/' || pathname === '/index.html'
        if (!isIndex && (isFile || pathname === '/favicon.ico')) {
          next()
          return
        }
        res.setHeader('Content-Type', 'text/html; charset=utf-8')
        res.setHeader('Cache-Control', 'no-cache')
        const resolved = readAppUrl()
        if ('error' in resolved) {
          res.statusCode = 500
          res.end(resolved.error)
          return
        }
        res.statusCode = 200
        res.end(
          req.method === 'HEAD'
            ? undefined
            : readFileSync(resolve(outDir, 'index.html'), 'utf8').replaceAll(
                appUrlPlaceholder,
                resolved.origin,
              ),
        )
      })
    },
  }
}

const nodeModule = (names: readonly string[]): RegExp =>
  new RegExp(`node_modules[\\\\/](?:${names.join('|')})[\\\\/]`)

// Rolldown's `output.codeSplitting` for the apps' builds: the vendor libraries get their own
// chunks, so the app chunk stays under Vite's 500 kB warning and a code change doesn't invalidate
// the libraries' cached files.
export const vendorChunks = {
  groups: [
    {
      name: 'react',
      test: nodeModule(['react', 'react-dom', 'react-router', 'scheduler']),
      priority: 2,
    },
    { name: 'mui', test: nodeModule(['@mui', '@emotion']), priority: 1 },
  ],
}

// The address `vite dev` and `vite preview` listen on, taken from the app's own URL. The port is
// strict so a busy port fails loudly instead of moving to one the API's CORS doesn't allow. An URL
// without a port gets its scheme's default (80 or 443). Unset means Vite's defaults apply.
export const serverAddress = (
  appUrl?: string,
): { host: string; port: number; strictPort: true } | undefined => {
  if (!appUrl) return undefined
  let url: URL
  try {
    url = new URL(appUrl)
  } catch {
    throw new Error(`APP_URL is not a valid URL: ${JSON.stringify(appUrl)}`)
  }
  const port = url.port ? Number(url.port) : url.protocol === 'https:' ? 443 : 80
  return { host: url.hostname.replace(/^\[(.*)\]$/, '$1'), port, strictPort: true }
}
