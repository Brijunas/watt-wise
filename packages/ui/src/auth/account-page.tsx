import Container from '@mui/material/Container'
import Stack from '@mui/material/Stack'
import { styled, useThemeProps } from '@mui/material/styles'
import Typography from '@mui/material/Typography'
import type { ElementType, ReactNode } from 'react'

export interface AccountPageProps {
  title: string
  children: ReactNode
}

export type AccountPageClassKey = 'root' | 'title'

const name = 'WwAccountPage'

const AccountPageRoot = styled(Container, { name, slot: 'root' })<{
  ownerState: AccountPageProps
}>(({ theme }) => ({
  paddingTop: theme.spacing(3),
  paddingBottom: theme.spacing(3),
  [theme.breakpoints.up('md')]: {
    paddingTop: theme.spacing(6),
    paddingBottom: theme.spacing(6),
  },
}))

const AccountPageTitle = styled(Typography, { name, slot: 'title' })<{
  ownerState: AccountPageProps
  component?: ElementType
}>({})

// The frame of every account page: a narrow column with the page's h1.
const AccountPage = (inProps: AccountPageProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { title, children } = props

  return (
    <AccountPageRoot maxWidth="sm" ownerState={props}>
      <Stack spacing={3}>
        <AccountPageTitle component="h1" variant="h4" ownerState={props}>
          {title}
        </AccountPageTitle>
        {children}
      </Stack>
    </AccountPageRoot>
  )
}

export default AccountPage
