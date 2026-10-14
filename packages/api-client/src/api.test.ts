import { createStore } from '@wattwise/core'
import { combineReducers } from '@reduxjs/toolkit'
import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'
import { afterAll, afterEach, beforeAll, describe, expect, it } from 'vitest'
import { api } from './api'

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
    const store = createStore({
      reducer: combineReducers({ [api.reducerPath]: api.reducer }),
      api,
      baseUrl: 'http://api.test',
      devTools: false,
    })

    const result = await store.dispatch(api.endpoints.getAbout.initiate())

    expect(result.data).toEqual({ name: 'Watt-Wise API', version: '1.2.3' })
  })
})
