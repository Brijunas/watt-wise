import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

beforeEach(() => {
  vi.resetModules()
})

afterEach(() => {
  vi.unstubAllEnvs()
  vi.resetModules()
})

describe('env', () => {
  it('reads the API base URL', async () => {
    vi.stubEnv('VITE_API_BASE_URL', 'http://api.example.test')
    const { env } = await import('./env')
    expect(env.apiBaseUrl).toBe('http://api.example.test')
  })

  it('throws a clear error when the API base URL is missing', async () => {
    vi.stubEnv('VITE_API_BASE_URL', '')
    await expect(import('./env')).rejects.toThrow('VITE_API_BASE_URL is not set')
  })
})
