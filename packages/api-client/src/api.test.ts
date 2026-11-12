import { createStore, sessionSlice, sessionStarted } from '@wattwise/core'
import { combineReducers } from '@reduxjs/toolkit'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll, describe, expect, it, vi } from 'vitest'
import { api } from './api'

const server = setupServer()
beforeAll(() => {
  server.listen({ onUnhandledFrame: 'error' })
})
afterEach(() => {
  vi.unstubAllGlobals()
  server.resetHandlers()
})
afterAll(() => {
  server.close()
})

const makeStore = () =>
  createStore({
    reducer: combineReducers({ [api.reducerPath]: api.reducer, session: sessionSlice.reducer }),
    api,
    baseUrl: 'http://api.test',
    devTools: false,
  })

const signedInStore = () => {
  const store = makeStore()
  store.dispatch(sessionStarted({ accessToken: 'token-1' }))
  return store
}

const signedInResponse = (accessToken: string) =>
  HttpResponse.json({ accessToken, accessTokenExpiresAt: '2026-01-01T00:15:00Z' })

describe('api', () => {
  it('is the single slice under the api key with the generated endpoint', () => {
    expect(api.reducerPath).toBe('api')
    expect(api.endpoints.getAbout).toBeDefined()
  })

  it('loads the about data through a store from core', async () => {
    server.use(
      http.get('http://api.test/api/v1/about', () =>
        HttpResponse.json({ name: 'Watt-Wise API', version: '1.2.3' }),
      ),
    )
    const store = makeStore()

    const result = await store.dispatch(api.endpoints.getAbout.initiate())

    expect(result.data).toEqual({ name: 'Watt-Wise API', version: '1.2.3' })
  })

  describe('session wiring', () => {
    it('sends the generated refreshSession endpoint through the shared refresh', async () => {
      const lockNames: string[] = []
      vi.stubGlobal('navigator', {
        locks: {
          request: (name: string, callback: () => Promise<unknown>) => {
            lockNames.push(name)
            return callback()
          },
        },
      })
      let requests = 0
      server.use(
        http.post('http://api.test/api/v1/auth/refresh', () => {
          requests += 1
          return signedInResponse('refreshed')
        }),
      )
      const store = signedInStore()

      const result = await store.dispatch(api.endpoints.refreshSession.initiate())

      expect(result.data).toEqual({
        accessToken: 'refreshed',
        accessTokenExpiresAt: '2026-01-01T00:15:00Z',
      })
      expect(requests).toBe(1)
      expect(lockNames).toEqual(['wattwise-refresh'])
      expect(store.getState().session).toMatchObject({
        status: 'signedIn',
        accessToken: 'refreshed',
      })
    })

    it('fails the refreshSession endpoint and ends the session when the cookie is rejected', async () => {
      server.use(
        http.post(
          'http://api.test/api/v1/auth/refresh',
          () => new HttpResponse(null, { status: 401 }),
        ),
      )
      const store = signedInStore()

      const result = await store.dispatch(api.endpoints.refreshSession.initiate())

      expect(result).toMatchObject({ error: expect.anything() as unknown })
      expect(store.getState().session.status).toBe('signedOut')
    })

    it('stores the access token a sign-in returns', async () => {
      server.use(http.post('http://api.test/api/v1/auth/sign-in', () => signedInResponse('abc')))
      const store = makeStore()

      await store.dispatch(
        api.endpoints.signIn.initiate({ body: { email: 'a@b.test', password: 'x' } }),
      )

      expect(store.getState().session).toMatchObject({ status: 'signedIn', accessToken: 'abc' })
    })

    it('leaves the session alone when sign-in fails', async () => {
      server.use(
        http.post(
          'http://api.test/api/v1/auth/sign-in',
          () => new HttpResponse(null, { status: 401 }),
        ),
      )
      const store = makeStore()

      await store.dispatch(
        api.endpoints.signIn.initiate({ body: { email: 'a@b.test', password: 'x' } }),
      )

      expect(store.getState().session.accessToken).toBeNull()
    })

    it('replaces the access token after a password change', async () => {
      server.use(
        http.post('http://api.test/api/v1/auth/change-password', () => signedInResponse('new')),
      )
      const store = signedInStore()

      await store.dispatch(
        api.endpoints.changePassword.initiate({
          body: { currentPassword: 'x', newPassword: 'y' },
        }),
      )

      expect(store.getState().session.accessToken).toBe('new')
    })

    it('ends the session and clears the cache on sign-out', async () => {
      server.use(
        http.get('http://api.test/api/v1/about', () =>
          HttpResponse.json({ name: 'a', version: '1' }),
        ),
        http.post(
          'http://api.test/api/v1/auth/sign-out',
          () => new HttpResponse(null, { status: 204 }),
        ),
      )
      const store = signedInStore()
      await store.dispatch(api.endpoints.getAbout.initiate())

      await store.dispatch(api.endpoints.signOut.initiate())

      expect(store.getState().session).toMatchObject({ status: 'signedOut', accessToken: null })
      expect(Object.keys(store.getState().api.queries)).toEqual([])
    })

    it('ends the session on sign-out even when the request fails', async () => {
      server.use(http.post('http://api.test/api/v1/auth/sign-out', () => HttpResponse.error()))
      const store = signedInStore()

      await store.dispatch(api.endpoints.signOut.initiate())

      expect(store.getState().session.status).toBe('signedOut')
    })

    it('ends the session when signing out everywhere succeeds', async () => {
      server.use(
        http.post(
          'http://api.test/api/v1/auth/sign-out-everywhere',
          () => new HttpResponse(null, { status: 204 }),
        ),
      )
      const store = signedInStore()

      await store.dispatch(api.endpoints.signOutEverywhere.initiate())

      expect(store.getState().session.status).toBe('signedOut')
    })

    it('keeps the session when signing out everywhere fails', async () => {
      server.use(
        http.post(
          'http://api.test/api/v1/auth/sign-out-everywhere',
          () => new HttpResponse(null, { status: 500 }),
        ),
      )
      const store = signedInStore()

      await store.dispatch(api.endpoints.signOutEverywhere.initiate())

      expect(store.getState().session).toMatchObject({ status: 'signedIn', accessToken: 'token-1' })
    })
  })
})
