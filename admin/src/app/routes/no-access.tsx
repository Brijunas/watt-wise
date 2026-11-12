import { selectSessionStatus } from '@wattwise/core'
import NoAccessPanel from '@/features/accounts/components/no-access-panel'
import { useAppSelector } from '@/stores/hooks'

const NoAccess = () => {
  const status = useAppSelector((state) => selectSessionStatus(state))
  return <NoAccessPanel signedOut={status === 'signedOut'} />
}

export default NoAccess
