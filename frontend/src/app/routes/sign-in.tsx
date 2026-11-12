import { Navigate, useLocation } from 'react-router'
import { selectSessionStatus } from '@wattwise/core'
import { PageLoading } from '@wattwise/ui'
import { paths } from '@/config/routes'
import SignInForm from '@/features/accounts/components/sign-in-form'
import readSignInState from '@/features/accounts/utils/read-sign-in-state'
import { useAppSelector } from '@/stores/hooks'

// A signed-in session leaves sign-in for the page the user came from (the router state's `from`),
// or home: that covers a successful sign-in and a signed-in user who opens the page.
const SignIn = () => {
  const status = useAppSelector((state) => selectSessionStatus(state))
  const location = useLocation()
  const { returnTo, notice } = readSignInState(location.state as unknown)

  // Neither the form nor a redirect while the session is restored from the refresh cookie.
  if (status === 'restoring') {
    return <PageLoading />
  }
  if (status === 'signedIn') {
    return <Navigate to={returnTo ?? paths.home} replace />
  }
  return <SignInForm notice={notice} />
}

export default SignIn
