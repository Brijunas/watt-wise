import Typography from '@mui/material/Typography'
import { useTranslation } from 'react-i18next'

const Home = () => {
  const { t } = useTranslation('app')

  return (
    <Typography component="h1" variant="h1">
      {t('nav.home')}
    </Typography>
  )
}

export default Home
