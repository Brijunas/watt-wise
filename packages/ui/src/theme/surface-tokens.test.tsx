import HomeIcon from '@mui/icons-material/HomeRounded'
import Alert from '@mui/material/Alert'
import AppBar from '@mui/material/AppBar'
import Card from '@mui/material/Card'
import Chip from '@mui/material/Chip'
import Menu from '@mui/material/Menu'
import MenuItem from '@mui/material/MenuItem'
import SnackbarContent from '@mui/material/SnackbarContent'
import Toolbar from '@mui/material/Toolbar'
import { screen } from '@testing-library/react'
import type { AnchorHTMLAttributes, ReactElement } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import AppShell from '../shell/app-shell'
import {
  cssFor,
  renderWithComponents,
  resetThemeState,
  type ColorMode,
} from '../testing/theme-test-utils'

// style.md's "Colour tokens" and "Components" tables map each surface to a token. The per-component
// theme tests check the override's own text; this one renders the real MUI component under the real
// theme and checks the rule Emotion emitted for the element, so a surface that falls back to MUI's
// default (the app bar on `primary.main`) fails here. jsdom doesn't resolve CSS variables, so the
// declaration is compared as the `var(--mui-palette-…)` reference. The theme emits the same rule in
// both modes (the variables change per colour scheme), so running light as well would only double
// the runs; the cases run in dark, where the elevation overlay (`background-image`) would show.
afterEach(() => {
  resetThemeState()
  vi.unstubAllGlobals()
})

const TestLink = ({ to, ...rest }: { to: string } & AnchorHTMLAttributes<HTMLAnchorElement>) => (
  <a href={to} {...rest} />
)

const shell = (): ReactElement => (
  <AppShell
    items={[{ key: 'home', label: 'Home page', icon: <HomeIcon />, to: '/' }]}
    activeKey="home"
    linkComponent={TestLink}
  >
    <p>Page content</p>
  </AppShell>
)

// The value of the last `property:` declaration in the element's own rule: the one that wins.
const declared = (element: Element, property: string): string | undefined => {
  const css = cssFor(element)
  const pattern = new RegExp('(?:^|[{;])' + property + ':([^;}]*)', 'g')
  return Array.from(css.matchAll(pattern))
    .map((match) => match[1])
    .at(-1)
}

const surfaceVariable = (token: string) => new RegExp('^var\\(--mui-palette-' + token + '[,)]')

const found = (selector: string): Element => {
  const element = document.querySelector(selector)
  if (element === null) throw new Error('Missing element ' + selector)
  return element
}

interface Surface {
  name: string
  /** `background.paper` or `surface2`, as its CSS-variable name. */
  token: 'background-paper' | 'surface2'
  /** Whether style.md wants a flat surface: no dark elevation overlay. */
  flat: boolean
  width?: number
  render: () => ReactElement
  /** The element that carries the surface. */
  element: () => Element
}

const surfaces: readonly Surface[] = [
  {
    name: 'the app bar (theme defaults, no colour prop)',
    token: 'background-paper',
    flat: true,
    render: () => (
      <AppBar position="static">
        <Toolbar>Watt-Wise</Toolbar>
      </AppBar>
    ),
    element: () => screen.getByRole('banner'),
  },
  {
    name: "the shell's app bar",
    token: 'background-paper',
    flat: true,
    render: shell,
    element: () => screen.getByRole('banner'),
  },
  {
    name: 'a default card',
    token: 'background-paper',
    flat: true,
    render: () => <Card data-testid="card">Body</Card>,
    element: () => screen.getByTestId('card'),
  },
  {
    name: 'an outlined card',
    token: 'background-paper',
    flat: true,
    render: () => (
      <Card variant="outlined" data-testid="card">
        Body
      </Card>
    ),
    element: () => screen.getByTestId('card'),
  },
  {
    name: 'the menu paper',
    token: 'background-paper',
    flat: true,
    render: () => (
      <Menu open anchorEl={document.body}>
        <MenuItem>Item</MenuItem>
      </Menu>
    ),
    // Class selectors: jsdom gives these papers no role.
    element: () => found('.MuiMenu-paper'),
  },
  {
    name: "the shell's bottom bar (360px)",
    token: 'background-paper',
    flat: true,
    width: 360,
    render: shell,
    element: () => screen.getByRole('navigation'),
  },
  {
    name: "the shell's bottom navigation (360px)",
    token: 'background-paper',
    flat: false,
    width: 360,
    render: shell,
    // No role for the inner bar; at 360px it is the only BottomNavigation.
    element: () => found('.MuiBottomNavigation-root'),
  },
  {
    name: "the shell's rail paper (900px)",
    token: 'background-paper',
    flat: true,
    width: 900,
    render: shell,
    // Class selector (no role); the shell renders one permanent Drawer at a time.
    element: () => found('.MuiDrawer-paper'),
  },
  {
    name: "the shell's drawer paper (1280px)",
    token: 'background-paper',
    flat: true,
    width: 1280,
    render: shell,
    // Class selector (no role); the shell renders one permanent Drawer at a time.
    element: () => found('.MuiDrawer-paper'),
  },
  {
    name: "the update prompt's snackbar content",
    token: 'background-paper',
    flat: true,
    render: () => <SnackbarContent message="Update" data-testid="content" />,
    element: () => screen.getByTestId('content'),
  },
  {
    name: 'a default chip',
    token: 'surface2',
    flat: false,
    render: () => <Chip label="Chip" data-testid="chip" />,
    element: () => screen.getByTestId('chip'),
  },
  {
    name: 'an info alert',
    token: 'surface2',
    flat: false,
    render: () => <Alert severity="info">Note</Alert>,
    element: () => screen.getByRole('alert'),
  },
]

const mode: ColorMode = 'dark'

describe.each(surfaces)('$name', (surface) => {
  const renderSurface = () => {
    renderWithComponents(surface.render(), { mode, width: surface.width })
    return surface.element()
  }

  it('is filled with its mapped token', () => {
    const element = renderSurface()
    expect(declared(element, 'background-color')).toMatch(surfaceVariable(surface.token))
  })

  // Guards the S5A.2 app-bar regression: MUI's default fill is `--AppBar-background`, from the
  // primary colour.
  it('has no primary or app-bar default fill in its rule', () => {
    const rule = cssFor(renderSurface())
    expect(rule).not.toContain('--AppBar-background')
    expect(rule).not.toContain('--mui-palette-primary-main')
  })
})

describe.each(surfaces.filter((surface) => surface.flat))('$name', (surface) => {
  it('has no elevation overlay image', () => {
    renderWithComponents(surface.render(), { mode, width: surface.width })
    expect(declared(surface.element(), 'background-image')).toBe('none')
  })
})
