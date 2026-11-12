import Container from '@mui/material/Container'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import type { ReactNode } from 'react'

// The frame of every account page: a narrow column with the page's h1.
const AccountPage = ({ title, children }: { title: string; children: ReactNode }) => {
  return (
    <Container maxWidth="sm" sx={{ py: { xs: 3, md: 6 } }}>
      <Stack spacing={3}>
        <Typography component="h1" variant="h4">
          {title}
        </Typography>
        {children}
      </Stack>
    </Container>
  )
}

export default AccountPage
