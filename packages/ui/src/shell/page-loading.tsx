import Box from '@mui/material/Box'
import CircularProgress from '@mui/material/CircularProgress'
import { useTranslation } from 'react-i18next'

const PageLoading = () => {
  const { t } = useTranslation('shell')

  return (
    <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
      <CircularProgress aria-label={t('loading')} />
    </Box>
  )
}

export default PageLoading
