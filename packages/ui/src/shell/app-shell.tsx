import AppBar from '@mui/material/AppBar'
import BottomNavigation from '@mui/material/BottomNavigation'
import BottomNavigationAction from '@mui/material/BottomNavigationAction'
import Chip from '@mui/material/Chip'
import Drawer from '@mui/material/Drawer'
import GlobalStyles from '@mui/material/GlobalStyles'
import { iconButtonClasses } from '@mui/material/IconButton'
import List from '@mui/material/List'
import ListItem from '@mui/material/ListItem'
import ListItemButton, { listItemButtonClasses } from '@mui/material/ListItemButton'
import ListItemIcon, { listItemIconClasses } from '@mui/material/ListItemIcon'
import ListItemText, { listItemTextClasses } from '@mui/material/ListItemText'
import Paper from '@mui/material/Paper'
import { svgIconClasses } from '@mui/material/SvgIcon'
import { styled, useTheme, useThemeProps } from '@mui/material/styles'
import type { Theme } from '@mui/material/styles'
import Toolbar from '@mui/material/Toolbar'
import useMediaQuery from '@mui/material/useMediaQuery'
import { useEffect, useRef, useState } from 'react'
import type { ElementType, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import LanguageSwitch from '../i18n/language-switch'
import GlowSwitch, { glowSwitchClasses } from '../theme/glow-switch'
import ModeSwitch from '../theme/mode-switch'
import type { AppShellNavItem } from './app-shell-nav-item'
import Logo from './logo'
import { bottomInsetFallback, shellInset, shellInsetNames } from './shell-insets'

export type AppShellClassKey =
  | 'root'
  | 'appBar'
  | 'toolbar'
  | 'logoChip'
  | 'controls'
  | 'drawer'
  | 'rail'
  | 'nav'
  | 'bottomBar'
  | 'main'
  | 'content'
  | 'primary'
  | 'aside'

interface AppShellCommonProps {
  linkComponent: ElementType<{ to: string }>
  /** A small chip after the logo, already translated (the admin's "Admin"). */
  logoChip?: string
  /** The app's account menu (`AccountMenu`), the last control in the app bar. */
  accountMenu?: ReactNode
  /** A side column: beside the content from `xl`, after it below. */
  aside?: ReactNode
  children: ReactNode
}

interface AppShellFullProps extends AppShellCommonProps {
  variant?: 'full'
  items: readonly AppShellNavItem[]
  /** `null` when no item matches, for example on the not-found page. */
  activeKey: string | null
}

// Signed-out pages: the app bar only, no navigation, and the language at every width.
interface AppShellMinimalProps extends AppShellCommonProps {
  variant: 'minimal'
  items?: never
  activeKey?: never
}

export type AppShellProps = AppShellFullProps | AppShellMinimalProps

type Navigation = 'bottomBar' | 'rail' | 'drawer' | 'none'

type OwnerState = AppShellProps & { navigation: Navigation }

const name = 'WwAppShell'

// Below this width the app bar's controls shrink and its gutters tighten (layout.md).
const narrow = 400

// The app bar's icons are 24px whatever the text size: MUI sizes them in rem, which at 200% text
// would grow them over the 44px and 40px buttons.
const appBarIconSize = 24

// layout.md "App shell": the content's cap, the side column, the gutters.
const contentMaxWidth = { base: 1120, xl: 1440, xxl: 1600 } as const
const asideWidth = { xl: 340, xxl: 380 } as const

const AppShellRoot = styled('div', { name, slot: 'root' })<{ ownerState: OwnerState }>({
  display: 'flex',
  minHeight: '100dvh',
})

const AppShellAppBar = styled(AppBar, { name, slot: 'appBar' })<{ ownerState: OwnerState }>(
  ({ theme }) => ({
    zIndex: theme.zIndex.drawer + 1,
  }),
)

// 56px at xs (also in landscape) and 64px from `sm`; the `--ww-shell-inset-top` variable is these
// plus the app bar's 1px divider. Rendered with the `dense` variant because the default one applies
// MUI's mixin, which drops a landscape phone to 48px in a media rule no plain declaration can beat;
// `dense` only sets a plain `min-height`, which these replace.
const AppShellToolbar = styled(Toolbar, { name, slot: 'toolbar' })<{ ownerState: OwnerState }>(
  ({ theme }) => ({
    minHeight: theme.spacing(7),
    [theme.breakpoints.up('sm')]: { minHeight: theme.spacing(8) },
    [theme.breakpoints.down(narrow)]: { paddingInline: theme.spacing(1) },
  }),
)

// The chip gives way to the controls at any width or text size: it shrinks (its label ends in an
// ellipsis) down to nothing, so the controls never leave the screen. `overflow: hidden` clips the
// label's padding once the chip is narrower than that.
const AppShellLogoChip = styled(Chip, { name, slot: 'logoChip' })<{ ownerState: OwnerState }>(
  ({ theme }) => ({
    flexShrink: 1,
    minWidth: 0,
    overflow: 'hidden',
    // MUI's small chip is a fixed 24px; the height follows the text so zoomed text isn't clipped.
    height: 'auto',
    minHeight: theme.spacing(3),
    marginInlineEnd: theme.spacing(0.5),
  }),
)

// The app bar's controls: 44px targets, 40px below 400px.
const AppShellControls = styled('div', { name, slot: 'controls' })<{ ownerState: OwnerState }>(
  ({ theme }) => ({
    marginInlineStart: 'auto',
    flexShrink: 0,
    display: 'flex',
    alignItems: 'center',
    [`& .${svgIconClasses.root}`]: { fontSize: appBarIconSize },
    [`& .${iconButtonClasses.root}`]: {
      // A minimum, not a fixed width: the language button's code grows with the text size and
      // widens its button (a pill) instead of overflowing it.
      minWidth: theme.spacing(5.5),
      height: theme.spacing(5.5),
      borderRadius: theme.vars.shape.radius.chip,
      [theme.breakpoints.down(narrow)]: { minWidth: theme.spacing(5), height: theme.spacing(5) },
    },
    // The glow switch's label.
    [`& .${glowSwitchClasses.root}`]: {
      minHeight: theme.spacing(5.5),
      [theme.breakpoints.down(narrow)]: { minHeight: theme.spacing(5) },
    },
  }),
)

// The rail and the drawer are permanent drawers whose paper starts under the app bar.
const sideNavigation = (theme: Theme, width: string) => ({
  width,
  flexShrink: 0,
  '& .MuiDrawer-paper': {
    width,
    boxSizing: 'border-box' as const,
    paddingTop: shellInset('top'),
    paddingInline: theme.spacing(1),
  },
})

const AppShellDrawer = styled(Drawer, { name, slot: 'drawer' })<{ ownerState: OwnerState }>(
  ({ theme }) => sideNavigation(theme, theme.spacing(32)),
)

// Icon over label, 88px wide.
const AppShellRail = styled(Drawer, { name, slot: 'rail' })<{ ownerState: OwnerState }>(
  ({ theme }) => ({
    ...sideNavigation(theme, theme.spacing(11)),
    [`& .${listItemButtonClasses.root}`]: {
      flexDirection: 'column',
      gap: theme.spacing(0.5),
      paddingBlock: theme.spacing(1.5),
      paddingInline: theme.spacing(0.5),
      textAlign: 'center',
    },
    [`& .${listItemIconClasses.root}`]: { minWidth: 0 },
    [`& .${listItemTextClasses.root}`]: { flex: 'none', margin: 0 },
    [`& .${listItemTextClasses.primary}`]: { overflowWrap: 'anywhere' },
  }),
)

const AppShellBottomBar = styled(Paper, { name, slot: 'bottomBar' })<{
  ownerState: OwnerState
  component?: ElementType
}>(({ theme }) => ({
  position: 'fixed',
  insetInline: 0,
  bottom: 0,
  zIndex: theme.zIndex.appBar,
  paddingBottom: 'env(safe-area-inset-bottom)',
  // The same surface as the bar inside it: without this the dark-mode elevation overlay tints
  // the safe-area strip under the bar.
  backgroundColor: theme.vars.palette.background.paper,
  backgroundImage: 'none',
}))

// The fixed bars are not in the flow: `main` clears them with the inset variables, not with
// spacer toolbars.
const AppShellMain = styled('main', { name, slot: 'main' })<{ ownerState: OwnerState }>(
  ({ theme }) => ({
    flexGrow: 1,
    minWidth: 0,
    paddingInline: theme.spacing(2),
    paddingTop: `calc(${shellInset('top')} + ${theme.spacing(2)})`,
    paddingBottom: `calc(${shellInset('bottom')} + ${theme.spacing(2)})`,
    [theme.breakpoints.up('sm')]: {
      paddingInline: theme.spacing(3),
      paddingTop: `calc(${shellInset('top')} + ${theme.spacing(3)})`,
      paddingBottom: `calc(${shellInset('bottom')} + ${theme.spacing(3)})`,
    },
    [theme.breakpoints.up('lg')]: { paddingInline: theme.spacing(5) },
  }),
)

const AppShellContent = styled('div', { name, slot: 'content' })<{ ownerState: OwnerState }>(
  ({ theme, ownerState }) => ({
    width: '100%',
    minWidth: 0,
    marginInline: 'auto',
    maxWidth: contentMaxWidth.base,
    [theme.breakpoints.up('xl')]: { maxWidth: contentMaxWidth.xl },
    [theme.breakpoints.up('xxl')]: { maxWidth: contentMaxWidth.xxl },
    ...(ownerState.aside === undefined
      ? {}
      : {
          display: 'grid',
          gap: theme.spacing(3),
          [theme.breakpoints.up('xl')]: {
            gridTemplateColumns: `minmax(0, 1fr) ${String(asideWidth.xl)}px`,
          },
          [theme.breakpoints.up('xxl')]: {
            gridTemplateColumns: `minmax(0, 1fr) ${String(asideWidth.xxl)}px`,
          },
        }),
  }),
)

const AppShellPrimary = styled('div', { name, slot: 'primary' })<{ ownerState: OwnerState }>({
  minWidth: 0,
})

// The landmark around the rail's and the drawer's list.
const AppShellNav = styled('nav', { name, slot: 'nav' })<{ ownerState: OwnerState }>({})

const AppShellAside = styled('aside', { name, slot: 'aside' })<{ ownerState: OwnerState }>({
  minWidth: 0,
})

const AppShell = (inProps: AppShellProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { linkComponent, logoChip, accountMenu, aside, children } = props
  const full = props.variant === 'minimal' ? undefined : props
  const { t } = useTranslation('shell')
  const theme = useTheme()
  const fromSm = useMediaQuery(theme.breakpoints.up('sm'), { noSsr: true })
  const fromLg = useMediaQuery(theme.breakpoints.up('lg'), { noSsr: true })
  const navigation: Navigation =
    full === undefined ? 'none' : fromLg ? 'drawer' : fromSm ? 'rail' : 'bottomBar'
  const ownerState: OwnerState = { ...props, navigation }
  const navigationLabel = t('navigation.label')

  // The bottom bar's height varies (labels wrap), so it is measured; until then, or where
  // `ResizeObserver` does not exist, the inset is the fallback.
  const bottomBarRef = useRef<HTMLDivElement>(null)
  const [bottomBarHeight, setBottomBarHeight] = useState<number | null>(null)
  const hasBottomBar = navigation === 'bottomBar'
  useEffect(() => {
    const bar = bottomBarRef.current
    if (!hasBottomBar || bar === null || typeof ResizeObserver === 'undefined') {
      return undefined
    }
    const observer = new ResizeObserver(() => {
      setBottomBarHeight(bar.getBoundingClientRect().height)
    })
    observer.observe(bar)
    return () => {
      observer.disconnect()
      // A bar that returns uses the fallback until it is measured again.
      setBottomBarHeight(null)
    }
  }, [hasBottomBar])

  const insetStart = {
    bottomBar: '0px',
    rail: theme.spacing(11),
    drawer: theme.spacing(32),
    none: '0px',
  }
  const insetBottom = !hasBottomBar
    ? '0px'
    : bottomBarHeight === null
      ? bottomInsetFallback
      : `${String(bottomBarHeight)}px`

  const items = (variantProps: AppShellFullProps) =>
    variantProps.items.map((item) => {
      const selected = item.key === variantProps.activeKey
      return (
        <ListItem key={item.key} disablePadding>
          <ListItemButton
            component={linkComponent}
            to={item.to}
            selected={selected}
            aria-current={selected ? 'page' : undefined}
          >
            <ListItemIcon>{item.icon}</ListItemIcon>
            <ListItemText
              primary={item.label}
              slotProps={navigation === 'rail' ? { primary: { variant: 'caption' } } : undefined}
            />
          </ListItemButton>
        </ListItem>
      )
    })

  const list = (variantProps: AppShellFullProps) => (
    <AppShellNav aria-label={navigationLabel} ownerState={ownerState}>
      <List>{items(variantProps)}</List>
    </AppShellNav>
  )

  return (
    <AppShellRoot ownerState={ownerState}>
      <GlobalStyles
        styles={(globalTheme) => ({
          html: {
            [shellInsetNames.top]: `calc(${globalTheme.spacing(7)} + 1px)`,
            [globalTheme.breakpoints.up('sm')]: {
              [shellInsetNames.top]: `calc(${globalTheme.spacing(8)} + 1px)`,
            },
            [shellInsetNames.start]: insetStart[navigation],
            [shellInsetNames.bottom]: insetBottom,
            scrollPaddingTop: `calc(${shellInset('top')} + ${globalTheme.spacing(1)})`,
            scrollPaddingBottom: `calc(${shellInset('bottom')} + ${globalTheme.spacing(1)})`,
          },
        })}
      />
      <AppShellAppBar position="fixed" ownerState={ownerState}>
        <AppShellToolbar variant="dense" ownerState={ownerState}>
          <Logo linkComponent={linkComponent} to="/" />
          {logoChip === undefined ? null : (
            <AppShellLogoChip size="small" label={logoChip} ownerState={ownerState} />
          )}
          <AppShellControls ownerState={ownerState}>
            <GlowSwitch />
            <ModeSwitch />
            {fromSm || full === undefined ? <LanguageSwitch /> : null}
            {accountMenu}
          </AppShellControls>
        </AppShellToolbar>
      </AppShellAppBar>

      {full !== undefined && navigation === 'drawer' ? (
        <AppShellDrawer variant="permanent" ownerState={ownerState}>
          {list(full)}
        </AppShellDrawer>
      ) : null}
      {full !== undefined && navigation === 'rail' ? (
        <AppShellRail variant="permanent" ownerState={ownerState}>
          {list(full)}
        </AppShellRail>
      ) : null}
      {full !== undefined && navigation === 'bottomBar' ? (
        <AppShellBottomBar
          ref={bottomBarRef}
          elevation={3}
          component="nav"
          aria-label={navigationLabel}
          ownerState={ownerState}
        >
          <BottomNavigation showLabels value={full.activeKey}>
            {full.items.map((item) => (
              <BottomNavigationAction
                key={item.key}
                component={linkComponent}
                to={item.to}
                value={item.key}
                label={item.label}
                icon={item.icon}
                aria-current={item.key === full.activeKey ? 'page' : undefined}
              />
            ))}
          </BottomNavigation>
        </AppShellBottomBar>
      ) : null}

      <AppShellMain ownerState={ownerState}>
        <AppShellContent ownerState={ownerState}>
          {aside === undefined ? (
            children
          ) : (
            <>
              <AppShellPrimary ownerState={ownerState}>{children}</AppShellPrimary>
              <AppShellAside ownerState={ownerState}>{aside}</AppShellAside>
            </>
          )}
        </AppShellContent>
      </AppShellMain>
    </AppShellRoot>
  )
}

export default AppShell
