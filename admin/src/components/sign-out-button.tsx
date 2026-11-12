import LogoutIcon from '@mui/icons-material/Logout'
import IconButton from '@mui/material/IconButton'
import { useTranslation } from 'react-i18next'
import { useSignOutMutation } from '@wattwise/api-client'
import { selectSessionStatus } from '@wattwise/core'
import { useAppSelector } from '@/stores/hooks'

// The app bar's sign-out. It doesn't navigate: the signed-out session sends the guard to sign-in.
const SignOutButton = () => {
  const { t } = useTranslation('auth')
  const status = useAppSelector((state) => selectSessionStatus(state))
  const [signOut] = useSignOutMutation()

  if (status !== 'signedIn') {
    return null
  }
  return (
    <IconButton
      color="inherit"
      aria-label={t('signOut')}
      onClick={() => {
        void signOut()
      }}
    >
      <LogoutIcon />
    </IconButton>
  )
}

export default SignOutButton
