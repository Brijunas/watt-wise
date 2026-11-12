import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import restoreSession from './restore-session'
import { api, baseUrl, createCookieJar, makeStore, pingHandler } from './session-test-kit'

const server = setupServer()
beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' })
})
afterEach(() => {
  server.resetHandlers()
  localStorage.clear()
  sessionStorage.clear()
})
afterAll(() => {
  server.close()
})

describe('session storage', () => {
  it('never writes a token to localStorage or sessionStorage', async () => {
    const setItem = vi.spyOn(Storage.prototype, 'setItem')
    const { refreshHandler } = createCookieJar()
    server.use(
      refreshHandler,
      pingHandler(),
      http.post(`${baseUrl}/api/v1/auth/sign-in`, () =>
        HttpResponse.json({ accessToken: 'expired' }),
      ),
    )
    const store = makeStore()

    await store.dispatch(restoreSession())
    await store.dispatch(api.endpoints.signIn.initiate(undefined))
    // The token from sign-in is rejected, which refreshes and retries.
    const result = await store.dispatch(api.endpoints.ping.initiate('1'))

    expect(result.data).toEqual({ ok: true })
    expect(store.getState().session.accessToken).toBe('token-2')
    expect(setItem).not.toHaveBeenCalled()
    expect(localStorage).toHaveLength(0)
    expect(sessionStorage).toHaveLength(0)
  })
})
