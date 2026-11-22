import FactCheckIcon from '@mui/icons-material/FactCheckRounded'
import HomeIcon from '@mui/icons-material/HomeRounded'
import InfoIcon from '@mui/icons-material/InfoRounded'
import { useTranslation } from 'react-i18next'
import { Link, matchPath, Outlet, useLocation } from 'react-router'
import { AppShell } from '@wattwise/ui'
import type { AppShellNavItem } from '@wattwise/ui'
import AccountMenu from '@/components/account-menu'
import { paths } from '@/config/routes'

const RootLayout = () => {
  const { t } = useTranslation('app')
  const { pathname } = useLocation()

  const items: readonly AppShellNavItem[] = [
    { key: 'home', label: t('nav.home'), icon: <HomeIcon />, to: paths.home },
    {
      key: 'catalogReview',
      label: t('nav.catalogReview'),
      icon: <FactCheckIcon />,
      to: paths.catalogReview,
    },
    { key: 'about', label: t('nav.about'), icon: <InfoIcon />, to: paths.about },
  ]
  const active = items.find((item) =>
    matchPath({ path: item.to, end: item.to === paths.home }, pathname),
  )

  return (
    <AppShell
      items={items}
      activeKey={active?.key ?? null}
      linkComponent={Link}
      logoChip={t('shell.adminChip')}
      accountMenu={<AccountMenu />}
    >
      <Outlet />
    </AppShell>
  )
}

export default RootLayout
