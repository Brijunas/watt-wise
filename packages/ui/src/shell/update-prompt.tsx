import Button from '@mui/material/Button'
import Snackbar from '@mui/material/Snackbar'
import { styled, useThemeProps } from '@mui/material/styles'
import { useTranslation } from 'react-i18next'
import { shellInset } from './shell-insets'

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
  // The prompt renders outside the shell, which publishes the insets on <html>; without a shell the
  // fallbacks are 0px. Under 600px it sits above the bottom bar.
  bottom: `calc(${shellInset('bottom', '0px')} + ${theme.spacing(1)})`,
  // From 600px it is centred in the space beside the rail or drawer, not the whole viewport.
  [theme.breakpoints.up('sm')]: {
    bottom: theme.spacing(3),
    left: `calc(50% + ${shellInset('start', '0px')} / 2)`,
    maxWidth: `calc(100vw - ${shellInset('start', '0px')} - ${theme.spacing(6)})`,
  },
}))

const UpdatePromptReloadButton = styled(Button, { name, slot: 'reloadButton' })<{
  ownerState: UpdatePromptProps
}>({})

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
          <Button variant="outlined" onClick={onClose}>
            {t('update.later')}
          </Button>
          <UpdatePromptReloadButton ownerState={props} variant="contained" onClick={onReload}>
            {t('update.reload')}
          </UpdatePromptReloadButton>
        </>
      }
    />
  )
}

export default UpdatePrompt
