import Alert from '@mui/material/Alert'
import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useTranslation } from 'react-i18next'
import { useGetAboutQuery } from '@wattwise/api-client'
import { PageLoading } from '@wattwise/ui'

const AboutPanel = () => {
  const { t } = useTranslation('about')
  const { data, isLoading, isError } = useGetAboutQuery()

  return (
    <Stack spacing={2}>
      <Typography component="h1" variant="h1">
        {t('title')}
      </Typography>
      {isLoading && <PageLoading />}
      {isError && <Alert severity="error">{t('error')}</Alert>}
      {data && (
        <Card>
          <CardContent>
            <Typography>
              {t('apiName')}: {data.name}
            </Typography>
            <Typography>
              {t('version')}: {data.version}
            </Typography>
          </CardContent>
        </Card>
      )}
    </Stack>
  )
}

export default AboutPanel
