import { zodResolver } from '@hookform/resolvers/zod'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Link from '@mui/material/Link'
import Stack from '@mui/material/Stack'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link as RouterLink } from 'react-router'
import { useResendConfirmationMutation, useSignInMutation } from '@wattwise/api-client'
import { errorCode, signInSchema } from '@wattwise/core'
import type { SignInInput, SignInValues } from '@wattwise/core'
import { SignInFields } from '@wattwise/ui'
import { paths } from '@/config/routes'
import useErrorText from '../use-error-text'
import useMounted from '@/hooks/use-mounted'
import AccountPage from './account-page'

/**
 * The sign-in page's form. A successful answer stores the session (api-client does that); the
 * route sees the signed-in session and leaves for the page the user came from.
 */
const SignInForm = ({ notice }: { notice: 'passwordReset' | undefined }) => {
  const { t } = useTranslation('accounts')
  const { t: tAuth } = useTranslation('auth')
  const errorText = useErrorText()
  const mountedRef = useMounted()
  const [signIn, { isLoading }] = useSignInMutation()
  const [resend, { isLoading: isResending }] = useResendConfirmationMutation()
  const [failure, setFailure] = useState<{ error: unknown } | null>(null)
  const [resent, setResent] = useState(false)
  const { control, handleSubmit, getValues } = useForm<SignInInput, unknown, SignInValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { email: '', password: '' },
  })

  const onValid = async (values: SignInValues) => {
    setFailure(null)
    setResent(false)
    const result = await signIn({ body: values })
    if (mountedRef.current && 'error' in result) {
      setFailure({ error: result.error })
    }
  }

  const onResend = async () => {
    setResent(false)
    const result = await resend({ body: { email: getValues('email').trim() } })
    if (!mountedRef.current) {
      return
    }
    if ('error' in result) {
      setFailure({ error: result.error })
    } else {
      setFailure(null)
      setResent(true)
    }
  }

  const unconfirmed = failure !== null && errorCode(failure.error) === 'Accounts.EmailNotConfirmed'

  return (
    <AccountPage title={t('signIn.title')}>
      {notice === 'passwordReset' ? (
        <Alert severity="success" role="status">
          {t('signIn.passwordReset')}
        </Alert>
      ) : null}
      <SignInFields
        control={control}
        busy={isLoading}
        onSubmit={(event) => {
          void handleSubmit(onValid)(event)
        }}
        forgotPasswordLink={
          <Link component={RouterLink} to={paths.forgotPassword}>
            {tAuth('signIn.forgotPassword')}
          </Link>
        }
        error={
          failure === null && !resent ? undefined : (
            <Stack spacing={1}>
              {failure === null ? null : <Alert severity="error">{errorText(failure.error)}</Alert>}
              {unconfirmed ? (
                <Button variant="outlined" loading={isResending} onClick={() => void onResend()}>
                  {t('signIn.resendLink')}
                </Button>
              ) : null}
              {resent ? (
                <Alert severity="success" role="status">
                  {t('signIn.resent')}
                </Alert>
              ) : null}
            </Stack>
          )
        }
      />
      <Link component={RouterLink} to={paths.signUp}>
        {tAuth('signIn.createAccount')}
      </Link>
    </AccountPage>
  )
}

export default SignInForm
