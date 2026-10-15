import { afterEach, describe, expect, it, vi } from 'vitest'
import { runtimeConfig, serverAddress } from './vite'

type Middleware = (req: object, res: object, next: () => void) => void

function getMiddleware(hook: 'configureServer' | 'configurePreviewServer'): Middleware {
  const plugin = runtimeConfig()
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
