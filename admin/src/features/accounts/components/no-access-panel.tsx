import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import { useTranslation } from 'react-i18next'
import { Link as RouterLink } from 'react-router'
import { AccountPage } from '@wattwise/ui'
import { paths } from '@/config/routes'

// Shown to an account without admin access. The sign-in link waits for the sign-out to finish
// (`signedOut`), so the link can't race the request still in flight.
const NoAccessPanel = ({ signedOut }: { signedOut: boolean }) => {
  const { t } = useTranslation('accounts')

  return (
    <AccountPage title={t('noAccess.title')}>
      <Typography>{t('noAccess.text')}</Typography>
      {signedOut ? (
        <Button component={RouterLink} to={paths.signIn} variant="contained">
          {t('noAccess.signIn')}
        </Button>
      ) : null}
    </AccountPage>
  )
}

export default NoAccessPanel
