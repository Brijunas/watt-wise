import { useLocation } from 'react-router'
import CheckEmailPanel from '@/features/accounts/components/check-email-panel'

// Sign-up leaves the address in the router state; a reload loses it and the text turns generic.
const CheckEmail = () => {
  const { state } = useLocation() as { state: { email?: unknown } | null }
  const email = typeof state?.email === 'string' && state.email !== '' ? state.email : undefined
  return <CheckEmailPanel email={email} />
}

export default CheckEmail
