import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { useSignOutMutation } from '@wattwise/api-client'
import { selectSessionStatus } from '@wattwise/core'
import { AccountMenu as UiAccountMenu } from '@wattwise/ui'
import type { AccountMenuItem } from '@wattwise/ui'
import { paths } from '@/config/routes'
import { useAppSelector } from '@/stores/hooks'

// The app bar's account menu: Settings and sign out for a signed-in user, sign in and sign up for a
// visitor, and no items while the session is restored (the choice isn't known yet; the menu then
// holds only the language below 600px).
const AccountMenu = () => {
  const { t } = useTranslation('app')
  const { t: tAuth } = useTranslation('auth')
  const status = useAppSelector((state) => selectSessionStatus(state))
  const [signOut] = useSignOutMutation()
  const navigate = useNavigate()

  const items: readonly AccountMenuItem[] = (() => {
    if (status === 'restoring') {
      return []
    }
    if (status === 'signedIn') {
      return [
        { key: 'settings', label: t('accountMenu.settings'), to: paths.settings },
        {
          key: 'sign-out',
          label: tAuth('signOut'),
          onSelect: () => {
            // The api-client ends the session even when the request fails, so we always leave.
            void signOut().then(() => navigate(paths.signIn, { replace: true }))
          },
        },
      ]
    }
    return [
      { key: 'sign-in', label: t('accountMenu.signIn'), to: paths.signIn },
      { key: 'sign-up', label: t('accountMenu.signUp'), to: paths.signUp },
    ]
  })()

  return <UiAccountMenu label={t('accountMenu.label')} linkComponent={Link} items={items} />
}

export default AccountMenu
