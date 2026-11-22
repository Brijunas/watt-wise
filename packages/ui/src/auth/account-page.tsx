import Card from '@mui/material/Card'
import CardContent from '@mui/material/CardContent'
import Container from '@mui/material/Container'
import Stack from '@mui/material/Stack'
import { styled, useThemeProps } from '@mui/material/styles'
import Typography from '@mui/material/Typography'
import type { ElementType, ReactNode } from 'react'

export interface AccountPageProps {
  title: string
  children: ReactNode
}

export type AccountPageClassKey = 'root' | 'title' | 'card'

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

const AccountPageCard = styled(Card, { name, slot: 'card' })<{
  ownerState: AccountPageProps
}>({})

// The frame of every account page: a narrow column holding a card with the page's h1.
const AccountPage = (inProps: AccountPageProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { title, children } = props

  return (
    <AccountPageRoot maxWidth="sm" ownerState={props}>
      <AccountPageCard ownerState={props}>
        <CardContent>
          <Stack spacing={3}>
            <AccountPageTitle component="h1" variant="h1" ownerState={props}>
              {title}
            </AccountPageTitle>
            {children}
          </Stack>
        </CardContent>
      </AccountPageCard>
    </AccountPageRoot>
  )
}

export default AccountPage
