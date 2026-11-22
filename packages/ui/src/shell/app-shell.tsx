import AppBar from '@mui/material/AppBar'
import Box from '@mui/material/Box'
import BottomNavigation from '@mui/material/BottomNavigation'
import BottomNavigationAction from '@mui/material/BottomNavigationAction'
import Drawer from '@mui/material/Drawer'
import List from '@mui/material/List'
import ListItem from '@mui/material/ListItem'
import ListItemButton from '@mui/material/ListItemButton'
import ListItemIcon from '@mui/material/ListItemIcon'
import ListItemText from '@mui/material/ListItemText'
import Paper from '@mui/material/Paper'
import { styled, useTheme, useThemeProps } from '@mui/material/styles'
import Toolbar from '@mui/material/Toolbar'
import Typography from '@mui/material/Typography'
import useMediaQuery from '@mui/material/useMediaQuery'
import type { ElementType, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import type { AppShellNavItem } from './app-shell-nav-item'
import { bottomBarHeight } from './bottom-bar-height'

const drawerWidth = 240

export type AppShellClassKey = 'root' | 'appBar' | 'drawer' | 'bottomBar' | 'main'

export interface AppShellProps {
  title: string
  items: readonly AppShellNavItem[]
  /** `null` when no item matches, for example on the not-found page. */
  activeKey: string | null
  linkComponent: ElementType<{ to: string }>
  actions?: ReactNode
  children: ReactNode
}

interface OwnerState extends AppShellProps {
  isDesktop: boolean
}

const name = 'WwAppShell'

const AppShellRoot = styled('div', { name, slot: 'root' })<{ ownerState: OwnerState }>({
  display: 'flex',
  minHeight: '100dvh',
})

const AppShellAppBar = styled(AppBar, { name, slot: 'appBar' })<{ ownerState: OwnerState }>(
  ({ theme }) => ({
    zIndex: theme.zIndex.drawer + 1,
  }),
)

const AppShellDrawer = styled(Drawer, { name, slot: 'drawer' })<{ ownerState: OwnerState }>({
  width: drawerWidth,
  flexShrink: 0,
  '& .MuiDrawer-paper': { width: drawerWidth, boxSizing: 'border-box' },
})

const AppShellBottomBar = styled(Paper, { name, slot: 'bottomBar' })<{
  ownerState: OwnerState
  component?: ElementType
}>(({ theme }) => ({
  position: 'fixed',
  insetInline: 0,
  bottom: 0,
  zIndex: theme.zIndex.appBar,
  paddingBottom: 'env(safe-area-inset-bottom)',
}))

const AppShellMain = styled('main', { name, slot: 'main' })<{ ownerState: OwnerState }>(
  ({ theme, ownerState }) => ({
    flexGrow: 1,
    minWidth: 0,
    padding: theme.spacing(2),
    [theme.breakpoints.up('md')]: { padding: theme.spacing(3) },
    paddingBottom: ownerState.isDesktop
      ? theme.spacing(3)
      : `calc(${bottomBarHeight} + env(safe-area-inset-bottom) + ${theme.spacing(2)})`,
  }),
)

const AppShell = (inProps: AppShellProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { title, items, activeKey, linkComponent, actions, children } = props
  const { t } = useTranslation('shell')
  const theme = useTheme()
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'))
  const navigationLabel = t('navigation.label')
  const ownerState: OwnerState = { ...props, isDesktop }

  return (
    <AppShellRoot ownerState={ownerState}>
      <AppShellAppBar position="fixed" ownerState={ownerState}>
        <Toolbar>
          <Typography component="div" variant="h6" noWrap>
            {title}
          </Typography>
          {/* Plain Box with sx: stays until S5A.4 reworks the shell's layout. */}
          <Box sx={{ flexGrow: 1 }} />
          {actions}
        </Toolbar>
      </AppShellAppBar>

      {isDesktop ? (
        <AppShellDrawer variant="permanent" ownerState={ownerState}>
          <Toolbar />
          {/* Plain Box: stays until S5A.4 reworks the shell's layout. */}
          <Box component="nav" aria-label={navigationLabel}>
            <List>
              {items.map((item) => (
                <ListItem key={item.key} disablePadding>
                  <ListItemButton
                    component={linkComponent}
                    to={item.to}
                    selected={item.key === activeKey}
                    aria-current={item.key === activeKey ? 'page' : undefined}
                  >
                    <ListItemIcon>{item.icon}</ListItemIcon>
                    <ListItemText primary={item.label} />
                  </ListItemButton>
                </ListItem>
              ))}
            </List>
          </Box>
        </AppShellDrawer>
      ) : (
        <AppShellBottomBar
          elevation={3}
          component="nav"
          aria-label={navigationLabel}
          ownerState={ownerState}
        >
          <BottomNavigation showLabels value={activeKey}>
            {items.map((item) => (
              <BottomNavigationAction
                key={item.key}
                component={linkComponent}
                to={item.to}
                value={item.key}
                label={item.label}
                icon={item.icon}
                aria-current={item.key === activeKey ? 'page' : undefined}
              />
            ))}
          </BottomNavigation>
        </AppShellBottomBar>
      )}

      <AppShellMain ownerState={ownerState}>
        <Toolbar />
        {children}
      </AppShellMain>
    </AppShellRoot>
  )
}

export default AppShell
