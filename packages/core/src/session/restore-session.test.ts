import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import restoreSession from './restore-session'
import { api, baseUrl, createCookieJar, makeStore, recordStatuses } from './session-test-kit'

const server = setupServer()
beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' })
})
afterEach(() => {
  server.resetHandlers()
})
afterAll(() => {
  server.close()
})

describe('restoreSession', () => {
  it('restores the session from a valid cookie without ever showing signed out', async () => {
    const { jar, refreshHandler } = createCookieJar()
    server.use(refreshHandler)
    const store = makeStore()
    const statuses = recordStatuses(store)

    await store.dispatch(restoreSession())

    expect(statuses).toEqual(['restoring', 'signedIn'])
    expect(store.getState().session.accessToken).toBe('token-1')
    expect(jar.cookie).toBe('cookie-1')
  })

  it('ends signed out when the cookie is missing or rejected', async () => {
    const { refreshHandler } = createCookieJar(null)
    server.use(refreshHandler)
    const store = makeStore()
    const statuses = recordStatuses(store)

    await store.dispatch(restoreSession())

    expect(statuses).toEqual(['restoring', 'signedOut'])
    expect(store.getState().session.accessToken).toBeNull()
  })

  it('ends signed out when the network fails', async () => {
    server.use(http.post(`${baseUrl}/api/v1/auth/refresh`, () => HttpResponse.error()))
    const store = makeStore()

    await store.dispatch(restoreSession())

    expect(store.getState().session.status).toBe('signedOut')
  })

  it('ends signed out when the server fails', async () => {
    server.use(
      http.post(`${baseUrl}/api/v1/auth/refresh`, () => new HttpResponse(null, { status: 503 })),
    )
    const store = makeStore()

    await store.dispatch(restoreSession())

    expect(store.getState().session.status).toBe('signedOut')
  })

  it('ends signed out when the answer carries no token', async () => {
    server.use(http.post(`${baseUrl}/api/v1/auth/refresh`, () => HttpResponse.json({})))
    const store = makeStore()

    await store.dispatch(restoreSession())

    expect(store.getState().session.status).toBe('signedOut')
  })

  it('does not end a session that signed in while the refresh was running', async () => {
    let answerRefresh: () => void = vi.fn()
    const refreshHeld = new Promise<void>((resolve) => {
      answerRefresh = resolve
    })
    server.use(
      http.post(`${baseUrl}/api/v1/auth/refresh`, async () => {
        await refreshHeld
        return new HttpResponse(null, { status: 401 })
      }),
      http.post(`${baseUrl}/api/v1/auth/sign-in`, () =>
        HttpResponse.json({ accessToken: 'token-1' }),
      ),
    )
    const store = makeStore()

    const restoring = store.dispatch(restoreSession())
    await store.dispatch(api.endpoints.signIn.initiate(undefined))
    answerRefresh()
    await restoring

    expect(store.getState().session).toMatchObject({ status: 'signedIn', accessToken: 'token-1' })
  })
})
