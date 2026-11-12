import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  useEndSessionMutation,
  useListSessionsQuery,
  useSignOutEverywhereMutation,
} from '@wattwise/api-client'
import { errorCode, errorMessage } from '@wattwise/core'
import type { ErrorMessage } from '@wattwise/core'
import { PageLoading, useMounted } from '@wattwise/ui'
import SessionItem from './session-item'

// The devices page body: the sessions list with its actions. Ending the session this device uses,
// or signing out everywhere, ends the client session in the api-client; the protected guard then
// leaves the page, so nothing here handles that success.
const DeviceList = () => {
  const { t } = useTranslation('devices')
  const { t: tErrors } = useTranslation('errors')
  const { data, error, isError, refetch } = useListSessionsQuery()
  const [endSession, endState] = useEndSessionMutation()
  const [signOutEverywhere, signOutState] = useSignOutEverywhereMutation()
  const [failure, setFailure] = useState<ErrorMessage | null>(null)

  const mountedRef = useMounted()
  const busy = endState.isLoading || signOutState.isLoading

  const handleEnd = async (id: string) => {
    setFailure(null)
    const result = await endSession({ id })
    if (!result.error || !mountedRef.current) {
      return
    }
    setFailure(errorMessage(result.error))
    if (errorCode(result.error) === 'Accounts.SessionNotFound') {
      void refetch()
    }
  }

  const handleSignOutEverywhere = async () => {
    setFailure(null)
    const result = await signOutEverywhere()
    if (result.error && mountedRef.current) {
      setFailure(errorMessage(result.error))
    }
  }

  if (isError) {
    const message = errorMessage(error)
    return (
      <Stack spacing={2} sx={{ alignItems: 'flex-start' }}>
        <Alert severity="error">{tErrors(message.key, message.params ?? {})}</Alert>
        <Button
          variant="outlined"
          onClick={() => {
            void refetch()
          }}
        >
          {t('retry')}
        </Button>
      </Stack>
    )
  }
  if (data === undefined) {
    return <PageLoading />
  }

  return (
    <Stack spacing={2}>
      <Typography color="text.secondary">{t('intro')}</Typography>
      {failure && <Alert severity="error">{tErrors(failure.key, failure.params ?? {})}</Alert>}
      <Stack
        component="ul"
        aria-label={t('title')}
        spacing={2}
        sx={{ listStyle: 'none', m: 0, p: 0 }}
      >
        {data.map((session) => (
          <SessionItem
            key={session.id}
            session={session}
            busy={busy}
            onEnd={(id) => {
              void handleEnd(id)
            }}
          />
        ))}
      </Stack>
      <Button
        variant="outlined"
        color="error"
        disabled={busy}
        sx={{ alignSelf: 'flex-start' }}
        onClick={() => {
          void handleSignOutEverywhere()
        }}
      >
        {t('signOutEverywhere')}
      </Button>
    </Stack>
  )
}

export default DeviceList
