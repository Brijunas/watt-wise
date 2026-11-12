import { createSlice } from '@reduxjs/toolkit'
import type { PayloadAction } from '@reduxjs/toolkit'

/**
 * `restoring` until the refresh at app start has settled, so the app shows neither sign-in nor
 * signed-in pages yet; then `signedIn` or `signedOut`.
 */
export type SessionStatus = 'restoring' | 'signedIn' | 'signedOut'

export interface SessionState {
  status: SessionStatus
  /** The access token. In memory only: the store is never persisted (docs/security.md, Tokens). */
  accessToken: string | null
  /**
   * Counts every start and end of a session. A refresh applies its result only if the revision is
   * the one it started with, so a late answer can't undo a sign-out or a newer sign-in.
   */
  revision: number
}

const initialState: SessionState = { status: 'restoring', accessToken: null, revision: 0 }

export const sessionSlice = createSlice({
  name: 'session',
  initialState,
  reducers: {
    sessionStarted: (state, action: PayloadAction<{ accessToken: string }>): SessionState => ({
      status: 'signedIn',
      accessToken: action.payload.accessToken,
      revision: state.revision + 1,
    }),
    sessionEnded: (state): SessionState => ({
      status: 'signedOut',
      accessToken: null,
      revision: state.revision + 1,
    }),
  },
  selectors: {
    selectAccessToken: (state) => state.accessToken,
    selectSessionStatus: (state) => state.status,
    selectSessionRevision: (state) => state.revision,
  },
})

export const { sessionStarted, sessionEnded } = sessionSlice.actions
export const { selectAccessToken, selectSessionRevision, selectSessionStatus } =
  sessionSlice.selectors

/** The part of the root state the session code reads; every app's root reducer has it. */
export interface SessionRootState {
  session: SessionState
}
