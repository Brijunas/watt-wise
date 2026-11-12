import { zodResolver } from '@hookform/resolvers/zod'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useResendConfirmationMutation } from '@wattwise/api-client'
import { FormTextField } from '@wattwise/ui'
import useErrorText from '../use-error-text'
import useMounted from '@/hooks/use-mounted'
import { emailSchema } from '../schemas/email-schema'
import type { EmailInput, EmailValues } from '../schemas/email-schema'

// The form under an invalid confirmation link: an email field that asks for a new link.
const ResendConfirmationForm = () => {
  const { t } = useTranslation('accounts')
  const { t: tAuth } = useTranslation('auth')
  const errorText = useErrorText()
  const mountedRef = useMounted()
  const [resend, { isLoading }] = useResendConfirmationMutation()
  const [outcome, setOutcome] = useState<{ error: unknown } | 'sent' | null>(null)
  const { control, handleSubmit } = useForm<EmailInput, unknown, EmailValues>({
    resolver: zodResolver(emailSchema),
    defaultValues: { email: '' },
  })

  const onValid = async (values: EmailValues) => {
    setOutcome(null)
    const result = await resend({ body: values })
    if (mountedRef.current) {
      setOutcome('error' in result ? { error: result.error } : 'sent')
    }
  }

  return (
    <form
      noValidate
      aria-busy={isLoading}
      onSubmit={(event) => {
        void handleSubmit(onValid)(event)
      }}
    >
      <Stack spacing={2}>
        <Typography>{t('confirmEmail.resendIntro')}</Typography>
        <FormTextField
          control={control}
          name="email"
          label={tAuth('fields.email')}
          type="email"
          autoComplete="email"
        />
        {outcome === 'sent' ? (
          <Alert severity="success" role="status">
            {t('confirmEmail.resent')}
          </Alert>
        ) : null}
        {outcome !== null && outcome !== 'sent' ? (
          <Alert severity="error">{errorText(outcome.error)}</Alert>
        ) : null}
        <Button type="submit" variant="contained" size="large" fullWidth loading={isLoading}>
          {t('confirmEmail.resendSubmit')}
        </Button>
      </Stack>
    </form>
  )
}

export default ResendConfirmationForm
