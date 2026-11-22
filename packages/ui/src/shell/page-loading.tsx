import CircularProgress from '@mui/material/CircularProgress'
import { styled, useThemeProps } from '@mui/material/styles'
import { useTranslation } from 'react-i18next'

export type PageLoadingProps = Record<string, never>

export type PageLoadingClassKey = 'root'

const name = 'WwPageLoading'

const PageLoadingRoot = styled('div', { name, slot: 'root' })<{ ownerState: PageLoadingProps }>(
  ({ theme }) => ({
    display: 'flex',
    justifyContent: 'center',
    paddingTop: theme.spacing(4),
    paddingBottom: theme.spacing(4),
  }),
)

const PageLoading = (inProps: PageLoadingProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { t } = useTranslation('shell')

  return (
    <PageLoadingRoot ownerState={props}>
      <CircularProgress aria-label={t('loading')} />
    </PageLoadingRoot>
  )
}

export default PageLoading
