import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { useSignOutMutation } from '@wattwise/api-client'
import { selectSessionStatus } from '@wattwise/core'
import { AccountMenu as UiAccountMenu } from '@wattwise/ui'
import type { AccountMenuItem } from '@wattwise/ui'
import { useAppSelector } from '@/stores/hooks'

// The app bar's account menu: sign out for a signed-in user (it doesn't navigate: the signed-out
// session sends the guard to sign-in), and below 600px the language.
const AccountMenu = () => {
  const { t } = useTranslation('app')
  const { t: tAuth } = useTranslation('auth')
  const status = useAppSelector((state) => selectSessionStatus(state))
  const [signOut] = useSignOutMutation()

  const items: readonly AccountMenuItem[] =
    status === 'signedIn'
      ? [
          {
            key: 'sign-out',
            label: tAuth('signOut'),
            onSelect: () => {
              void signOut()
            },
          },
        ]
      : []

  return <UiAccountMenu label={t('accountMenu.label')} linkComponent={Link} items={items} />
}

export default AccountMenu
