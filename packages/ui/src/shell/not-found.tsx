import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import type { ElementType } from 'react'
import { useTranslation } from 'react-i18next'

const NotFound = ({
  linkComponent,
  homeTo,
}: {
  linkComponent: ElementType<{ to: string }>
  homeTo: string
}) => {
  const { t } = useTranslation('shell')

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-start', gap: 2 }}>
      <Typography component="h1" variant="h4">
        {t('notFound.title')}
      </Typography>
      <Typography>{t('notFound.message')}</Typography>
      <Button component={linkComponent} to={homeTo} variant="contained">
        {t('notFound.home')}
      </Button>
    </Box>
  )
}

export default NotFound
