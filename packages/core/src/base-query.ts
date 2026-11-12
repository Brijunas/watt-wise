import type { ThunkDispatch, UnknownAction } from '@reduxjs/toolkit'
import { fetchBaseQuery } from '@reduxjs/toolkit/query'
import type { BaseQueryFn, FetchArgs, FetchBaseQueryError } from '@reduxjs/toolkit/query'
import refreshSession, { refreshPath } from './session/refresh-session'
import { selectAccessToken } from './session/session-slice'
import type { SessionRootState } from './session/session-slice'
import type { StoreExtra } from './store-extra'

const authPath = '/api/v1/auth'
const signInPath = '/api/v1/auth/sign-in'

// Only the auth endpoints take the httpOnly refresh cookie, and the cookie is limited to that path.
const isAuthUrl = (url: string) => url === authPath || url.startsWith(`${authPath}/`)

// A 401 from these says nothing about an expired access token, so it never starts a refresh.
const answersWithoutRefresh = (url: string) => url === refreshPath || url === signInPath

/**
 * The base query of the single API slice. It reads the API base URL from the thunk extra argument
 * (`api.extra`), never from `import.meta.env`, so the app decides the URL.
 *
 * It sends the access token as a Bearer header. When a request that carried a token gets a 401, it
 * refreshes the session once (requests failing together share the one refresh) and retries once
 * with the new token; if the refresh fails, the 401 is returned and the session has ended. It
 * never loops.
 */
const baseQuery: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = async (
  args,
  api,
  extraOptions,
) => {
  const { baseUrl } = api.extra as StoreExtra
  const request: FetchArgs = typeof args === 'string' ? { url: args } : args
  const readToken = () => selectAccessToken(api.getState() as SessionRootState)

  const send = (token: string | null) => {
    const headers = new Headers(request.headers as HeadersInit | undefined)
    if (token) {
      headers.set('Authorization', `Bearer ${token}`)
    }
    return fetchBaseQuery({ baseUrl })(
      { ...request, headers, ...(isAuthUrl(request.url) && { credentials: 'include' as const }) },
      api,
      extraOptions,
    )
  }

  // `api.dispatch` is typed for any thunk, which TypeScript 6 (ESLint) reads as not returning it.
  const refresh = () =>
    (api.dispatch as ThunkDispatch<SessionRootState, StoreExtra, UnknownAction>)(refreshSession())

  // The generated `refreshSession` endpoint goes through the shared refresh (Web Lock, one refresh
  // in flight, session handling) instead of a plain request. Its data is the refresh's response body.
  if (request.url === refreshPath) {
    const refreshed = await refresh()
    return refreshed
      ? { data: refreshed }
      : { error: { status: 'CUSTOM_ERROR', error: 'The session could not be refreshed' } }
  }

  const sentToken = readToken()
  const result = await send(sentToken)
  if (result.error?.status !== 401 || !sentToken || answersWithoutRefresh(request.url)) {
    return result
  }

  const current = readToken()
  if (current === null) {
    return result
  }
  // Another request already refreshed after this one was sent: only the retry is left.
  if (current === sentToken && !(await refresh())) {
    return result
  }
  return send(readToken())
}

export default baseQuery
