import { zodResolver } from '@hookform/resolvers/zod'
import Alert from '@mui/material/Alert'
import Link from '@mui/material/Link'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useSignInMutation } from '@wattwise/api-client'
import { signInSchema } from '@wattwise/core'
import type { SignInInput, SignInValues } from '@wattwise/core'
import { AccountPage, SignInFields, useErrorText, useMounted } from '@wattwise/ui'
import { env } from '@/config/env'
import { frontendPaths } from '@/config/routes'

/**
 * The sign-in page's form. A successful answer stores the session (api-client does that); the
 * route sees the signed-in session and leaves for the page the user came from. The forgot-password
 * link goes to the frontend app, in the same tab.
 */
const SignInForm = () => {
  const { t } = useTranslation('auth')
  const errorText = useErrorText()
  const mountedRef = useMounted()
  const [signIn, { isLoading }] = useSignInMutation()
  const [failure, setFailure] = useState<{ error: unknown } | null>(null)
  const { control, handleSubmit } = useForm<SignInInput, unknown, SignInValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { email: '', password: '' },
  })

  const onValid = async (values: SignInValues) => {
    setFailure(null)
    const { error } = await signIn({ body: values })
    if (mountedRef.current && error !== undefined) {
      setFailure({ error })
    }
  }

  return (
    <AccountPage title={t('signIn.title')}>
      <SignInFields
        control={control}
        busy={isLoading}
        onSubmit={(event) => {
          void handleSubmit(onValid)(event)
        }}
        forgotPasswordLink={
          <Link href={new URL(frontendPaths.forgotPassword, env.frontendUrl).href}>
            {t('signIn.forgotPassword')}
          </Link>
        }
        error={
          failure === null ? undefined : <Alert severity="error">{errorText(failure.error)}</Alert>
        }
      />
    </AccountPage>
  )
}

export default SignInForm
