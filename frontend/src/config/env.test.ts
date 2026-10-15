import { describe, expect, it } from 'vitest'
import { env } from './env'

describe('env', () => {
  it('reads the API base URL from the runtime config', () => {
    expect(env.apiBaseUrl).toBe('https://api.test')
  })
})
