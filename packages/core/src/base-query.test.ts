import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest'
import { createApi } from '@reduxjs/toolkit/query'
import baseQuery from './base-query'
import createStore from './create-store'
import { combineReducers } from '@reduxjs/toolkit'

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

const api = createApi({
  reducerPath: 'api',
  baseQuery,
  endpoints: (build) => ({
    ping: build.query<{ ok: boolean }, undefined>({ query: () => 'ping' }),
  }),
})

const makeStore = (baseUrl: string, devTools = false) =>
  createStore({
    reducer: combineReducers({ [api.reducerPath]: api.reducer }),
    api,
    baseUrl,
    devTools,
  })

describe('baseQuery', () => {
  it('requests the path under the base URL from the store', async () => {
    server.use(http.get('http://one.test/v1/ping', () => HttpResponse.json({ ok: true })))
    const store = makeStore('http://one.test/v1')
    const result = await store.dispatch(api.endpoints.ping.initiate(undefined))
    expect(result.data).toEqual({ ok: true })
  })

  it('reports a failing response as an error', async () => {
    server.use(http.get('http://one.test/ping', () => new HttpResponse(null, { status: 500 })))
    const store = makeStore('http://one.test')
    const result = await store.dispatch(api.endpoints.ping.initiate(undefined))
    expect(result.error).toMatchObject({ status: 500 })
  })
})
