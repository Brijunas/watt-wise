import { Navigate, useLocation } from 'react-router'
import { readReturnTo, selectSessionStatus } from '@wattwise/core'
import { PageLoading } from '@wattwise/ui'
import { paths } from '@/config/routes'
import SignInForm from '@/features/accounts/components/sign-in-form'
import { useAppSelector } from '@/stores/hooks'

// A signed-in session leaves sign-in for the page the user came from (the router state's `from`),
// or home: that covers a successful sign-in and a signed-in user who opens the page.
const SignIn = () => {
  const status = useAppSelector((state) => selectSessionStatus(state))
  const location = useLocation()

  if (status === 'restoring') {
    return <PageLoading />
  }
  if (status === 'signedIn') {
    return <Navigate to={readReturnTo(location.state) ?? paths.home} replace />
  }
  return <SignInForm />
}

export default SignIn
