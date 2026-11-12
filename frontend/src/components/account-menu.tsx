import AccountCircleIcon from '@mui/icons-material/AccountCircle'
import IconButton from '@mui/material/IconButton'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import { useId, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { useSignOutMutation } from '@wattwise/api-client'
import { selectSessionStatus } from '@wattwise/core'
import { paths } from '@/config/routes'
import { useAppSelector } from '@/stores/hooks'

// The app bar's account menu: the account pages for a signed-in user, sign in and sign up for a
// visitor, nothing while the session is restored (the choice isn't known yet).
const AccountMenu = () => {
  const { t } = useTranslation('app')
  const { t: tAuth } = useTranslation('auth')
  const status = useAppSelector((state) => selectSessionStatus(state))
  const [signOut] = useSignOutMutation()
  const navigate = useNavigate()
  const menuId = useId()
  const [anchor, setAnchor] = useState<HTMLElement | null>(null)

  if (status === 'restoring') {
    return null
  }

  const open = anchor !== null
  const close = () => {
    setAnchor(null)
  }
  const handleSignOut = () => {
    close()
    // The api-client ends the session even when the request fails, so we always leave.
    void signOut()
      .unwrap()
      .catch(() => undefined)
      .then(() => navigate(paths.signIn, { replace: true }))
  }

  return (
    <>
      <IconButton
        color="inherit"
        aria-label={t('accountMenu.label')}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={(event) => {
          setAnchor(event.currentTarget)
        }}
      >
        <AccountCircleIcon />
      </IconButton>
      <Menu id={menuId} anchorEl={anchor} open={open} onClose={close}>
        {status === 'signedIn'
          ? [
              <MenuItem key="devices" component={Link} to={paths.devices} onClick={close}>
                {t('accountMenu.devices')}
              </MenuItem>,
              <MenuItem
                key="change-password"
                component={Link}
                to={paths.changePassword}
                onClick={close}
              >
                {t('accountMenu.changePassword')}
              </MenuItem>,
              <MenuItem key="sign-out" onClick={handleSignOut}>
                {tAuth('signOut')}
              </MenuItem>,
            ]
          : [
              <MenuItem key="sign-in" component={Link} to={paths.signIn} onClick={close}>
                {t('accountMenu.signIn')}
              </MenuItem>,
              <MenuItem key="sign-up" component={Link} to={paths.signUp} onClick={close}>
                {t('accountMenu.signUp')}
              </MenuItem>,
            ]}
      </Menu>
    </>
  )
}

export default AccountMenu
