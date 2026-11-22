import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { server } from './mocks/server'

beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' })
})
afterEach(() => {
  cleanup()
  if (typeof window !== 'undefined') window.localStorage.clear()
  server.resetHandlers()
})
afterAll(() => {
  server.close()
})
