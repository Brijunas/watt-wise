import type { ThunkAction, UnknownAction } from '@reduxjs/toolkit'
import type { StoreExtra } from '../store-extra'
import refreshSession from './refresh-session'
import { selectSessionStatus, sessionEnded } from './session-slice'
import type { SessionRootState } from './session-slice'

type RestoreThunk = ThunkAction<Promise<void>, SessionRootState, StoreExtra, UnknownAction>

/**
 * Dispatched once when the app starts: the refresh cookie, if the browser has a valid one, brings
 * the session back after a reload. Ends in `signedIn` or `signedOut` (a failure of any kind, the
 * network included, is `signedOut`), and never passes through `signedOut` on the way to `signedIn`.
 */
const restoreSession =
  (): RestoreThunk =>
  async (dispatch, getState): Promise<void> => {
    await dispatch(refreshSession())
    if (selectSessionStatus(getState()) === 'restoring') {
      dispatch(sessionEnded())
    }
  }

export default restoreSession
