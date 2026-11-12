import { readReturnTo } from '@wattwise/core'

export interface SignInState {
  /** Where to go after signing in: path, query and hash of the page that needed the session. */
  returnTo: string | undefined
  /** A notice to show above the form. */
  notice: 'passwordReset' | undefined
}

// The router state a page left for sign-in: the protected layout's `from` location and a notice.
const readSignInState = (state: unknown): SignInState => {
  const notice =
    typeof state === 'object' && state !== null && 'notice' in state ? state.notice : undefined
  return {
    returnTo: readReturnTo(state),
    notice: notice === 'passwordReset' ? 'passwordReset' : undefined,
  }
}

export default readSignInState
