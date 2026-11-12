import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Link as RouterLink } from 'react-router'
import { useConfirmEmailMutation } from '@wattwise/api-client'
import { errorCode } from '@wattwise/core'
import { paths } from '@/config/routes'
import useErrorText from '../use-error-text'
import AccountPage from './account-page'
import ResendConfirmationForm from './resend-confirmation-form'

/** `id` and `token` come from the email link's query; a missing one makes the link invalid. */
const ConfirmEmailPanel = ({
  id,
  token,
}: {
  id: string | undefined
  token: string | undefined
}) => {
  const { t } = useTranslation('accounts')
  const errorText = useErrorText()
  // The link is single-use, so it must be sent once: StrictMode runs the effect twice, and the
  // ref (kept across that run) stops the second one.
  const [confirm, { isSuccess, error }] = useConfirmEmailMutation()
  const startedRef = useRef(false)
  const linkMissing = id === undefined || token === undefined

  useEffect(() => {
    if (startedRef.current || id === undefined || token === undefined) {
      return
    }
    startedRef.current = true
    void confirm({ body: { id, token } })
  }, [confirm, id, token])

  const invalid = linkMissing || errorCode(error) === 'Accounts.InvalidEmailLink'
  let body
  if (invalid) {
    body = (
      <>
        <Alert severity="error">{errorText({ data: { code: 'Accounts.InvalidEmailLink' } })}</Alert>
        <ResendConfirmationForm />
      </>
    )
  } else if (isSuccess) {
    body = (
      <>
        <Alert severity="success" role="status">
          {t('confirmEmail.success')}
        </Alert>
        <Stack direction="row">
          <Button component={RouterLink} to={paths.signIn} variant="contained">
            {t('confirmEmail.signIn')}
          </Button>
        </Stack>
      </>
    )
  } else if (error !== undefined) {
    body = <Alert severity="error">{errorText(error)}</Alert>
  } else {
    body = <Typography role="status">{t('confirmEmail.confirming')}</Typography>
  }

  return <AccountPage title={t('confirmEmail.title')}>{body}</AccountPage>
}

export default ConfirmEmailPanel
