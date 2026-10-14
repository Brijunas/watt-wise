import Button from '@mui/material/Button'
import Snackbar from '@mui/material/Snackbar'
import { useTranslation } from 'react-i18next'
import { bottomBarHeight } from './bottom-bar-height'

const UpdatePrompt = ({
  open,
  onReload,
  onClose,
}: {
  open: boolean
  onReload: () => void
  onClose: () => void
}) => {
  const { t } = useTranslation('shell')

  return (
    <Snackbar
      open={open}
      message={t('update.message')}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      sx={(theme) => ({
        bottom: `calc(${bottomBarHeight} + env(safe-area-inset-bottom) + ${theme.spacing(1)})`,
        [theme.breakpoints.up('md')]: { bottom: theme.spacing(3) },
      })}
      action={
        <>
          <Button color="inherit" size="small" onClick={onClose}>
            {t('update.later')}
          </Button>
          <Button color="inherit" size="small" onClick={onReload} sx={{ fontWeight: 700 }}>
            {t('update.reload')}
          </Button>
        </>
      }
    />
  )
}

export default UpdatePrompt
