import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import refreshSession, { refreshLockName } from './refresh-session'
import { sessionEnded, sessionStarted } from './session-slice'
import { baseUrl, createCookieJar, createFakeLocks, makeStore } from './session-test-kit'

const server = setupServer()
beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' })
})
afterEach(() => {
  server.resetHandlers()
  vi.unstubAllGlobals()
})
afterAll(() => {
  server.close()
})

describe('refreshSession', () => {
  it('runs the refresh inside the origin-wide Web Lock', async () => {
    const { refreshHandler } = createCookieJar()
    server.use(refreshHandler)
    const locks = createFakeLocks()
    vi.stubGlobal('navigator', { locks })
    const store = makeStore()

    const refreshed = await store.dispatch(refreshSession())

    expect(refreshed).toEqual({
      accessToken: 'token-1',
      accessTokenExpiresAt: '2026-01-01T00:15:00Z',
    })
    expect(locks.names).toEqual([refreshLockName])
  })

  it('takes the lock once for callers in one store that refresh together', async () => {
    const { jar, refreshHandler } = createCookieJar()
    server.use(refreshHandler)
    const locks = createFakeLocks()
    vi.stubGlobal('navigator', { locks })
    const store = makeStore()

    const results = await Promise.all([
      store.dispatch(refreshSession()),
      store.dispatch(refreshSession()),
    ])

    expect(results.map((result) => result?.accessToken)).toEqual([
      expect.any(String),
      expect.any(String),
    ])
    expect(locks.names).toHaveLength(1)
    expect(jar.sent).toHaveLength(1)
  })

  it('lets a second tab wait and send the cookie the first one rotated', async () => {
    const { jar, refreshHandler } = createCookieJar()
    server.use(refreshHandler)
    vi.stubGlobal('navigator', { locks: createFakeLocks() })
    const firstTab = makeStore()
    const secondTab = makeStore()

    const results = await Promise.all([
      firstTab.dispatch(refreshSession()),
      secondTab.dispatch(refreshSession()),
    ])

    expect(results.map((result) => result?.accessToken)).toEqual([
      expect.any(String),
      expect.any(String),
    ])
    expect(jar.sent).toEqual(['cookie-0', 'cookie-1'])
    expect(firstTab.getState().session.accessToken).toBe('token-1')
    expect(secondTab.getState().session).toMatchObject({
      status: 'signedIn',
      accessToken: 'token-2',
    })
  })

  it('starts a new refresh after the previous one settled', async () => {
    const { jar, refreshHandler } = createCookieJar()
    server.use(refreshHandler)
    const store = makeStore()

    await store.dispatch(refreshSession())
    await store.dispatch(refreshSession())

    expect(jar.sent).toEqual(['cookie-0', 'cookie-1'])
  })

  it('works without Web Locks', async () => {
    const { refreshHandler } = createCookieJar()
    server.use(refreshHandler)
    vi.stubGlobal('navigator', {})
    const store = makeStore()

    expect(await store.dispatch(refreshSession())).not.toBeNull()
  })

  it('releases the lock when the refresh fails', async () => {
    server.use(http.post(`${baseUrl}/api/v1/auth/refresh`, () => HttpResponse.error()))
    const locks = createFakeLocks()
    vi.stubGlobal('navigator', { locks })
    const store = makeStore()

    expect(await store.dispatch(refreshSession())).toBeNull()
    expect(await store.dispatch(refreshSession())).toBeNull()
    expect(locks.names).toHaveLength(2)
  })

  it('leaves the session as it is when the server fails or throttles', async () => {
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'abc' }))
    server.use(
      http.post(`${baseUrl}/api/v1/auth/refresh`, () => new HttpResponse(null, { status: 429 })),
    )

    expect(await store.dispatch(refreshSession())).toBeNull()
    expect(store.getState().session).toMatchObject({ status: 'signedIn', accessToken: 'abc' })
  })

  it('sends no request when the session ended while it queued for the lock', async () => {
    let requests = 0
    server.use(
      http.post(`${baseUrl}/api/v1/auth/refresh`, () => {
        requests += 1
        return HttpResponse.json({ accessToken: 'late-token', accessTokenExpiresAt: 'x' })
      }),
    )
    // Another tab holds the lock until the test releases it.
    let release: () => void = vi.fn()
    const held = new Promise<void>((resolve) => {
      release = resolve
    })
    vi.stubGlobal('navigator', {
      locks: {
        request: async <T>(_name: string, callback: () => Promise<T>) => {
          await held
          return callback()
        },
      },
    })
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'abc' }))

    const refreshing = store.dispatch(refreshSession())
    store.dispatch(sessionEnded())
    release()

    expect(await refreshing).toBeNull()
    expect(requests).toBe(0)
    expect(store.getState().session).toMatchObject({ status: 'signedOut', accessToken: null })
  })

  describe('when the session changes while the refresh is in flight', () => {
    // The refresh request is held until the test lets it answer.
    const heldRefresh = (status: number) => {
      let answer: () => void = vi.fn()
      let reached: () => void = vi.fn()
      const held = new Promise<void>((resolve) => {
        answer = resolve
      })
      const sent = new Promise<void>((resolve) => {
        reached = resolve
      })
      server.use(
        http.post(`${baseUrl}/api/v1/auth/refresh`, async () => {
          reached()
          await held
          return status === 200
            ? HttpResponse.json({ accessToken: 'late-token', accessTokenExpiresAt: 'x' })
            : new HttpResponse(null, { status })
        }),
      )
      return { answer, sent }
    }

    it('stays signed out when a late 200 follows a sign-out', async () => {
      const store = makeStore()
      store.dispatch(sessionStarted({ accessToken: 'abc' }))
      const { answer, sent } = heldRefresh(200)

      const refreshing = store.dispatch(refreshSession())
      await sent
      store.dispatch(sessionEnded())
      answer()
      await refreshing

      expect(store.getState().session).toMatchObject({ status: 'signedOut', accessToken: null })
    })

    it.each([200, 401])('keeps a newer sign-in when a late %i arrives', async (status) => {
      const store = makeStore()
      const { answer, sent } = heldRefresh(status)

      const refreshing = store.dispatch(refreshSession())
      await sent
      store.dispatch(sessionStarted({ accessToken: 'signed-in' }))
      answer()

      expect(await refreshing).toBeNull()
      expect(store.getState().session).toMatchObject({
        status: 'signedIn',
        accessToken: 'signed-in',
      })
    })
  })
})
