import { createStore } from '@wattwise/core'
import { api } from '@wattwise/api-client'
import { env } from '@/config/env'
import { rootReducer } from './root-reducer'
import type { RootState } from './root-reducer'

const createAppStore = (preloadedState?: Partial<RootState>) => {
  return createStore({
    reducer: rootReducer,
    api,
    baseUrl: env.apiBaseUrl,
    devTools: import.meta.env.DEV,
    preloadedState,
  })
}

export type AppStore = ReturnType<typeof createAppStore>
export type AppDispatch = AppStore['dispatch']

export default createAppStore
