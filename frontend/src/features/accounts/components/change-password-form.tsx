import { zodResolver } from '@hookform/resolvers/zod'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useChangePasswordMutation } from '@wattwise/api-client'
import { encodeValidationMessage, errorCode } from '@wattwise/core'
import { AccountPage, FormPasswordField, useErrorText, useMounted } from '@wattwise/ui'
import { changePasswordSchema } from '../schemas/change-password-schema'
import type { ChangePasswordInput, ChangePasswordValues } from '../schemas/change-password-schema'
import applyFieldErrors from '../utils/apply-field-errors'

const ChangePasswordForm = () => {
  const { t } = useTranslation('accounts')
  const errorText = useErrorText()
  const mountedRef = useMounted()
  const [changePassword, { isLoading }] = useChangePasswordMutation()
  const [outcome, setOutcome] = useState<{ error: unknown } | 'changed' | null>(null)
  const { control, handleSubmit, setError, reset } = useForm<
    ChangePasswordInput,
    unknown,
    ChangePasswordValues
  >({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', newPassword: '' },
  })

  const onValid = async (values: ChangePasswordValues) => {
    setOutcome(null)
    const result = await changePassword({ body: values })
    // The page is gone when the user signed out meanwhile: no notice for a change they left.
    if (!mountedRef.current) {
      return
    }
    if (!('error' in result)) {
      reset()
      setOutcome('changed')
      return
    }
    if (errorCode(result.error) === 'Accounts.InvalidCurrentPassword') {
      // The field is marked and the message under the form says what is wrong with it.
      setError('currentPassword', { message: encodeValidationMessage('invalid') })
      setOutcome({ error: result.error })
      return
    }
    if (!applyFieldErrors(result.error, ['currentPassword', 'newPassword'], setError)) {
      setOutcome({ error: result.error })
    }
  }

  return (
    <AccountPage title={t('changePassword.title')}>
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
            name="currentPassword"
            label={t('fields.currentPassword')}
            autoComplete="current-password"
          />
          <FormPasswordField
            control={control}
            name="newPassword"
            label={t('fields.newPassword')}
            autoComplete="new-password"
          />
          {outcome === 'changed' ? (
            <Alert severity="success" role="status">
              {t('changePassword.success')}
            </Alert>
          ) : null}
          {outcome !== null && outcome !== 'changed' ? (
            <Alert severity="error">{errorText(outcome.error)}</Alert>
          ) : null}
          <Button type="submit" variant="contained" size="large" fullWidth loading={isLoading}>
            {t('changePassword.submit')}
          </Button>
        </Stack>
      </form>
    </AccountPage>
  )
}

export default ChangePasswordForm
