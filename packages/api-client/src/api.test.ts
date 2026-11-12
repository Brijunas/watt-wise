import { createStore, sessionEnded, sessionSlice, sessionStarted } from '@wattwise/core'
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

    it('ends the session when a password reset succeeds', async () => {
      server.use(
        http.post(
          'http://api.test/api/v1/auth/reset-password',
          () => new HttpResponse(null, { status: 204 }),
        ),
      )
      const store = signedInStore()

      await store.dispatch(
        api.endpoints.resetPassword.initiate({ body: { id: 'a', token: 't', newPassword: 'x' } }),
      )

      expect(store.getState().session).toMatchObject({ status: 'signedOut', accessToken: null })
    })

    it('keeps the session when a password reset fails', async () => {
      server.use(
        http.post(
          'http://api.test/api/v1/auth/reset-password',
          () => new HttpResponse(null, { status: 400 }),
        ),
      )
      const store = signedInStore()

      await store.dispatch(
        api.endpoints.resetPassword.initiate({ body: { id: 'a', token: 't', newPassword: 'x' } }),
      )

      expect(store.getState().session).toMatchObject({ status: 'signedIn', accessToken: 'token-1' })
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
  describe('retry-after on errors', () => {
    const signInArg = { body: { email: 'a@b.test', password: 'x' } }
    const lockedOut = (retryAfter?: string) =>
      HttpResponse.json(
        { status: 423, code: 'Accounts.LockedOut' },
        { status: 423, headers: retryAfter === undefined ? {} : { 'Retry-After': retryAfter } },
      )

    it('adds the seconds of Retry-After to a failed sign-in and keeps status and data', async () => {
      server.use(http.post('http://api.test/api/v1/auth/sign-in', () => lockedOut('120')))

      const result = await makeStore().dispatch(api.endpoints.signIn.initiate(signInArg))

      expect(result.error).toEqual({
        status: 423,
        data: { status: 423, code: 'Accounts.LockedOut' },
        retryAfterSeconds: 120,
      })
    })

    it('adds the seconds of Retry-After to a failed password change', async () => {
      server.use(http.post('http://api.test/api/v1/auth/change-password', () => lockedOut('30')))

      const result = await signedInStore().dispatch(
        api.endpoints.changePassword.initiate({
          body: { currentPassword: 'x', newPassword: 'y' },
        }),
      )

      expect(result.error).toMatchObject({ status: 423, retryAfterSeconds: 30 })
    })

    it.each([[undefined], ['soon'], ['1.5'], ['-3'], ['']])(
      'ignores the Retry-After value %j',
      async (value) => {
        server.use(http.post('http://api.test/api/v1/auth/sign-in', () => lockedOut(value)))

        const result = await makeStore().dispatch(api.endpoints.signIn.initiate(signInArg))

        expect(result.error).toEqual({
          status: 423,
          data: { status: 423, code: 'Accounts.LockedOut' },
        })
      },
    )
  })

  describe('sessions', () => {
    const list = [
      {
        id: 'a',
        deviceLabel: 'Phone',
        signedInAt: '2026-01-01T00:00:00Z',
        lastUsedAt: '2026-01-01T00:00:00Z',
        current: true,
      },
      {
        id: 'b',
        deviceLabel: 'Laptop',
        signedInAt: '2026-01-01T00:00:00Z',
        lastUsedAt: '2026-01-01T00:00:00Z',
        current: false,
      },
    ]

    const useSessions = (store: ReturnType<typeof makeStore>, counter: { gets: number }) => {
      server.use(
        http.get('http://api.test/api/v1/auth/sessions', () => {
          counter.gets += 1
          return HttpResponse.json(list)
        }),
      )
      return store.dispatch(api.endpoints.listSessions.initiate())
    }

    it('refetches the list after ending another session', async () => {
      const counter = { gets: 0 }
      const store = signedInStore()
      const subscription = useSessions(store, counter)
      await subscription
      server.use(
        http.delete(
          'http://api.test/api/v1/auth/sessions/b',
          () => new HttpResponse(null, { status: 204 }),
        ),
      )

      await store.dispatch(api.endpoints.endSession.initiate({ id: 'b' }))

      await vi.waitFor(() => {
        expect(counter.gets).toBe(2)
      })
      expect(store.getState().session.status).toBe('signedIn')
      subscription.unsubscribe()
    })

    it('ends the session and clears the cache when the current one is ended', async () => {
      const counter = { gets: 0 }
      const store = signedInStore()
      const subscription = useSessions(store, counter)
      await subscription
      server.use(
        http.delete(
          'http://api.test/api/v1/auth/sessions/a',
          () => new HttpResponse(null, { status: 204 }),
        ),
      )

      await store.dispatch(api.endpoints.endSession.initiate({ id: 'a' }))

      expect(store.getState().session).toMatchObject({ status: 'signedOut', accessToken: null })
      expect(Object.keys(store.getState().api.queries)).toEqual([])
      expect(counter.gets).toBe(1)
      subscription.unsubscribe()
    })

    it('keeps the session and the list when ending a session fails', async () => {
      const counter = { gets: 0 }
      const store = signedInStore()
      const subscription = useSessions(store, counter)
      await subscription
      server.use(
        http.delete(
          'http://api.test/api/v1/auth/sessions/a',
          () => new HttpResponse(null, { status: 500 }),
        ),
      )

      await store.dispatch(api.endpoints.endSession.initiate({ id: 'a' }))

      expect(store.getState().session.status).toBe('signedIn')
      expect(counter.gets).toBe(1)
      subscription.unsubscribe()
    })

    it('waits for a deferred answer before ending the session', async () => {
      const counter = { gets: 0 }
      const store = signedInStore()
      const subscription = useSessions(store, counter)
      await subscription
      let release: () => void = () => undefined
      const gate = new Promise<void>((resolve) => {
        release = resolve
      })
      server.use(
        http.delete('http://api.test/api/v1/auth/sessions/a', async () => {
          await gate
          return new HttpResponse(null, { status: 204 })
        }),
      )

      const pending = store.dispatch(api.endpoints.endSession.initiate({ id: 'a' }))
      await vi.waitFor(() => {
        expect(store.getState().api.mutations).not.toEqual({})
      })
      expect(store.getState().session.status).toBe('signedIn')

      release()
      await pending

      expect(store.getState().session.status).toBe('signedOut')
      subscription.unsubscribe()
    })
  })

  describe('late answers', () => {
    const gated = (method: 'post' | 'delete', path: string, body: () => Response) => {
      let release: () => void = () => undefined
      const gate = new Promise<void>((resolve) => {
        release = resolve
      })
      server.use(
        http[method](`http://api.test/api/v1/auth${path}`, async () => {
          await gate
          return body()
        }),
      )
      return release
    }
    const started = async (store: ReturnType<typeof makeStore>) =>
      vi.waitFor(() => {
        expect(store.getState().api.mutations).not.toEqual({})
      })

    it('does not sign the user back in when a password change answers after a sign-out', async () => {
      const release = gated('post', '/change-password', () => signedInResponse('late'))
      const store = signedInStore()
      const pending = store.dispatch(
        api.endpoints.changePassword.initiate({ body: { currentPassword: 'x', newPassword: 'y' } }),
      )
      await started(store)
      store.dispatch(sessionEnded())

      release()
      await pending

      expect(store.getState().session).toMatchObject({ status: 'signedOut', accessToken: null })
    })

    it('does not sign the user in when a sign-in answers after a sign-out', async () => {
      const release = gated('post', '/sign-in', () => signedInResponse('late'))
      const store = makeStore()
      store.dispatch(sessionEnded())
      const revisionBefore = store.getState().session.revision
      const pending = store.dispatch(
        api.endpoints.signIn.initiate({ body: { email: 'a@b.test', password: 'x' } }),
      )
      await started(store)
      store.dispatch(sessionEnded())
      expect(store.getState().session.revision).not.toBe(revisionBefore)

      release()
      await pending

      expect(store.getState().session).toMatchObject({ status: 'signedOut', accessToken: null })
    })

    it('does not replace a newer sign-in when a password change answers late', async () => {
      const release = gated('post', '/change-password', () => signedInResponse('late'))
      const store = signedInStore()
      const pending = store.dispatch(
        api.endpoints.changePassword.initiate({ body: { currentPassword: 'x', newPassword: 'y' } }),
      )
      await started(store)
      store.dispatch(sessionStarted({ accessToken: 'newer' }))

      release()
      await pending

      expect(store.getState().session.accessToken).toBe('newer')
    })

    it('keeps a new sign-in when ending the old current session answers late', async () => {
      const store = signedInStore()
      server.use(
        http.get('http://api.test/api/v1/auth/sessions', () =>
          HttpResponse.json([
            {
              id: 'a',
              deviceLabel: 'Phone',
              signedInAt: '2026-01-01T00:00:00Z',
              lastUsedAt: '2026-01-01T00:00:00Z',
              current: true,
            },
          ]),
        ),
      )
      const subscription = store.dispatch(api.endpoints.listSessions.initiate())
      await subscription
      const release = gated('delete', '/sessions/a', () => new HttpResponse(null, { status: 204 }))
      const pending = store.dispatch(api.endpoints.endSession.initiate({ id: 'a' }))
      await started(store)
      store.dispatch(sessionEnded())
      store.dispatch(sessionStarted({ accessToken: 'new-session' }))

      release()
      await pending

      expect(store.getState().session).toMatchObject({
        status: 'signedIn',
        accessToken: 'new-session',
      })
      subscription.unsubscribe()
    })

    it('keeps a newer session when signing out everywhere answers late', async () => {
      const release = gated(
        'post',
        '/sign-out-everywhere',
        () => new HttpResponse(null, { status: 204 }),
      )
      const store = signedInStore()
      const pending = store.dispatch(api.endpoints.signOutEverywhere.initiate())
      await started(store)
      store.dispatch(sessionStarted({ accessToken: 'newer' }))

      release()
      await pending

      expect(store.getState().session).toMatchObject({ status: 'signedIn', accessToken: 'newer' })
    })
  })
})
