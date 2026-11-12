import { describe, expect, it } from 'vitest'
import {
  selectAccessToken,
  selectSessionStatus,
  sessionEnded,
  sessionSlice,
  sessionStarted,
} from './session-slice'

const reduce = (...actions: Parameters<typeof sessionSlice.reducer>[1][]) => {
  const state = actions.reduce(sessionSlice.reducer, undefined as never)
  return { session: state }
}

describe('sessionSlice', () => {
  it('starts restoring with no token', () => {
    const state = sessionSlice.reducer(undefined, { type: 'unknown' })
    expect(state).toMatchObject({ status: 'restoring', accessToken: null })
  })

  it('holds the token once a session started', () => {
    const state = reduce(sessionStarted({ accessToken: 'abc' }))
    expect(selectSessionStatus(state)).toBe('signedIn')
    expect(selectAccessToken(state)).toBe('abc')
  })

  it('drops the token when the session ended', () => {
    const state = reduce(sessionStarted({ accessToken: 'abc' }), sessionEnded())
    expect(selectSessionStatus(state)).toBe('signedOut')
    expect(selectAccessToken(state)).toBeNull()
  })
})
