import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, Outlet, useLocation } from 'react-router'
import { useGetMeQuery } from '@wattwise/api-client'
import { selectSessionStatus } from '@wattwise/core'
import { PageLoading, useErrorText } from '@wattwise/ui'
import { adminAccessPermission } from '@/config/permissions'
import { paths } from '@/config/routes'
import { useAppSelector } from '@/stores/hooks'
import DenyAccess from './deny-access'

// The guard around every admin page. While the session is restored from the refresh cookie it
// shows a loader, so a reload never bounces a signed-in user. A signed-out visitor goes to sign-in
// with the requested location in the router state. A signed-in user is checked against a fresh
// `/me` (never stale cache: a role may have been revoked): without the admin permission the
// session is ended and the user lands on the no-access page.
const AdminLayout = () => {
  const { t } = useTranslation('accounts')
  const errorText = useErrorText()
  const status = useAppSelector((state) => selectSessionStatus(state))
  const location = useLocation()
  const [denied, setDenied] = useState(false)
  // What the cache held when this guard first saw a signed-in session. Data is fresh only from a
  // request newer than a settled entry (`requestId` differs): the refetch on mount starts in an
  // effect, so the first render can still show an older answer. A request already in flight
  // (`pending`) was started for this session and RTK Query reuses it instead of forcing another, so
  // its answer counts as fresh whatever its id.
  const [baseline, setBaseline] = useState<{
    requestId: string | undefined
    pending: boolean
  } | null>(null)
  const { data, error, isFetching, requestId, refetch } = useGetMeQuery(undefined, {
    skip: status !== 'signedIn',
    refetchOnMountOrArgChange: true,
  })

  if (status === 'signedIn' && baseline === null) {
    setBaseline({ requestId, pending: isFetching })
  }
  const fresh =
    baseline !== null &&
    (baseline.pending || requestId !== baseline.requestId) &&
    !isFetching &&
    error === undefined &&
    data !== undefined
  const missingPermission = fresh && !data.permissions.includes(adminAccessPermission)

  // Once denied, stay on this branch: the sign-out turns the session `signedOut`, which must not
  // send the user to sign-in instead of no-access.
  if (denied || (status === 'signedIn' && missingPermission)) {
    return (
      <DenyAccess
        onDenied={() => {
          setDenied(true)
        }}
      />
    )
  }
  if (status === 'restoring') {
    return <PageLoading />
  }
  if (status === 'signedOut') {
    return <Navigate to={paths.signIn} replace state={{ from: location }} />
  }
  if (isFetching) {
    return <PageLoading />
  }
  if (error !== undefined) {
    return (
      <Stack spacing={2} sx={{ alignItems: 'flex-start' }}>
        <Alert severity="error">{errorText(error)}</Alert>
        <Button
          variant="outlined"
          onClick={() => {
            void refetch()
          }}
        >
          {t('access.retry')}
        </Button>
      </Stack>
    )
  }
  return fresh ? <Outlet /> : <PageLoading />
}

export default AdminLayout
