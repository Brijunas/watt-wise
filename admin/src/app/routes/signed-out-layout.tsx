import { useTranslation } from 'react-i18next'
import { Link, Outlet } from 'react-router'
import { AppShell } from '@wattwise/ui'

// The shell of the signed-out pages (sign-in, no access): the app bar only, no navigation.
const SignedOutLayout = () => {
  const { t } = useTranslation('app')

  return (
    <AppShell variant="minimal" linkComponent={Link} logoChip={t('shell.adminChip')}>
      <Outlet />
    </AppShell>
  )
}

export default SignedOutLayout
