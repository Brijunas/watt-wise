import { useSearchParams } from 'react-router'
import ResetPasswordForm from '@/features/accounts/components/reset-password-form'

const ResetPassword = () => {
  const [params] = useSearchParams()
  return (
    <ResetPasswordForm
      id={params.get('id') ?? undefined}
      token={params.get('token') ?? undefined}
    />
  )
}

export default ResetPassword
