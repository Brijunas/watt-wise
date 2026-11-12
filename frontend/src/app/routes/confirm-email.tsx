import { useSearchParams } from 'react-router'
import ConfirmEmailPanel from '@/features/accounts/components/confirm-email-panel'

const ConfirmEmail = () => {
  const [params] = useSearchParams()
  return (
    <ConfirmEmailPanel
      id={params.get('id') ?? undefined}
      token={params.get('token') ?? undefined}
    />
  )
}

export default ConfirmEmail
