import { combineReducers, createSlice } from '@reduxjs/toolkit'
import type { Middleware } from '@reduxjs/toolkit'
import { describe, expect, it, vi } from 'vitest'
import type { StoreExtra } from './base-query'
import createStore from './create-store'

const counter = createSlice({
  name: 'counter',
  initialState: 0,
  reducers: { increment: (state) => state + 1 },
})
const reducer = combineReducers({ counter: counter.reducer })
const noopMiddleware: Middleware = () => (next) => (action) => next(action)
const api = { middleware: noopMiddleware }

describe('createStore', () => {
  it('hands the base URL to thunks as the extra argument', () => {
    const store = createStore({ reducer, api, baseUrl: 'http://api.test', devTools: false })
    let seen: unknown
    store.dispatch((_dispatch, _getState, extra) => {
      seen = extra
    })
    expect(seen).toEqual({ baseUrl: 'http://api.test' } satisfies StoreExtra)
  })

  it('adds the API middleware', () => {
    const spy = vi.fn()
    const middleware: Middleware = () => (next) => (action) => {
      spy(action)
      return next(action)
    }
    const store = createStore({
      reducer,
      api: { middleware },
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
