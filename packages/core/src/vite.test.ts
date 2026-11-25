import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { appUrl, runtimeConfig, serverAddress, vendorChunks } from './vite'

type Middleware = (req: object, res: object, next: () => void) => void

function getMiddleware(
  hook: 'configureServer' | 'configurePreviewServer',
  options: { frontendUrl?: boolean } = {},
): Middleware {
  const plugin = runtimeConfig(options)
  let registered: Middleware | undefined
  const server = { middlewares: { use: (fn: Middleware) => (registered = fn) } }
  const fn = plugin[hook]
  if (typeof fn !== 'function') throw new Error(`${hook} is not a function`)
  void fn.call({} as never, server as never)
  if (!registered) throw new Error('No middleware registered')
  return registered
}

function call(middleware: Middleware, url: string, method = 'GET') {
  const headers: Record<string, string> = {}
  const res = {
    statusCode: 0,
    body: undefined as string | undefined,
    setHeader: (name: string, value: string) => (headers[name] = value),
    end: (body: string) => (res.body = body),
  }
  const next = vi.fn()
  middleware({ url, method }, res, next)
  return { res, headers, next }
}

afterEach(() => {
  vi.unstubAllEnvs()
})

describe.each(['configureServer', 'configurePreviewServer'] as const)(
  'runtimeConfig %s',
  (hook) => {
    it('serves the config from API_BASE_URL', () => {
      vi.stubEnv('API_BASE_URL', 'http://localhost:4999')
      const { res, headers, next } = call(getMiddleware(hook), '/config.js?x=1')
      expect(res.statusCode).toBe(200)
      expect(headers['Content-Type']).toBe('text/javascript; charset=utf-8')
      expect(headers['Cache-Control']).toBe('no-cache')
      expect(res.body).toBe('window.__WATTWISE_CONFIG__ = {"apiBaseUrl":"http://localhost:4999"};')
      expect(next).not.toHaveBeenCalled()
    })

    it('answers 500 with a throwing script when API_BASE_URL is unset', () => {
      vi.stubEnv('API_BASE_URL', '')
      const { res } = call(getMiddleware(hook), '/config.js')
      expect(res.statusCode).toBe(500)
      expect(res.body).toContain('throw new Error(')
      expect(res.body).toContain('op run --env-file')
    })

    it('serves FRONTEND_URL when it is set', () => {
      vi.stubEnv('API_BASE_URL', 'http://localhost:4999')
      vi.stubEnv('FRONTEND_URL', 'http://localhost:5174')
      const { res } = call(getMiddleware(hook), '/config.js')
      expect(res.body).toBe(
        'window.__WATTWISE_CONFIG__ = {"apiBaseUrl":"http://localhost:4999","frontendUrl":"http://localhost:5174"};',
      )
    })

    it('answers 500 naming FRONTEND_URL when the admin needs it and it is unset', () => {
      vi.stubEnv('API_BASE_URL', 'http://localhost:4999')
      vi.stubEnv('FRONTEND_URL', '')
      const { res } = call(getMiddleware(hook, { frontendUrl: true }), '/config.js')
      expect(res.statusCode).toBe(500)
      expect(res.body).toContain('FRONTEND_URL is not set')
      expect(res.body).toContain('op run --env-file')
    })

    it('serves both when the admin needs FRONTEND_URL and it is set', () => {
      vi.stubEnv('API_BASE_URL', 'http://localhost:4999')
      vi.stubEnv('FRONTEND_URL', 'http://localhost:5174')
      const { res } = call(getMiddleware(hook, { frontendUrl: true }), '/config.js')
      expect(res.statusCode).toBe(200)
      expect(res.body).toContain('"frontendUrl":"http://localhost:5174"')
    })

    it('checks the variable on each request', () => {
      const middleware = getMiddleware(hook)
      vi.stubEnv('API_BASE_URL', '')
      expect(call(middleware, '/config.js').res.statusCode).toBe(500)
      vi.stubEnv('API_BASE_URL', 'https://api.example.test')
      expect(call(middleware, '/config.js').res.statusCode).toBe(200)
    })

    it('passes other requests on', () => {
      const middleware = getMiddleware(hook)
      expect(call(middleware, '/index.html').next).toHaveBeenCalled()
      expect(call(middleware, '/config.js', 'POST').next).toHaveBeenCalled()
    })
  },
)

describe('runtimeConfig', () => {
  it('is named and does nothing at build time', () => {
    const plugin = runtimeConfig()
    expect(plugin.name).toBe('wattwise:runtime-config')
    expect(plugin.generateBundle).toBeUndefined()
    expect(plugin.transformIndexHtml).toBeUndefined()
  })
})

const notSet =
  'APP_URL is not set. Start the server through 1Password: ' +
  'op run --env-file <app>/.env.op.development -- pnpm --filter <app> dev'

const template = '<meta content="${APP_URL}/"><meta content="${APP_URL}/x">'

describe('appUrl factory', () => {
  it('is named', () => {
    vi.stubEnv('APP_URL', '')
    expect(appUrl().name).toBe('wattwise:app-url')
  })

  it.each([
    ['not a url', 'APP_URL is not valid: APP_URL "not a url" is not a URL'],
    [
      'http://example.com',
      'APP_URL is not valid: APP_URL must use https (http is allowed only for localhost)',
    ],
    ['https://u:p@example.com', 'APP_URL is not valid: APP_URL must not contain credentials'],
    [
      'https://example.com/?a=1',
      'APP_URL is not valid: APP_URL must not contain a query or a fragment',
    ],
    ['https://example.com/app', 'APP_URL is not valid: APP_URL must not contain a path'],
  ])('throws for %s', (value, message) => {
    vi.stubEnv('APP_URL', value)
    expect(() => appUrl()).toThrow(new Error(message))
  })
})

type HtmlHandler = (html: string, ctx: object) => string

const getHtmlHandler = (): HtmlHandler => {
  const hook = appUrl().transformIndexHtml
  if (typeof hook !== 'object' || !('handler' in hook)) {
    throw new Error('transformIndexHtml has no handler')
  }
  expect(hook.order).toBe('pre')
  return (html, ctx) => (hook.handler as HtmlHandler).call({} as never, html, ctx)
}

describe('appUrl transformIndexHtml', () => {
  it('leaves the html untouched in a build', () => {
    vi.stubEnv('APP_URL', 'https://app.example.test')
    expect(getHtmlHandler()(template, {})).toBe(template)
  })

  it('leaves the html untouched in a build without APP_URL', () => {
    vi.stubEnv('APP_URL', '')
    expect(getHtmlHandler()(template, {})).toBe(template)
  })

  it('fills every placeholder when serving and drops a trailing slash', () => {
    vi.stubEnv('APP_URL', 'https://app.example.test/')
    expect(getHtmlHandler()(template, { server: {} })).toBe(
      '<meta content="https://app.example.test/"><meta content="https://app.example.test/x">',
    )
  })

  it('throws the invalid message when APP_URL turns invalid after creation', () => {
    vi.stubEnv('APP_URL', 'https://app.example.test')
    const handler = getHtmlHandler()
    vi.stubEnv('APP_URL', 'https://app.example.test/app')
    expect(() => handler(template, { server: {} })).toThrow(
      new Error('APP_URL is not valid: APP_URL must not contain a path'),
    )
  })

  it('throws the not-set message when serving without APP_URL', () => {
    vi.stubEnv('APP_URL', '')
    expect(() => getHtmlHandler()(template, { server: {} })).toThrow(new Error(notSet))
  })
})

interface FakeResponse {
  statusCode: number
  headers: Record<string, string>
  body: string | undefined
  ended: boolean
  setHeader: (name: string, value: string) => void
  end: (body?: string) => void
}

describe('appUrl preview middleware', () => {
  let root: string

  beforeEach(() => {
    root = mkdtempSync(join(tmpdir(), 'wattwise-preview-'))
    mkdirSync(join(root, 'dist', 'assets'), { recursive: true })
    writeFileSync(join(root, 'dist', 'index.html'), template)
    writeFileSync(join(root, 'dist', 'assets', 'app.js'), 'console.log(1)')
    mkdirSync(join(root, 'dist2'))
    writeFileSync(join(root, 'dist2', 'x.js'), 'console.log(2)')
    writeFileSync(join(root, 'secret.txt'), 'TOP-SECRET')
    vi.stubEnv('APP_URL', 'https://app.example.test')
  })

  afterEach(() => {
    rmSync(root, { recursive: true, force: true })
  })

  // `envAfterCreation` replaces APP_URL after the plugin was created, before the request.
  const run = (url: string, method = 'GET', accept?: string, envAfterCreation?: string) => {
    const plugin = appUrl()
    if (envAfterCreation !== undefined) vi.stubEnv('APP_URL', envAfterCreation)
    const registered: ((req: object, res: object, next: () => void) => void)[] = []
    const server = {
      config: { root, build: { outDir: 'dist' } },
      middlewares: { use: (fn: (typeof registered)[number]) => registered.push(fn) },
    }
    const hook = plugin.configurePreviewServer
    if (typeof hook !== 'function') throw new Error('configurePreviewServer is not a function')
    void hook.call({} as never, server as never)
    expect(registered).toHaveLength(1)
    const res: FakeResponse = {
      statusCode: 0,
      headers: {},
      body: undefined,
      ended: false,
      setHeader: (name, value) => {
        res.headers[name] = value
      },
      end: (body) => {
        res.ended = true
        res.body = body
      },
    }
    const next = vi.fn()
    registered[0]?.({ url, method, headers: accept === undefined ? {} : { accept } }, res, next)
    return { res, next }
  }

  const filled =
    '<meta content="https://app.example.test/"><meta content="https://app.example.test/x">'

  it.each(['/', '/index.html', '/settings', '/a/b?x=1', '/a#frag', '/index.html?v=2'])(
    'serves %s filled with both headers',
    (url) => {
      const { res, next } = run(url)
      expect(res.statusCode).toBe(200)
      expect(res.headers).toEqual({
        'Content-Type': 'text/html; charset=utf-8',
        'Cache-Control': 'no-cache',
      })
      expect(res.body).toBe(filled)
      expect(next).not.toHaveBeenCalled()
    },
  )

  it.each(['text/html,application/xhtml+xml', '*/*', ''])('serves for Accept %j', (accept) => {
    expect(run('/settings', 'GET', accept).res.body).toBe(filled)
  })

  it('passes an existing file on', () => {
    const { res, next } = run('/assets/app.js')
    expect(next).toHaveBeenCalledTimes(1)
    expect(res.ended).toBe(false)
  })

  it('passes /favicon.ico on even when it does not exist', () => {
    const { res, next } = run('/favicon.ico')
    expect(next).toHaveBeenCalledTimes(1)
    expect(res.ended).toBe(false)
  })

  it('falls back to the page for a missing file', () => {
    expect(run('/assets/missing.js').res.body).toBe(filled)
  })

  it.each(['/../secret.txt', '/%2e%2e/secret.txt', '/assets/../../secret.txt', '/../dist2/x.js'])(
    'falls back to the page for the traversal path %s',
    (url) => {
      const { res, next } = run(url)
      expect(res.body).toBe(filled)
      expect(next).not.toHaveBeenCalled()
    },
  )

  it('answers 500 with the invalid message when APP_URL turns invalid after creation', () => {
    const { res } = run('/', 'GET', undefined, 'http://example.com')
    expect(res.statusCode).toBe(500)
    expect(res.headers).toEqual({
      'Content-Type': 'text/html; charset=utf-8',
      'Cache-Control': 'no-cache',
    })
    expect(res.body).toBe(
      'APP_URL is not valid: APP_URL must use https (http is allowed only for localhost)',
    )
  })

  it('passes a POST on', () => {
    const { res, next } = run('/settings', 'POST')
    expect(next).toHaveBeenCalledTimes(1)
    expect(res.ended).toBe(false)
  })

  it('passes a request that accepts only JSON on', () => {
    const { res, next } = run('/settings', 'GET', 'application/json')
    expect(next).toHaveBeenCalledTimes(1)
    expect(res.ended).toBe(false)
  })

  it('passes a malformed percent escape on', () => {
    const { res, next } = run('/%E0%A4%A')
    expect(next).toHaveBeenCalledTimes(1)
    expect(res.ended).toBe(false)
  })

  it('answers a HEAD with an empty body and the headers', () => {
    const { res } = run('/settings', 'HEAD')
    expect(res.statusCode).toBe(200)
    expect(res.headers['Content-Type']).toBe('text/html; charset=utf-8')
    expect(res.body).toBeUndefined()
    expect(res.ended).toBe(true)
  })

  it('answers 500 with the not-set message when APP_URL is unset', () => {
    vi.stubEnv('APP_URL', '')
    const { res } = run('/')
    expect(res.statusCode).toBe(500)
    expect(res.body).toBe(notSet)
    expect(res.headers['Content-Type']).toBe('text/html; charset=utf-8')
  })

  it('reads APP_URL on each request', () => {
    vi.stubEnv('APP_URL', '')
    expect(run('/').res.statusCode).toBe(500)
    vi.stubEnv('APP_URL', 'https://app.example.test')
    expect(run('/').res.statusCode).toBe(200)
  })
})

describe('serverAddress', () => {
  it('is undefined when APP_URL is unset or empty', () => {
    expect(serverAddress(undefined)).toBeUndefined()
    expect(serverAddress('')).toBeUndefined()
  })

  it('uses the explicit port', () => {
    expect(serverAddress('http://localhost:5173')).toEqual({
      host: 'localhost',
      port: 5173,
      strictPort: true,
    })
  })

  it('defaults to port 80 for http without a port', () => {
    expect(serverAddress('http://app.localhost')?.port).toBe(80)
  })

  it('defaults to port 443 for https without a port', () => {
    expect(serverAddress('https://app.localhost')?.port).toBe(443)
  })

  it('strips the brackets from an IPv6 host', () => {
    expect(serverAddress('http://[::1]:5173')).toEqual({
      host: '::1',
      port: 5173,
      strictPort: true,
    })
  })

  it('throws a clear error for an invalid URL', () => {
    expect(() => serverAddress('not a url')).toThrow(/APP_URL is not a valid URL/)
  })
})

describe('vendorChunks', () => {
  const group = (name: string) => {
    const found = vendorChunks.groups.find((candidate) => candidate.name === name)
    if (!found) throw new Error(`Missing group ${name}`)
    return found
  }

  it('has a react group and a mui group', () => {
    expect(vendorChunks.groups.map((candidate) => candidate.name)).toEqual(['react', 'mui'])
  })

  it('ranks react above mui', () => {
    expect([group('react').priority, group('mui').priority]).toEqual([2, 1])
  })

  it.each([
    '/x/node_modules/.pnpm/react@19.0.0/node_modules/react/index.js',
    '/x/node_modules/react/index.js',
    'C:\\x\\node_modules\\react\\index.js',
    '/x/node_modules/.pnpm/react-dom@19.0.0_react@19.0.0/node_modules/react-dom/client.js',
    '/x/node_modules/react-dom/client.js',
    'C:\\x\\node_modules\\react-dom\\client.js',
    '/x/node_modules/.pnpm/react-router@7.0.0/node_modules/react-router/dist/index.js',
    '/x/node_modules/react-router/dist/index.js',
    'C:\\x\\node_modules\\react-router\\dist\\index.js',
    '/x/node_modules/.pnpm/scheduler@0.25.0/node_modules/scheduler/index.js',
    '/x/node_modules/scheduler/index.js',
    'C:\\x\\node_modules\\scheduler\\index.js',
  ])('puts %s in the react group', (path) => {
    expect(group('react').test.test(path)).toBe(true)
  })

  it.each([
    '/x/node_modules/.pnpm/@mui+material@7.0.0/node_modules/@mui/material/Button/Button.js',
    '/x/node_modules/@mui/material/Button/Button.js',
    'C:\\x\\node_modules\\@mui\\material\\Button\\Button.js',
    '/x/node_modules/@mui/icons-material/HomeRounded.js',
    '/x/node_modules/.pnpm/@emotion+react@11.0.0/node_modules/@emotion/react/dist/emotion-react.js',
    '/x/node_modules/@emotion/styled/dist/emotion-styled.js',
    'C:\\x\\node_modules\\@emotion\\cache\\dist\\emotion-cache.js',
  ])('puts %s in the mui group', (path) => {
    expect(group('mui').test.test(path)).toBe(true)
  })

  it.each([
    '/x/node_modules/react-redux/dist/react-redux.js',
    '/x/node_modules/react-i18next/dist/index.js',
    '/x/node_modules/react-hook-form/dist/index.js',
    '/x/node_modules/react-routerx/index.js',
    '/x/node_modules/react-dom-extras/index.js',
    '/x/node_modules/schedulerx/index.js',
    '/x/node_modules/@mui-x/charts/index.js',
    '/x/node_modules/@emotionx/react/index.js',
    '/x/node_modules/@mui/material/Button.js',
    '/x/node_modules/@emotion/react/dist/emotion-react.js',
    '/x/src/react/index.js',
    '/x/src/features/about/about-panel.tsx',
  ])('keeps %s out of the react group', (path) => {
    expect(group('react').test.test(path)).toBe(false)
  })

  it.each([
    '/x/node_modules/react/index.js',
    '/x/node_modules/react-router/dist/index.js',
    '/x/node_modules/react-i18next/dist/index.js',
    '/x/node_modules/@mui-x/charts/index.js',
    '/x/node_modules/@emotionx/react/index.js',
    '/x/src/mui/index.js',
    '/x/src/features/about/about-panel.tsx',
  ])('keeps %s out of the mui group', (path) => {
    expect(group('mui').test.test(path)).toBe(false)
  })
})
