import { describe, expect, it } from 'vitest'
import readRuntimeConfig from './read-runtime-config'

const read = (apiBaseUrl: unknown) => readRuntimeConfig({ apiBaseUrl })

describe('readRuntimeConfig', () => {
  it('returns the origin of an https URL', () => {
    expect(read('https://api.example.test')).toEqual({ apiBaseUrl: 'https://api.example.test' })
  })

  it('drops a trailing slash', () => {
    expect(read('https://api.example.test/').apiBaseUrl).toBe('https://api.example.test')
  })

  it.each(['http://localhost:5000', 'http://127.0.0.1:5000', 'http://[::1]:5000'])(
    'allows http on loopback: %s',
    (url) => {
      expect(read(url).apiBaseUrl).toBe(url)
    },
  )

  it('rejects http on other hosts', () => {
    expect(() => read('http://api.example.test')).toThrow('must use https')
  })

  it('rejects other protocols', () => {
    expect(() => read('ftp://api.example.test')).toThrow('must use https')
  })

  it('rejects credentials', () => {
    const url = new URL('https://api.example.test')
    url.username = 'someone'
    expect(() => read(url.href)).toThrow('credentials')
  })

  it('rejects a query and a fragment', () => {
    expect(() => read('https://api.example.test?a=1')).toThrow('query')
    expect(() => read('https://api.example.test#a')).toThrow('fragment')
  })

  it('rejects a path', () => {
    expect(() => read('https://api.example.test/api/v1')).toThrow('path')
  })

  it('rejects text that is not a URL', () => {
    expect(() => read('not a url')).toThrow('is not a URL')
  })

  it('rejects a missing or non-string apiBaseUrl', () => {
    expect(() => read(undefined)).toThrow('apiBaseUrl is missing')
    expect(() => read('')).toThrow('apiBaseUrl is missing')
    expect(() => read(42)).toThrow('apiBaseUrl is missing')
  })

  it('rejects a missing config object and names /config.js and API_BASE_URL', () => {
    expect(() => readRuntimeConfig(undefined)).toThrow('/config.js')
    expect(() => readRuntimeConfig(null)).toThrow('API_BASE_URL')
    expect(() => readRuntimeConfig('x')).toThrow('not set')
  })

  it('reads globalThis by default', () => {
    globalThis.__WATTWISE_CONFIG__ = { apiBaseUrl: 'https://api.example.test' }
    try {
      expect(readRuntimeConfig().apiBaseUrl).toBe('https://api.example.test')
    } finally {
      delete (globalThis as { __WATTWISE_CONFIG__?: unknown }).__WATTWISE_CONFIG__
    }
  })
})
