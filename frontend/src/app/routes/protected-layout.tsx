import { Navigate, Outlet, useLocation } from 'react-router'
import { PageLoading } from '@wattwise/ui'
import { selectSessionStatus } from '@wattwise/core'
import { paths } from '@/config/routes'
import { useAppSelector } from '@/stores/hooks'

// The guard for pages that need a session. While the session is restored from the refresh cookie
// it shows a loader, so a reload never bounces a signed-in user to sign-in. A signed-out visitor
// goes to sign-in with the requested location in the router state (never in the URL), which the
// sign-in page returns to.
const ProtectedLayout = () => {
  const status = useAppSelector((state) => selectSessionStatus(state))
  const location = useLocation()

  if (status === 'restoring') {
    return <PageLoading />
  }
  if (status === 'signedOut') {
    return <Navigate to={paths.signIn} replace state={{ from: location }} />
  }
  return <Outlet />
}

export default ProtectedLayout
