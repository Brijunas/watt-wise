import { Link, Outlet } from 'react-router'
import { AppShell } from '@wattwise/ui'

// The shell of the signed-out pages: the app bar only, no navigation and no account menu.
const SignedOutLayout = () => (
  <AppShell variant="minimal" linkComponent={Link}>
    <Outlet />
  </AppShell>
)

export default SignedOutLayout
