import { zodResolver } from '@hookform/resolvers/zod'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Link from '@mui/material/Link'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link as RouterLink } from 'react-router'
import { useForgotPasswordMutation } from '@wattwise/api-client'
import { FormTextField } from '@wattwise/ui'
import { paths } from '@/config/routes'
import useErrorText from '../use-error-text'
import useMounted from '@/hooks/use-mounted'
import { emailSchema } from '../schemas/email-schema'
import type { EmailInput, EmailValues } from '../schemas/email-schema'
import AccountPage from './account-page'

const ForgotPasswordForm = () => {
  const { t } = useTranslation('accounts')
  const { t: tAuth } = useTranslation('auth')
  const errorText = useErrorText()
  const mountedRef = useMounted()
  const [forgotPassword, { isLoading }] = useForgotPasswordMutation()
  const [outcome, setOutcome] = useState<{ error: unknown } | 'sent' | null>(null)
  const { control, handleSubmit } = useForm<EmailInput, unknown, EmailValues>({
    resolver: zodResolver(emailSchema),
    defaultValues: { email: '' },
  })

  const onValid = async (values: EmailValues) => {
    setOutcome(null)
    const result = await forgotPassword({ body: values })
    if (mountedRef.current) {
      setOutcome('error' in result ? { error: result.error } : 'sent')
    }
  }

  return (
    <AccountPage title={t('forgotPassword.title')}>
      <Typography>{t('forgotPassword.intro')}</Typography>
      <form
        noValidate
        aria-busy={isLoading}
        onSubmit={(event) => {
          void handleSubmit(onValid)(event)
        }}
      >
        <Stack spacing={2}>
          <FormTextField
            control={control}
            name="email"
            label={tAuth('fields.email')}
            type="email"
            autoComplete="email"
          />
          {outcome === 'sent' ? (
            <Alert severity="success" role="status">
              {t('forgotPassword.sent')}
            </Alert>
          ) : null}
          {outcome !== null && outcome !== 'sent' ? (
            <Alert severity="error">{errorText(outcome.error)}</Alert>
          ) : null}
          <Button type="submit" variant="contained" size="large" fullWidth loading={isLoading}>
            {t('forgotPassword.submit')}
          </Button>
        </Stack>
      </form>
      <Link component={RouterLink} to={paths.signIn}>
        {t('forgotPassword.backToSignIn')}
      </Link>
    </AccountPage>
  )
}

export default ForgotPasswordForm
