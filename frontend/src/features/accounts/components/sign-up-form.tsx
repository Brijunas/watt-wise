import { zodResolver } from '@hookform/resolvers/zod'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Link from '@mui/material/Link'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link as RouterLink, useNavigate } from 'react-router'
import { useRegisterMutation } from '@wattwise/api-client'
import {
  AccountPage,
  FormPasswordField,
  FormTextField,
  useErrorText,
  useMounted,
} from '@wattwise/ui'
import { paths } from '@/config/routes'
import { signUpSchema } from '../schemas/sign-up-schema'
import type { SignUpInput, SignUpValues } from '../schemas/sign-up-schema'
import applyFieldErrors from '../utils/apply-field-errors'

const SignUpForm = () => {
  const { t, i18n } = useTranslation('accounts')
  const { t: tAuth } = useTranslation('auth')
  const errorText = useErrorText()
  const navigate = useNavigate()
  const mountedRef = useMounted()
  const [register, { isLoading }] = useRegisterMutation()
  const [failure, setFailure] = useState<{ error: unknown } | null>(null)
  const { control, handleSubmit, setError } = useForm<SignUpInput, unknown, SignUpValues>({
    resolver: zodResolver(signUpSchema),
    defaultValues: { email: '', password: '' },
  })

  const onValid = async (values: SignUpValues) => {
    setFailure(null)
    // The account's language for its emails is the one the page is shown in.
    const language = i18n.resolvedLanguage === 'lt' ? 'lt' : 'en'
    const result = await register({ body: { ...values, language } })
    if (!mountedRef.current) {
      return
    }
    if ('error' in result) {
      if (!applyFieldErrors(result.error, ['email', 'password'], setError)) {
        setFailure({ error: result.error })
      }
      return
    }
    void navigate(paths.checkEmail, { state: { email: values.email } })
  }

  return (
    <AccountPage title={t('signUp.title')}>
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
          <FormPasswordField
            control={control}
            name="password"
            label={tAuth('fields.password')}
            autoComplete="new-password"
          />
          {failure === null ? null : <Alert severity="error">{errorText(failure.error)}</Alert>}
          <Button type="submit" variant="contained" size="large" fullWidth loading={isLoading}>
            {t('signUp.submit')}
          </Button>
        </Stack>
      </form>
      <Typography>
        {t('signUp.haveAccount')}{' '}
        <Link component={RouterLink} to={paths.signIn}>
          {t('signUp.signIn')}
        </Link>
      </Typography>
    </AccountPage>
  )
}

export default SignUpForm
