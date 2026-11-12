import type { ThunkAction, UnknownAction } from '@reduxjs/toolkit'
import type { StoreExtra } from '../store-extra'
import { selectSessionRevision, sessionEnded, sessionStarted } from './session-slice'
import type { SessionRootState } from './session-slice'

/** One lock name for the whole origin: Web Locks are scoped per origin, so each app has its own. */
export const refreshLockName = 'wattwise-refresh'
export const refreshPath = '/api/v1/auth/refresh'

/** The body of a successful refresh, the same as sign-in's. */
export interface RefreshedSession {
  accessToken: string
  accessTokenExpiresAt: string
}

type RefreshThunk = ThunkAction<
  Promise<RefreshedSession | null>,
  SessionRootState,
  StoreExtra,
  UnknownAction
>

// A browser-wide lock makes a second tab wait for the first one's refresh and then send the cookie
// that refresh rotated, instead of the used one (which the server treats as reuse and ends the
// session). Without Web Locks (old browsers, jsdom) the refresh runs directly.
const withRefreshLock = <T>(run: () => Promise<T>): Promise<T> => {
  const locks =
    typeof navigator === 'undefined' ? undefined : (navigator.locks as LockManager | undefined)
  return locks ? locks.request(refreshLockName, run) : run()
}

// A rejected refresh (the cookie is missing, used, expired or the session ended) ends the session.
// A rate limit or timeout, a server error or a network failure says nothing about the session.
const rejectsSession = (status: number) =>
  status >= 400 && status < 500 && status !== 408 && status !== 429

/**
 * Exchanges the refresh cookie for a new access token and stores it. Resolves the response body on success and `null` otherwise.
 * A rejection ends the session and clears the API cache; any other failure resolves `false` and
 * leaves the session as it is. Callers in one store share one refresh, and every refresh runs inside
 * the origin's Web Lock.
 */
const refreshSession =
  (): RefreshThunk =>
  (dispatch, getState, extra): Promise<RefreshedSession | null> => {
    if (extra.refresh.inFlight) {
      return extra.refresh.inFlight
    }

    // Taken when the refresh is asked for, not when the lock is granted: a sign-out that happens
    // while this tab queues behind another tab's refresh must not be undone by the queued one.
    const revisionBefore = selectSessionRevision(getState())

    const run = async (): Promise<RefreshedSession | null> => {
      if (selectSessionRevision(getState()) !== revisionBefore) {
        return null
      }
      let response: Response
      try {
        response = await fetch(`${extra.baseUrl}${refreshPath}`, {
          method: 'POST',
          credentials: 'include',
        })
      } catch {
        return null
      }

      if (response.ok) {
        const body = (await response.json().catch(() => null)) as Partial<
          Record<keyof RefreshedSession, unknown>
        > | null
        if (
          typeof body?.accessToken !== 'string' ||
          typeof body.accessTokenExpiresAt !== 'string'
        ) {
          return null
        }
        // A sign-out or sign-in that happened while this was in flight wins over its answer.
        if (selectSessionRevision(getState()) !== revisionBefore) {
          return null
        }
        dispatch(sessionStarted({ accessToken: body.accessToken }))
        return { accessToken: body.accessToken, accessTokenExpiresAt: body.accessTokenExpiresAt }
      }

      if (rejectsSession(response.status) && selectSessionRevision(getState()) === revisionBefore) {
        dispatch(sessionEnded())
        dispatch(extra.resetApiState())
      }
      return null
    }

    const refresh = withRefreshLock(run).finally(() => {
      extra.refresh.inFlight = null
    })
    extra.refresh.inFlight = refresh
    return refresh
  }

export default refreshSession
