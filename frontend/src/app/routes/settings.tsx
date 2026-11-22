import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useTranslation } from 'react-i18next'
import SettingsLinks from '@/features/settings/components/settings-links'

const Settings = () => {
  const { t } = useTranslation('settings')
  return (
    <Stack spacing={2}>
      <Typography component="h1" variant="h1">
        {t('title')}
      </Typography>
      <SettingsLinks />
    </Stack>
  )
}

export default Settings
