import Typography from '@mui/material/Typography'
import { useTranslation } from 'react-i18next'

export function Placeholder() {
  const { t } = useTranslation('shell')
  return (
    <Typography component="h1" variant="h4">
      {t('nav.placeholder')}
    </Typography>
  )
}
