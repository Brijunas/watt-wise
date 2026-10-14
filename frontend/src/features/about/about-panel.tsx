import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import CircularProgress from '@mui/material/CircularProgress'
import Typography from '@mui/material/Typography'
import { useTranslation } from 'react-i18next'
import { useGetAboutQuery } from '@wattwise/api-client'

const AboutPanel = () => {
  const { t } = useTranslation('about')
  const { data, isLoading, isError } = useGetAboutQuery()

  return (
    <Box>
      <Typography component="h1" variant="h4" gutterBottom>
        {t('title')}
      </Typography>
      {isLoading && <CircularProgress aria-label={t('loading')} />}
      {isError && <Alert severity="error">{t('error')}</Alert>}
      {data && (
        <>
          <Typography>
            {t('apiName')}: {data.name}
          </Typography>
          <Typography>
            {t('version')}: {data.version}
          </Typography>
        </>
      )}
    </Box>
  )
}

export default AboutPanel
