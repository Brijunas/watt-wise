import type { Plugin } from 'vite'

const configPath = '/config.js'

const missingBody = (): string =>
  `throw new Error(${JSON.stringify(
    'API_BASE_URL is not set. Start the server through 1Password: ' +
      'op run --env-file <app>/.env.op.development -- pnpm --filter <app> dev',
  )})`

// Serves `/config.js` from the process environment in `vite dev` and `vite preview`. Deployed images
// render the same file with nginx. Nothing is written to `dist/`, so a build never needs the settings.
export const runtimeConfig = (): Plugin => {
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
    res.setHeader('Content-Type', 'text/javascript; charset=utf-8')
    res.setHeader('Cache-Control', 'no-cache')
    if (!apiBaseUrl) {
      res.statusCode = 500
      res.end(missingBody())
      return
    }
    res.statusCode = 200
    res.end(`window.__WATTWISE_CONFIG__ = ${JSON.stringify({ apiBaseUrl })};`)
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
