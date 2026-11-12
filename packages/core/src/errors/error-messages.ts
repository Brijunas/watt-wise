import type { ErrorKey } from '@wattwise/i18n'

// The ProblemDetails `code`s the account pages show a message for (docs: backend error-handling.md,
// accounts.md). A Record keeps the table and the `errors` namespace in step at type-check time.
const codeKeys: Record<string, ErrorKey> = {
  'Accounts.InvalidCredentials': 'invalidCredentials',
  'Accounts.EmailNotConfirmed': 'emailNotConfirmed',
  'Accounts.LockedOut': 'lockedOutNoTime',
  'Accounts.InvalidEmailLink': 'invalidEmailLink',
  'Accounts.AccountNotFound': 'accountNotFound',
  'Accounts.PasswordChangeConflict': 'passwordChangeConflict',
  'Accounts.SessionNotFound': 'sessionNotFound',
  'Validation.Failed': 'validationFailed',
  'General.Unexpected': 'unexpected',
}

/** A translatable message: the `errors` key and the parameters of its text. */
export interface ErrorMessage {
  key: ErrorKey
  params?: { count: number }
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null && !Array.isArray(value)

const bodyOf = (error: unknown): Record<string, unknown> | undefined => {
  if (!isRecord(error) || !isRecord(error.data)) {
    return undefined
  }
  return error.data
}

/** The machine-readable `code` of a ProblemDetails error, if the error carries one. */
export const errorCode = (error: unknown): string | undefined => {
  const code = bodyOf(error)?.code
  return typeof code === 'string' ? code : undefined
}

/**
 * The field errors of a validation ProblemDetails: camelCase field path to its messages. Entries
 * that aren't a list of texts are left out; an error without any gives an empty object.
 */
export const errorFieldMessages = (error: unknown): Record<string, string[]> => {
  const errors = bodyOf(error)?.errors
  if (!isRecord(errors)) {
    return {}
  }
  const result: Record<string, string[]> = {}
  for (const [field, messages] of Object.entries(errors)) {
    if (Array.isArray(messages) && messages.every((item) => typeof item === 'string')) {
      result[field] = messages
    }
  }
  return result
}

/**
 * The whole minutes to show for a lockout: `retryAfterSeconds` rounded up, at least 1. Undefined
 * when the seconds are absent or not a finite number.
 */
export const lockoutMinutes = (retryAfterSeconds: number | undefined): number | undefined => {
  if (retryAfterSeconds === undefined || !Number.isFinite(retryAfterSeconds)) {
    return undefined
  }
  return Math.max(1, Math.ceil(retryAfterSeconds / 60))
}

/**
 * Maps a query or mutation error to the `errors` message to show. A known `code` wins; a locked-out
 * account with a known wait gets the text with its minutes. `FETCH_ERROR` and `TIMEOUT_ERROR` are
 * the network; anything else is the generic message.
 */
export const errorMessage = (error: unknown): ErrorMessage => {
  const code = errorCode(error)
  const known =
    code === undefined ? undefined : Object.hasOwn(codeKeys, code) ? codeKeys[code] : undefined
  if (known !== undefined) {
    if (known === 'lockedOutNoTime') {
      const count = lockoutMinutes(
        isRecord(error) && typeof error.retryAfterSeconds === 'number'
          ? error.retryAfterSeconds
          : undefined,
      )
      return count === undefined
        ? { key: 'lockedOutNoTime' }
        : { key: 'lockedOut', params: { count } }
    }
    return { key: known }
  }
  if (isRecord(error) && (error.status === 'FETCH_ERROR' || error.status === 'TIMEOUT_ERROR')) {
    return { key: 'network' }
  }
  return { key: 'generic' }
}
