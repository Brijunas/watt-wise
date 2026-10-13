import Typography from '@mui/material/Typography'
import { useTranslation } from 'react-i18next'

export function CatalogReview() {
  const { t } = useTranslation('shell')

  return (
    <Typography component="h1" variant="h4">
      {t('nav.catalogReview')}
    </Typography>
  )
}
