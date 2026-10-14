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
import { useTheme } from '@mui/material/styles'
import Toolbar from '@mui/material/Toolbar'
import Typography from '@mui/material/Typography'
import useMediaQuery from '@mui/material/useMediaQuery'
import type { ElementType, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import type { AppShellNavItem } from './app-shell-nav-item'

const drawerWidth = 240
const bottomBarHeight = '56px'

const AppShell = ({
  title,
  items,
  activeKey,
  linkComponent,
  actions,
  children,
}: {
  title: string
  items: readonly AppShellNavItem[]
  /** `null` when no item matches, for example on the not-found page. */
  activeKey: string | null
  linkComponent: ElementType<{ to: string }>
  actions?: ReactNode
  children: ReactNode
}) => {
  const { t } = useTranslation('shell')
  const theme = useTheme()
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'))
  const navigationLabel = t('navigation.label')

  return (
    <Box sx={{ display: 'flex', minHeight: '100dvh' }}>
      <AppBar position="fixed" sx={{ zIndex: (muiTheme) => muiTheme.zIndex.drawer + 1 }}>
        <Toolbar>
          <Typography component="div" variant="h6" noWrap>
            {title}
          </Typography>
          <Box sx={{ flexGrow: 1 }} />
          {actions}
        </Toolbar>
      </AppBar>

      {isDesktop ? (
        <Drawer
          variant="permanent"
          sx={{
            width: drawerWidth,
            flexShrink: 0,
            '& .MuiDrawer-paper': { width: drawerWidth, boxSizing: 'border-box' },
          }}
        >
          <Toolbar />
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
        </Drawer>
      ) : (
        <Paper
          elevation={3}
          component="nav"
          aria-label={navigationLabel}
          sx={{
            position: 'fixed',
            insetInline: 0,
            bottom: 0,
            zIndex: (muiTheme) => muiTheme.zIndex.appBar,
            paddingBottom: 'env(safe-area-inset-bottom)',
          }}
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
        </Paper>
      )}

      <Box
        component="main"
        sx={{
          flexGrow: 1,
          minWidth: 0,
          p: { xs: 2, md: 3 },
          pb: isDesktop
            ? 3
            : `calc(${bottomBarHeight} + env(safe-area-inset-bottom) + ${theme.spacing(2)})`,
        }}
      >
        <Toolbar />
        {children}
      </Box>
    </Box>
  )
}

export default AppShell
