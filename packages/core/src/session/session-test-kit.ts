import { combineReducers } from '@reduxjs/toolkit'
import { createApi } from '@reduxjs/toolkit/query'
import { http, HttpResponse } from 'msw'
import type { HttpHandler } from 'msw'
import baseQuery from '../base-query'
import createStore from '../create-store'
import { sessionSlice, sessionStarted } from './session-slice'

// Shared by the session tests: a store with a tiny API and an MSW handler set that behaves like the
// Api's refresh endpoint with a browser's cookie jar.

export const baseUrl = 'http://api.test'

export const api = createApi({
  reducerPath: 'api',
  baseQuery,
  endpoints: (build) => ({
    ping: build.query<{ ok: boolean }, string>({ query: (id) => `/api/v1/ping?id=${id}` }),
    signIn: build.mutation<{ accessToken: string }, undefined>({
      query: () => ({ url: '/api/v1/auth/sign-in', method: 'POST' }),
      onQueryStarted: async (_arg, { dispatch, queryFulfilled }) => {
        await queryFulfilled.then(
          ({ data }) => dispatch(sessionStarted({ accessToken: data.accessToken })),
          () => undefined,
        )
      },
    }),
  }),
})

export const makeStore = () =>
  createStore({
    reducer: combineReducers({ [api.reducerPath]: api.reducer, session: sessionSlice.reducer }),
    api,
    baseUrl,
    devTools: false,
  })

export type TestStore = ReturnType<typeof makeStore>

/** Records every status the session passes through. */
export const recordStatuses = (store: TestStore) => {
  const seen = [store.getState().session.status]
  store.subscribe(() => {
    const { status } = store.getState().session
    if (seen[seen.length - 1] !== status) {
      seen.push(status)
    }
  })
  return seen
}

/**
 * What the browser holds for the refresh cookie, shared by every "tab" that uses it. A cookie is
 * single use: the endpoint answers 401 for one it has seen, as the Api does for a reused token.
 */
export const createCookieJar = (initial: string | null = 'cookie-0') => {
  const jar = {
    cookie: initial,
    usable: new Set<string>(initial ? [initial] : []),
    rotations: 0,
    sent: [] as (string | null)[],
  }
  const refreshHandler = http.post(`${baseUrl}/api/v1/auth/refresh`, () => {
    jar.sent.push(jar.cookie)
    if (!jar.cookie || !jar.usable.delete(jar.cookie)) {
      return new HttpResponse(null, { status: 401 })
    }
    jar.rotations += 1
    jar.cookie = `cookie-${String(jar.rotations)}`
    jar.usable.add(jar.cookie)
    return HttpResponse.json({
      accessToken: `token-${String(jar.rotations)}`,
      accessTokenExpiresAt: '2026-01-01T00:15:00Z',
    })
  })
  return { jar, refreshHandler }
}

/** A `navigator.locks` that grants a lock to one holder at a time and remembers the names asked for. */
export const createFakeLocks = () => {
  let chain: Promise<unknown> = Promise.resolve()
  const names: string[] = []
  const request = <T>(name: string, callback: () => Promise<T>): Promise<T> => {
    names.push(name)
    const run = chain.then(callback)
    chain = run.catch(() => undefined)
    return run
  }
  return { names, request }
}

export const pingHandler = (seenAuth: (string | null)[] = []): HttpHandler =>
  http.get(`${baseUrl}/api/v1/ping`, ({ request }) => {
    const authorization = request.headers.get('authorization')
    seenAuth.push(authorization)
    return authorization === 'Bearer token-1' || authorization === 'Bearer token-2'
      ? HttpResponse.json({ ok: true })
      : new HttpResponse(null, { status: 401 })
  })
