import Button from '@mui/material/Button'
import Snackbar from '@mui/material/Snackbar'
import { styled, useThemeProps } from '@mui/material/styles'
import { useTranslation } from 'react-i18next'
import { bottomBarHeight } from './bottom-bar-height'

export interface UpdatePromptProps {
  open: boolean
  onReload: () => void
  onClose: () => void
}

export type UpdatePromptClassKey = 'root' | 'reloadButton'

const name = 'WwUpdatePrompt'

const UpdatePromptRoot = styled(Snackbar, { name, slot: 'root' })<{
  ownerState: UpdatePromptProps
}>(({ theme }) => ({
  bottom: `calc(${bottomBarHeight} + env(safe-area-inset-bottom) + ${theme.spacing(1)})`,
  [theme.breakpoints.up('md')]: { bottom: theme.spacing(3) },
}))

const UpdatePromptReloadButton = styled(Button, { name, slot: 'reloadButton' })<{
  ownerState: UpdatePromptProps
}>(({ theme }) => ({ fontWeight: theme.typography.fontWeightBold }))

const UpdatePrompt = (inProps: UpdatePromptProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { open, onReload, onClose } = props
  const { t } = useTranslation('shell')

  return (
    <UpdatePromptRoot
      ownerState={props}
      open={open}
      message={t('update.message')}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      action={
        <>
          <Button color="inherit" size="small" onClick={onClose}>
            {t('update.later')}
          </Button>
          <UpdatePromptReloadButton
            ownerState={props}
            color="inherit"
            size="small"
            onClick={onReload}
          >
            {t('update.reload')}
          </UpdatePromptReloadButton>
        </>
      }
    />
  )
}

export default UpdatePrompt
