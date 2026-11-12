import { zodResolver } from '@hookform/resolvers/zod'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link as RouterLink, useNavigate } from 'react-router'
import { useResetPasswordMutation } from '@wattwise/api-client'
import { errorCode } from '@wattwise/core'
import { AccountPage, FormPasswordField, useErrorText, useMounted } from '@wattwise/ui'
import { paths } from '@/config/routes'
import { resetPasswordSchema } from '../schemas/reset-password-schema'
import type { ResetPasswordInput, ResetPasswordValues } from '../schemas/reset-password-schema'
import applyFieldErrors from '../utils/apply-field-errors'

/** `id` and `token` come from the email link's query; a missing one makes the link invalid. */
const ResetPasswordForm = ({
  id,
  token,
}: {
  id: string | undefined
  token: string | undefined
}) => {
  const { t } = useTranslation('accounts')
  const errorText = useErrorText()
  const navigate = useNavigate()
  const mountedRef = useMounted()
  const [resetPassword, { isLoading }] = useResetPasswordMutation()
  const [failure, setFailure] = useState<{ error: unknown } | null>(null)
  const { control, handleSubmit, setError } = useForm<
    ResetPasswordInput,
    unknown,
    ResetPasswordValues
  >({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: '' },
  })

  const invalid =
    id === undefined ||
    token === undefined ||
    (failure !== null && errorCode(failure.error) === 'Accounts.InvalidEmailLink')

  const onValid = async (values: ResetPasswordValues) => {
    if (id === undefined || token === undefined) {
      return
    }
    setFailure(null)
    const result = await resetPassword({ body: { id, token, newPassword: values.newPassword } })
    if (!mountedRef.current) {
      return
    }
    if ('error' in result) {
      if (!applyFieldErrors(result.error, ['newPassword'], setError)) {
        setFailure({ error: result.error })
      }
      return
    }
    void navigate(paths.signIn, { state: { notice: 'passwordReset' } })
  }

  if (invalid) {
    return (
      <AccountPage title={t('resetPassword.title')}>
        <Alert severity="error">{errorText({ data: { code: 'Accounts.InvalidEmailLink' } })}</Alert>
        <Stack direction="row">
          <Button component={RouterLink} to={paths.forgotPassword} variant="contained">
            {t('resetPassword.requestNewLink')}
          </Button>
        </Stack>
      </AccountPage>
    )
  }

  return (
    <AccountPage title={t('resetPassword.title')}>
      <form
        noValidate
        aria-busy={isLoading}
        onSubmit={(event) => {
          void handleSubmit(onValid)(event)
        }}
      >
        <Stack spacing={2}>
          <FormPasswordField
            control={control}
            name="newPassword"
            label={t('fields.newPassword')}
            autoComplete="new-password"
          />
          {failure === null ? null : <Alert severity="error">{errorText(failure.error)}</Alert>}
          <Button type="submit" variant="contained" size="large" fullWidth loading={isLoading}>
            {t('resetPassword.submit')}
          </Button>
        </Stack>
      </form>
    </AccountPage>
  )
}

export default ResetPasswordForm
