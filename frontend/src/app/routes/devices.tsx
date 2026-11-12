import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useTranslation } from 'react-i18next'
import DeviceList from '@/features/devices/components/device-list'

const Devices = () => {
  const { t } = useTranslation('devices')
  return (
    <Stack spacing={2}>
      <Typography component="h1" variant="h4">
        {t('title')}
      </Typography>
      <DeviceList />
    </Stack>
  )
}

export default Devices
