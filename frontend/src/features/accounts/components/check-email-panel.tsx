import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useResendConfirmationMutation } from '@wattwise/api-client'
import { AccountPage, useErrorText, useMounted } from '@wattwise/ui'

/** `email` is the address the user signed up with; without it (a reload) the text is generic. */
const CheckEmailPanel = ({ email }: { email: string | undefined }) => {
  const { t } = useTranslation('accounts')
  const errorText = useErrorText()
  const mountedRef = useMounted()
  const [resend, { isLoading }] = useResendConfirmationMutation()
  const [outcome, setOutcome] = useState<{ error: unknown } | 'sent' | null>(null)

  const onResend = async () => {
    if (email === undefined) {
      return
    }
    setOutcome(null)
    const result = await resend({ body: { email } })
    if (mountedRef.current) {
      setOutcome('error' in result ? { error: result.error } : 'sent')
    }
  }

  return (
    <AccountPage title={t('checkEmail.title')}>
      <Typography>
        {email === undefined ? t('checkEmail.sent') : t('checkEmail.sentTo', { email })}
      </Typography>
      {outcome === 'sent' ? (
        <Alert severity="success" role="status">
          {t('checkEmail.resent')}
        </Alert>
      ) : null}
      {outcome !== null && outcome !== 'sent' ? (
        <Alert severity="error">{errorText(outcome.error)}</Alert>
      ) : null}
      {email === undefined ? null : (
        <Stack direction="row">
          <Button variant="outlined" loading={isLoading} onClick={() => void onResend()}>
            {t('checkEmail.resend')}
          </Button>
        </Stack>
      )}
    </AccountPage>
  )
}

export default CheckEmailPanel
