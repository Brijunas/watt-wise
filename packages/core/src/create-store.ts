import { configureStore } from '@reduxjs/toolkit'
import { setupListeners } from '@reduxjs/toolkit/query'
import type { Middleware, Reducer, UnknownAction } from '@reduxjs/toolkit'
import type { StoreExtra } from './base-query'

export interface CreateStoreOptions<State, PreloadedState = State> {
  reducer: Reducer<State, UnknownAction, PreloadedState>
  /** The API slice; its middleware is added to the store. */
  api: { middleware: Middleware }
  /** The API base URL, handed to the base query as the thunk extra argument. */
  baseUrl: string
  /** Whether Redux DevTools may connect. Apps pass `import.meta.env.DEV`: the state will hold the token. */
  devTools: boolean
  preloadedState?: PreloadedState
}

/**
 * Builds the app's store. The app derives `AppStore`, `AppDispatch` and `RootState` from what the
 * function that calls this returns.
 */
const createStore = <State, PreloadedState = State>({
  reducer,
  api,
  baseUrl,
  devTools,
  preloadedState,
}: CreateStoreOptions<State, PreloadedState>) => {
  const extra: StoreExtra = { baseUrl }
  const store = configureStore({
    reducer,
    preloadedState,
    devTools,
    middleware: (getDefaultMiddleware) =>
      getDefaultMiddleware({ thunk: { extraArgument: extra } }).concat(api.middleware),
  })
  setupListeners(store.dispatch)
  return store
}

export default createStore
