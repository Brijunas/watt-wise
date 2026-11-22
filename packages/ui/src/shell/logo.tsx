import Link from '@mui/material/Link'
import { styled, useThemeProps } from '@mui/material/styles'
import type { Theme } from '@mui/material/styles'
import type { CSSObject } from '@mui/material/styles'
import type { ElementType } from 'react'
import { useTranslation } from 'react-i18next'

export interface LogoProps {
  linkComponent: ElementType<{ to: string }>
  to: string
}

export type LogoClassKey = 'root' | 'lightLockup' | 'darkLockup' | 'mono' | 'wordmark'

const name = 'WwLogo'

interface OwnerState {
  ownerState: LogoProps
}

// The lockup files' size (the width and height attributes keep the layout from shifting) and the
// mono mark's viewBox.
const lockupWidth = 1908
const lockupHeight = 606
const monoAspectRatio = '871 / 745'

// The logo's trailing space, as a share of its height.
const clearSpace = 0.25

// layout.md "App shell": 32px, 36 from 360, 40 from 400, 48 from `sm`.
const heights = [
  { query: null, height: 32 },
  { query: 360, height: 36 },
  { query: 400, height: 40 },
  { query: 'sm', height: 48 },
] as const

// One rule per breakpoint: `build` gets the logo's height in px.
const atHeights = (theme: Theme, build: (height: number) => CSSObject): CSSObject => {
  const style: CSSObject = {}
  for (const { query, height } of heights) {
    if (query === null) Object.assign(style, build(height))
    else style[theme.breakpoints.up(query)] = build(height)
  }
  return style
}

const forcedColors = '@media (forced-colors: active)'

const LogoRoot = styled(Link, { name, slot: 'root' })<
  OwnerState & { component: ElementType; to: string }
>(({ theme }) => ({
  display: 'inline-flex',
  // The logo keeps its size; whatever shares the app bar with it gives way.
  flexShrink: 0,
  alignItems: 'center',
  gap: theme.spacing(1),
  color: 'inherit',
  textDecoration: 'none',
  // Clear space: the cap height of WISE, a quarter of the lockup's height at each size.
  ...atHeights(theme, (height) => ({ marginInlineEnd: height * clearSpace })),
}))

// Both lockups carry the glow as a filter; `glow.logo` is `none` while glow is off, so no
// component reads the setting. Forced colours show the mono mark and the wordmark instead.
const lockupStyle = (theme: Theme): CSSObject => ({
  width: 'auto',
  filter: theme.vars.glow.logo,
  transition: theme.transitions.create('filter', {
    duration: theme.transitions.duration.short,
  }),
  ...atHeights(theme, (height) => ({ height })),
})

const forcedColorsHidden: CSSObject = {
  [forcedColors]: { display: 'none', filter: 'none' },
}

const LogoLightLockup = styled('img', { name, slot: 'lightLockup' })<OwnerState>(({ theme }) => [
  lockupStyle(theme),
  theme.applyStyles('dark', { display: 'none' }),
  // After the dark rule, which has the same specificity, so it wins.
  forcedColorsHidden,
])

const LogoDarkLockup = styled('img', { name, slot: 'darkLockup' })<OwnerState>(({ theme }) => [
  lockupStyle(theme),
  { display: 'none' },
  theme.applyStyles('dark', { display: 'inline' }),
  forcedColorsHidden,
])

// The forced-colours mark: the mono SVG as a mask filled with the system text colour (an `<img>`
// SVG can't inherit `currentColor`).
const LogoMono = styled('span', { name, slot: 'mono' })<OwnerState>(({ theme }) => ({
  display: 'none',
  [forcedColors]: {
    display: 'inline-block',
    aspectRatio: monoAspectRatio,
    mask: 'url(/logo-mark-mono.svg) center / contain no-repeat',
    backgroundColor: 'CanvasText',
    forcedColorAdjust: 'none',
    ...atHeights(theme, (height) => ({ height })),
  },
}))

const LogoWordmark = styled('span', { name, slot: 'wordmark' })<OwnerState>(({ theme }) => ({
  display: 'none',
  [forcedColors]: {
    display: 'inline-flex',
    flexDirection: 'column',
    justifyContent: 'center',
    color: 'CanvasText',
    fontFamily: theme.typography.fontFamily,
    lineHeight: 1,
    ...atHeights(theme, (height) => ({ fontSize: height * 0.4 })),
    '& > :first-of-type': { fontWeight: 800, letterSpacing: '0.14em' },
    '& > :last-of-type': { fontWeight: 700 },
  },
}))

const Logo = (inProps: LogoProps) => {
  const props = useThemeProps({ props: inProps, name })
  const { linkComponent, to } = props
  const { t } = useTranslation('shell')

  return (
    <LogoRoot component={linkComponent} to={to} aria-label={t('logo.home')} ownerState={props}>
      <LogoLightLockup
        src="/logo-lockup-light.png"
        alt=""
        width={lockupWidth}
        height={lockupHeight}
        ownerState={props}
      />
      <LogoDarkLockup
        src="/logo-lockup-dark.png"
        alt=""
        width={lockupWidth}
        height={lockupHeight}
        ownerState={props}
      />
      <LogoMono aria-hidden ownerState={props} />
      <LogoWordmark aria-hidden ownerState={props}>
        <span>WATT</span>
        <span>WISE</span>
      </LogoWordmark>
    </LogoRoot>
  )
}

export default Logo
