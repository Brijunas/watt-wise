import Typography from '@mui/material/Typography'
import { useTranslation } from 'react-i18next'

const CatalogReview = () => {
  const { t } = useTranslation('app')

  return (
    <Typography component="h1" variant="h4">
      {t('nav.catalogReview')}
    </Typography>
  )
}

export default CatalogReview
