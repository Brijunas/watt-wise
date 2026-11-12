import { combineReducers } from '@reduxjs/toolkit'
import { createApi } from '@reduxjs/toolkit/query'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { api, baseUrl, createCookieJar, makeStore, pingHandler } from './session/session-test-kit'
import baseQuery from './base-query'
import createStore from './create-store'
import { sessionSlice, sessionStarted } from './session/session-slice'

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

const ping = (store: ReturnType<typeof makeStore>, id: string) =>
  store.dispatch(api.endpoints.ping.initiate(id))

describe('baseQuery', () => {
  it('requests the path under the base URL from the store', async () => {
    server.use(http.get('http://one.test/v1/ping', () => HttpResponse.json({ ok: true })))
    const plain = createApi({
      reducerPath: 'api',
      baseQuery,
      endpoints: (build) => ({
        ping: build.query<{ ok: boolean }, undefined>({ query: () => 'ping' }),
      }),
    })
    const store = createStore({
      reducer: combineReducers({
        [plain.reducerPath]: plain.reducer,
        session: sessionSlice.reducer,
      }),
      api: plain,
      baseUrl: 'http://one.test/v1',
      devTools: false,
    })
    const result = await store.dispatch(plain.endpoints.ping.initiate(undefined))
    expect(result.data).toEqual({ ok: true })
  })

  it('reports a failing response as an error', async () => {
    server.use(http.get(`${baseUrl}/api/v1/ping`, () => new HttpResponse(null, { status: 500 })))
    const store = makeStore()
    const result = await ping(store, '1')
    expect(result.error).toMatchObject({ status: 500 })
  })

  it('sends the access token as a Bearer header', async () => {
    const seen: (string | null)[] = []
    server.use(pingHandler(seen))
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'token-1' }))

    const result = await ping(store, '1')

    expect(result.data).toEqual({ ok: true })
    expect(seen).toEqual(['Bearer token-1'])
  })

  it('refreshes once on a 401 and retries with the new token', async () => {
    const { jar, refreshHandler } = createCookieJar()
    const seen: (string | null)[] = []
    server.use(refreshHandler, pingHandler(seen))
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'expired' }))

    const result = await ping(store, '1')

    expect(result.data).toEqual({ ok: true })
    expect(jar.sent).toEqual(['cookie-0'])
    expect(seen).toEqual(['Bearer expired', 'Bearer token-1'])
    expect(store.getState().session.accessToken).toBe('token-1')
  })

  it('shares one refresh between two requests failing with 401 at once and retries both', async () => {
    const { jar, refreshHandler } = createCookieJar()
    const seen: (string | null)[] = []
    server.use(refreshHandler, pingHandler(seen))
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'expired' }))

    const [first, second] = await Promise.all([ping(store, '1'), ping(store, '2')])

    expect(first.data).toEqual({ ok: true })
    expect(second.data).toEqual({ ok: true })
    expect(jar.sent).toHaveLength(1)
    expect(jar.rotations).toBe(1)
    expect(seen.filter((header) => header === 'Bearer expired')).toHaveLength(2)
    expect(seen.filter((header) => header === 'Bearer token-1')).toHaveLength(2)
  })

  it('retries a late 401 with the token another request already refreshed', async () => {
    const { jar, refreshHandler } = createCookieJar()
    const seen: Record<string, (string | null)[]> = { '1': [], '2': [] }
    let releaseFirst: () => void = vi.fn()
    const firstHeld = new Promise<void>((resolve) => {
      releaseFirst = resolve
    })
    server.use(
      refreshHandler,
      http.get(`${baseUrl}/api/v1/ping`, async ({ request }) => {
        const id = new URL(request.url).searchParams.get('id') ?? ''
        const authorization = request.headers.get('authorization')
        seen[id]?.push(authorization)
        if (authorization === 'Bearer token-1') {
          return HttpResponse.json({ ok: true })
        }
        if (id === '1') {
          await firstHeld
        }
        return new HttpResponse(null, { status: 401 })
      }),
    )
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'expired' }))

    const first = ping(store, '1')
    const second = await ping(store, '2')
    expect(second.data).toEqual({ ok: true })
    releaseFirst()
    const firstResult = await first

    expect(firstResult.data).toEqual({ ok: true })
    expect(seen['1']).toEqual(['Bearer expired', 'Bearer token-1'])
    expect(jar.sent).toHaveLength(1)
  })

  it('retries at once when another request already replaced the token', async () => {
    const { jar, refreshHandler } = createCookieJar()
    const seen: (string | null)[] = []
    server.use(refreshHandler, pingHandler(seen))
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'expired' }))
    await ping(store, '1')

    // A request sent with the old token that only now gets its 401.
    server.use(
      http.get(`${baseUrl}/api/v1/ping`, ({ request }) => {
        seen.push(request.headers.get('authorization'))
        return request.headers.get('authorization') === 'Bearer token-1'
          ? HttpResponse.json({ ok: true })
          : new HttpResponse(null, { status: 401 })
      }),
    )
    store.dispatch(sessionStarted({ accessToken: 'token-1' }))
    const result = await ping(store, '2')

    expect(result.data).toEqual({ ok: true })
    expect(jar.sent).toHaveLength(1)
  })

  it('retries only once and returns the second 401', async () => {
    const { jar, refreshHandler } = createCookieJar()
    let calls = 0
    server.use(
      refreshHandler,
      http.get(`${baseUrl}/api/v1/ping`, () => {
        calls += 1
        return new HttpResponse(null, { status: 401 })
      }),
    )
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'expired' }))

    const result = await ping(store, '1')

    expect(result.error).toMatchObject({ status: 401 })
    expect(calls).toBe(2)
    expect(jar.sent).toHaveLength(1)
  })

  it('clears the session and the API cache when the refresh fails', async () => {
    const { jar, refreshHandler } = createCookieJar(null)
    server.use(refreshHandler, pingHandler())
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'expired' }))

    const result = await ping(store, '1')

    // The reset also wipes this request's own cache entry, so only the state is left to check.
    expect(result.error).toBeUndefined()
    expect(jar.sent).toHaveLength(1)
    expect(store.getState().session).toMatchObject({ status: 'signedOut', accessToken: null })
    expect(Object.keys(store.getState().api.queries)).toEqual([])
  })

  it('keeps the session when the refresh fails for a reason that is not a rejection', async () => {
    server.use(
      http.post(`${baseUrl}/api/v1/auth/refresh`, () => new HttpResponse(null, { status: 503 })),
      pingHandler(),
    )
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'expired' }))

    const result = await ping(store, '1')

    expect(result.error).toMatchObject({ status: 401 })
    expect(store.getState().session).toMatchObject({ status: 'signedIn', accessToken: 'expired' })
  })

  it('does not refresh for a request that carried no token', async () => {
    const { jar, refreshHandler } = createCookieJar()
    server.use(refreshHandler, pingHandler())
    const store = makeStore()

    const result = await ping(store, '1')

    expect(result.error).toMatchObject({ status: 401 })
    expect(jar.sent).toEqual([])
  })

  it('does not refresh for a 401 from sign-in', async () => {
    const { jar, refreshHandler } = createCookieJar()
    server.use(
      refreshHandler,
      http.post(`${baseUrl}/api/v1/auth/sign-in`, () => new HttpResponse(null, { status: 401 })),
    )
    const store = makeStore()
    store.dispatch(sessionStarted({ accessToken: 'token-1' }))

    const result = await store.dispatch(api.endpoints.signIn.initiate(undefined))

    expect(result.error).toMatchObject({ status: 401 })
    expect(jar.sent).toEqual([])
    expect(store.getState().session.accessToken).toBe('token-1')
  })

  it('never refreshes again for a 401 from the refresh endpoint itself', async () => {
    const calls: string[] = []
    server.use(
      http.post(`${baseUrl}/api/v1/auth/refresh`, () => {
        calls.push('refresh')
        return new HttpResponse(null, { status: 401 })
      }),
    )
    const refreshApi = createApi({
      reducerPath: 'api',
      baseQuery,
      endpoints: (build) => ({
        refresh: build.mutation<unknown, undefined>({
          query: () => ({ url: '/api/v1/auth/refresh', method: 'POST' }),
        }),
      }),
    })
    const store = createStore({
      reducer: combineReducers({
        [refreshApi.reducerPath]: refreshApi.reducer,
        session: sessionSlice.reducer,
      }),
      api: refreshApi,
      baseUrl,
      devTools: false,
    })
    store.dispatch(sessionStarted({ accessToken: 'token-1' }))

    const result = await store.dispatch(refreshApi.endpoints.refresh.initiate(undefined))

    expect(result.error).toBeDefined()
    expect(calls).toEqual(['refresh'])
    expect(store.getState().session.status).toBe('signedOut')
  })

  it('sends credentials to the auth endpoints and to nothing else', async () => {
    server.use(
      http.get(`${baseUrl}/api/v1/ping`, () => HttpResponse.json({ ok: true })),
      http.post(`${baseUrl}/api/v1/auth/sign-in`, () =>
        HttpResponse.json({ accessToken: 'token-1' }),
      ),
    )
    const fetchSpy = vi.spyOn(globalThis, 'fetch')
    const store = makeStore()

    await ping(store, '1')
    await store.dispatch(api.endpoints.signIn.initiate(undefined))

    const sent = fetchSpy.mock.calls.map(([input]) => input as Request)
    expect(sent.map((request) => [new URL(request.url).pathname, request.credentials])).toEqual([
      ['/api/v1/ping', 'same-origin'],
      ['/api/v1/auth/sign-in', 'include'],
    ])
  })
})
