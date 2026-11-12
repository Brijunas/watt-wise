import { combineReducers, createSlice } from '@reduxjs/toolkit'
import type { Middleware } from '@reduxjs/toolkit'
import { describe, expect, it, vi } from 'vitest'
import type { StoreExtra } from './store-extra'
import createStore from './create-store'

const counter = createSlice({
  name: 'counter',
  initialState: 0,
  reducers: { increment: (state) => state + 1 },
})
const reducer = combineReducers({ counter: counter.reducer })
const noopMiddleware: Middleware = () => (next) => (action) => next(action)
const resetApiState = () => ({ type: 'api/resetApiState' })
const api = { middleware: noopMiddleware, util: { resetApiState } }

describe('createStore', () => {
  it('hands the base URL to thunks as the extra argument', () => {
    const store = createStore({ reducer, api, baseUrl: 'http://api.test', devTools: false })
    let seen: unknown
    store.dispatch((_dispatch, _getState, extra) => {
      seen = extra
    })
    expect(seen).toEqual({
      baseUrl: 'http://api.test',
      refresh: { inFlight: null },
      resetApiState: expect.any(Function) as unknown,
    } satisfies Record<keyof StoreExtra, unknown>)
  })

  it("gives each store its own refresh state and the API slice's reset action", () => {
    const first = createStore({ reducer, api, baseUrl: 'http://a.test', devTools: false })
    const second = createStore({ reducer, api, baseUrl: 'http://b.test', devTools: false })
    let extras: StoreExtra[] = []
    first.dispatch((_d, _g, extra) => {
      extras = [...extras, extra]
    })
    second.dispatch((_d, _g, extra) => {
      extras = [...extras, extra]
    })
    expect(extras[0]?.refresh).not.toBe(extras[1]?.refresh)
    expect(extras[0]?.resetApiState()).toEqual(resetApiState())
  })

  it('adds the API middleware', () => {
    const spy = vi.fn()
    const middleware: Middleware = () => (next) => (action) => {
      spy(action)
      return next(action)
    }
    const store = createStore({
      reducer,
      api: { middleware, util: { resetApiState } },
      baseUrl: 'http://api.test',
      devTools: false,
    })
    store.dispatch(counter.actions.increment())
    expect(spy).toHaveBeenCalledWith(counter.actions.increment())
  })

  it('starts from the preloaded state', () => {
    const store = createStore({
      reducer,
      api,
      baseUrl: 'http://api.test',
      devTools: false,
      preloadedState: { counter: 5 },
    })
    expect(store.getState().counter).toBe(5)
  })

  it('keeps two stores independent', () => {
    const first = createStore({ reducer, api, baseUrl: 'http://a.test', devTools: false })
    const second = createStore({ reducer, api, baseUrl: 'http://b.test', devTools: false })
    first.dispatch(counter.actions.increment())
    expect(first.getState().counter).toBe(1)
    expect(second.getState().counter).toBe(0)
  })
})
