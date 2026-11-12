import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import type { SignInInput } from '@wattwise/core'
import type { SubmitEventHandler, ReactNode } from 'react'
import type { Control } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import FormPasswordField from '../form/form-password-field'
import FormTextField from '../form/form-text-field'

export interface SignInFieldsProps {
  control: Control<SignInInput>
  onSubmit: SubmitEventHandler<HTMLFormElement>
  /** True while the request is in flight: the button is disabled and shows progress. */
  busy?: boolean
  /** A message or action shown above the button, such as the server's error. */
  error?: ReactNode
  /** The link to the forgot-password page, shown under the fields. */
  forgotPasswordLink?: ReactNode
}

// The sign-in form's fields and button, shared by both apps. It holds no data fetching and no
// routing: the page binds `control` and `onSubmit` and passes the links and errors in.
const SignInFields = ({
  control,
  onSubmit,
  busy = false,
  error,
  forgotPasswordLink,
}: SignInFieldsProps) => {
  const { t } = useTranslation('auth')

  return (
    <form noValidate onSubmit={onSubmit} aria-busy={busy}>
      <Stack spacing={2}>
        <FormTextField
          control={control}
          name="email"
          label={t('fields.email')}
          type="email"
          autoComplete="email"
        />
        <FormPasswordField
          control={control}
          name="password"
          label={t('fields.password')}
          autoComplete="current-password"
        />
        {forgotPasswordLink === undefined ? null : <Box>{forgotPasswordLink}</Box>}
        {error}
        <Button type="submit" variant="contained" size="large" fullWidth loading={busy}>
          {t('signIn.submit')}
        </Button>
      </Stack>
    </form>
  )
}

export default SignInFields
