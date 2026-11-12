import type { UnknownAction } from '@reduxjs/toolkit'
import type { RefreshedSession } from './session/refresh-session'

/** The thunk extra argument of the store: what the store factory hands to thunks and the base query. */
export interface StoreExtra {
  baseUrl: string
  /** The refresh in progress, shared by everything in this store that needs a new token. */
  refresh: { inFlight: Promise<RefreshedSession | null> | null }
  /** The API slice's `util.resetApiState()`, so core can clear the cache without importing the slice. */
  resetApiState: () => UnknownAction
}
