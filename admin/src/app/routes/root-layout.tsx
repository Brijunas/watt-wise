import FactCheckIcon from '@mui/icons-material/FactCheck'
import HomeIcon from '@mui/icons-material/Home'
import { useTranslation } from 'react-i18next'
import { Link, matchPath, Outlet, useLocation } from 'react-router'
import { AppShell, ModeSwitch } from '@wattwise/ui'
import type { AppShellNavItem } from '@wattwise/ui'
import { paths } from '@/config/routes'

export function RootLayout() {
  const { t } = useTranslation('shell')
  const { pathname } = useLocation()

  const items: readonly AppShellNavItem[] = [
    { key: 'home', label: t('nav.home'), icon: <HomeIcon />, to: paths.home },
    {
      key: 'catalogReview',
      label: t('nav.catalogReview'),
      icon: <FactCheckIcon />,
      to: paths.catalogReview,
    },
  ]
  const active = items.find((item) =>
    matchPath({ path: item.to, end: item.to === paths.home }, pathname),
  )

  return (
    <AppShell
      title="Watt-Wise Admin"
      items={items}
      activeKey={active?.key ?? null}
      linkComponent={Link}
      actions={<ModeSwitch />}
    >
      <Outlet />
    </AppShell>
  )
}
