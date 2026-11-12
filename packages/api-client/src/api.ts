import { selectSessionRevision, sessionEnded, sessionStarted } from '@wattwise/core'
import type { SessionRootState } from '@wattwise/core'
import type { FetchBaseQueryError, FetchBaseQueryMeta } from '@reduxjs/toolkit/query'
import { generatedApi } from './generated/api'

export type {
  AboutResponse,
  GetAboutApiResponse,
  Me,
  SessionResponse,
  SignedInResponse,
} from './generated/api'

/** An API error as the pages read it: `retryAfterSeconds` is set when the answer had `Retry-After`. */
export type ApiError = FetchBaseQueryError & { retryAfterSeconds?: number }

// The session revision at a point in time. A request that changes the session after its answer
// arrives applies the change only if the revision is the one it started with, so a late answer
// can't undo a sign-out or a newer sign-in (the rule of the shared refresh).
const revisionOf = (thunk: { getState: () => unknown }) =>
  selectSessionRevision(thunk.getState() as SessionRootState)

// Adds the whole seconds of a numeric `Retry-After` header (a lockout's wait) to the error; a
// missing or non-numeric value adds nothing.
const withRetryAfter = (
  error: FetchBaseQueryError,
  meta: FetchBaseQueryMeta | undefined,
): ApiError => {
  const header = meta?.response?.headers.get('Retry-After')?.trim()
  if (header === undefined || !/^\d+$/.test(header)) {
    return error
  }
  return { ...error, retryAfterSeconds: Number(header) }
}

// Cache tags are part of the API contract, so both apps share them. Add a tag type here, then
// `providesTags` on the query and `invalidatesTags` on the mutations that change it.
//
// The session endpoints also keep `@wattwise/core`'s session state in step: the access token a
// sign-in or password change returns is stored, and a sign-out clears it and the API cache.
export const api = generatedApi.enhanceEndpoints({
  addTagTypes: ['About', 'Session'],
  endpoints: {
    getAbout: { providesTags: ['About'] },
    signIn: {
      transformErrorResponse: withRetryAfter,
      onQueryStarted: async (_arg, { dispatch, queryFulfilled }) => {
        await queryFulfilled.then(
          ({ data }) => dispatch(sessionStarted({ accessToken: data.accessToken })),
          () => undefined,
        )
      },
    },
    // A change ends every session and starts a new one for the caller, so the token is replaced.
    changePassword: {
      transformErrorResponse: withRetryAfter,
      onQueryStarted: async (_arg, thunk) => {
        const { dispatch, queryFulfilled } = thunk
        const revision = revisionOf(thunk)
        await queryFulfilled.then(
          ({ data }) => {
            if (revisionOf(thunk) === revision) {
              dispatch(sessionStarted({ accessToken: data.accessToken }))
            }
          },
          () => undefined,
        )
      },
    },
    // Signing out always ends the session here, even when the request failed (a network error):
    // the user asked to leave. The server succeeds with no or an unknown cookie.
    signOut: {
      onQueryStarted: async (_arg, { dispatch, queryFulfilled }) => {
        await queryFulfilled.catch(() => undefined)
        dispatch(sessionEnded())
        dispatch(api.util.resetApiState())
      },
    },
    listSessions: { providesTags: ['Session'] },
    // Ending the session this device uses ends it here too, like a sign-out. Any other session only
    // changes the list, which is refetched.
    endSession: {
      onQueryStarted: async (arg, thunk) => {
        const { dispatch, queryFulfilled } = thunk
        const revision = revisionOf(thunk)
        const cached = api.endpoints.listSessions.select()(thunk.getState() as never).data
        const endedCurrent = cached?.some((item) => item.id === arg.id && item.current) ?? false
        await queryFulfilled.then(
          () => {
            if (revisionOf(thunk) !== revision) {
              return
            }
            if (endedCurrent) {
              dispatch(sessionEnded())
              dispatch(api.util.resetApiState())
            } else {
              dispatch(api.util.invalidateTags(['Session']))
            }
          },
          () => undefined,
        )
      },
    },
    // Only a success ends the session: on a failure the sessions are still alive, so the user stays
    // signed in and can try again.
    signOutEverywhere: {
      onQueryStarted: async (_arg, thunk) => {
        const { dispatch, queryFulfilled } = thunk
        const revision = revisionOf(thunk)
        await queryFulfilled.then(
          () => {
            if (revisionOf(thunk) !== revision) {
              return
            }
            dispatch(sessionEnded())
            dispatch(api.util.resetApiState())
          },
          () => undefined,
        )
      },
    },
  },
})

export const {
  useGetAboutQuery,
  useGetMeQuery,
  useRegisterMutation,
  useConfirmEmailMutation,
  useResendConfirmationMutation,
  useSignInMutation,
  useSignOutMutation,
  useForgotPasswordMutation,
  useResetPasswordMutation,
  useChangePasswordMutation,
  useListSessionsQuery,
  useEndSessionMutation,
  useSignOutEverywhereMutation,
} = api
