import { afterEach, describe, expect, it, vi } from 'vitest'
import { runtimeConfig, serverAddress, vendorChunks } from './vite'

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
