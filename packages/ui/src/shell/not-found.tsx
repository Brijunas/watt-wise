import Button from '@mui/material/Button'
import { styled, useThemeProps } from '@mui/material/styles'
import Typography from '@mui/material/Typography'
import type { ElementType } from 'react'
import { useTranslation } from 'react-i18next'

export interface NotFoundProps {
  linkComponent: ElementType<{ to: string }>
  homeTo: string
}

export type NotFoundClassKey = 'root'

const name = 'WwNotFound'

const NotFoundRoot = styled('div', { name, slot: 'root' })<{ ownerState: NotFoundProps }>(
  ({ theme }) => ({
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'flex-start',
    gap: theme.spacing(2),
  }),
)

const NotFound = (inProps: NotFoundProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { linkComponent, homeTo } = props
  const { t } = useTranslation('shell')

  return (
    <NotFoundRoot ownerState={props}>
      <Typography component="h1" variant="h1">
        {t('notFound.title')}
      </Typography>
      <Typography>{t('notFound.message')}</Typography>
      <Button component={linkComponent} to={homeTo} variant="contained">
        {t('notFound.home')}
      </Button>
    </NotFoundRoot>
  )
}

export default NotFound
