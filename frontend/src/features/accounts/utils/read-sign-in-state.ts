export interface SignInState {
  /** Where to go after signing in: path, query and hash of the page that needed the session. */
  returnTo: string | undefined
  /** A notice to show above the form. */
  notice: 'passwordReset' | undefined
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === 'object' && value !== null

const text = (value: unknown): string => (typeof value === 'string' ? value : '')

// The router state a page left for sign-in: the protected layout's `from` location and a notice.
// The return-to is only ever a location in this app: a path that starts with a single slash.
const readSignInState = (state: unknown): SignInState => {
  const record = isRecord(state) ? state : {}
  const from = isRecord(record.from) ? record.from : undefined
  const pathname = text(from?.pathname)
  const valid = pathname.startsWith('/') && !pathname.startsWith('//')
  return {
    returnTo: valid ? `${pathname}${text(from?.search)}${text(from?.hash)}` : undefined,
    notice: record.notice === 'passwordReset' ? 'passwordReset' : undefined,
  }
}

export default readSignInState
