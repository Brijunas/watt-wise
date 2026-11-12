import { sessionEnded, sessionStarted } from '@wattwise/core'
import { generatedApi } from './generated/api'

export type { AboutResponse, GetAboutApiResponse } from './generated/api'

// Cache tags are part of the API contract, so both apps share them. Add a tag type here, then
// `providesTags` on the query and `invalidatesTags` on the mutations that change it.
//
// The session endpoints also keep `@wattwise/core`'s session state in step: the access token a
// sign-in or password change returns is stored, and a sign-out clears it and the API cache.
export const api = generatedApi.enhanceEndpoints({
  addTagTypes: ['About'],
  endpoints: {
    getAbout: { providesTags: ['About'] },
    signIn: {
      onQueryStarted: async (_arg, { dispatch, queryFulfilled }) => {
        await queryFulfilled.then(
          ({ data }) => dispatch(sessionStarted({ accessToken: data.accessToken })),
          () => undefined,
        )
      },
    },
    // A change ends every session and starts a new one for the caller, so the token is replaced.
    changePassword: {
      onQueryStarted: async (_arg, { dispatch, queryFulfilled }) => {
        await queryFulfilled.then(
          ({ data }) => dispatch(sessionStarted({ accessToken: data.accessToken })),
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
    // Only a success ends the session: on a failure the sessions are still alive, so the user stays
    // signed in and can try again.
    signOutEverywhere: {
      onQueryStarted: async (_arg, { dispatch, queryFulfilled }) => {
        await queryFulfilled.then(
          () => {
            dispatch(sessionEnded())
            dispatch(api.util.resetApiState())
          },
          () => undefined,
        )
      },
    },
  },
})

export const { useGetAboutQuery } = api
