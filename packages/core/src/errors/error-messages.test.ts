import { describe, expect, it } from 'vitest'
import { errorCode, errorFieldMessages, errorMessage, lockoutMinutes } from './error-messages'

const problem = (code: string, extra: object = {}) => ({ status: 400, data: { code }, ...extra })

describe('errorMessage', () => {
  it.each([
    ['Accounts.InvalidCredentials', 'invalidCredentials'],
    ['Accounts.EmailNotConfirmed', 'emailNotConfirmed'],
    ['Accounts.LockedOut', 'lockedOutNoTime'],
    ['Accounts.InvalidEmailLink', 'invalidEmailLink'],
    ['Accounts.AccountNotFound', 'accountNotFound'],
    ['Accounts.PasswordChangeConflict', 'passwordChangeConflict'],
    ['Accounts.SessionNotFound', 'sessionNotFound'],
    ['Validation.Failed', 'validationFailed'],
    ['General.Unexpected', 'unexpected'],
  ])('maps %s to %s', (code, key) => {
    expect(errorMessage(problem(code))).toEqual({ key })
  })

  it('gives a lockout its minutes when the wait is known', () => {
    expect(errorMessage(problem('Accounts.LockedOut', { retryAfterSeconds: 61 }))).toEqual({
      key: 'lockedOut',
      params: { count: 2 },
    })
  })

  it.each(['FETCH_ERROR', 'TIMEOUT_ERROR'])('maps %s to the network message', (status) => {
    expect(errorMessage({ status, error: 'x' })).toEqual({ key: 'network' })
  })

  it.each([
    ['an unknown code', problem('Something.Else')],
    ['a code named like an object member', problem('toString')],
    ['a status without a body', { status: 500, data: 'oops' }],
    ['a serialized error', { message: 'boom' }],
    ['undefined', undefined],
  ])('maps %s to the generic message', (_name, error) => {
    expect(errorMessage(error)).toEqual({ key: 'generic' })
  })

  it('prefers a known code over the network status', () => {
    expect(errorMessage({ status: 'FETCH_ERROR', data: { code: 'General.Unexpected' } })).toEqual({
      key: 'unexpected',
    })
  })
})

describe('lockoutMinutes', () => {
  it.each([
    [1, 1],
    [59, 1],
    [60, 1],
    [61, 2],
    [120, 2],
    [0, 1],
    [900, 15],
  ])('rounds %i seconds up to %i minutes', (seconds, minutes) => {
    expect(lockoutMinutes(seconds)).toBe(minutes)
  })

  it.each([[undefined], [Number.NaN], [Number.POSITIVE_INFINITY]])('gives %s no minutes', (v) => {
    expect(lockoutMinutes(v)).toBeUndefined()
  })
})

describe('errorCode', () => {
  it('reads the code of a ProblemDetails error', () => {
    expect(errorCode(problem('Accounts.LockedOut'))).toBe('Accounts.LockedOut')
  })

  it.each([[undefined], [{ status: 500 }], [{ data: { code: 5 } }], [{ data: 'x' }], ['x']])(
    'gives %j no code',
    (error) => {
      expect(errorCode(error)).toBeUndefined()
    },
  )
})

describe('errorFieldMessages', () => {
  it('reads the field map', () => {
    const error = { status: 400, data: { errors: { newPassword: ['too short', 'no digit'] } } }
    expect(errorFieldMessages(error)).toEqual({ newPassword: ['too short', 'no digit'] })
  })

  it('leaves out entries that are not lists of texts', () => {
    const error = { data: { errors: { a: ['x'], b: 'y', c: [1] } } }
    expect(errorFieldMessages(error)).toEqual({ a: ['x'] })
  })

  it.each([[undefined], [{ data: {} }], [{ data: { errors: ['x'] } }]])(
    'gives %j none',
    (error) => {
      expect(errorFieldMessages(error)).toEqual({})
    },
  )
})
