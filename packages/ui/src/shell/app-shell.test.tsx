import HomeIcon from '@mui/icons-material/HomeRounded'
import InboxIcon from '@mui/icons-material/InboxRounded'
import { act, screen, within } from '@testing-library/react'
import type { AnchorHTMLAttributes, ReactNode } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import {
  colorModes,
  cssFor,
  emittedCss,
  renderWithComponents,
  resetThemeState,
} from '../testing/theme-test-utils'
import type { MatchMediaFake } from '../testing/create-match-media'
import AppShell from './app-shell'
import type { AppShellNavItem } from './app-shell-nav-item'

function TestLink({ to, ...rest }: { to: string } & AnchorHTMLAttributes<HTMLAnchorElement>) {
  return <a href={to} {...rest} />
}

const items: readonly AppShellNavItem[] = [
  { key: 'home', label: 'Home page', icon: <HomeIcon />, to: '/' },
  { key: 'inbox', label: 'Inbox page', icon: <InboxIcon />, to: '/inbox' },
]

afterEach(() => {
  resetThemeState()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

const text = {
  en: {
    navigation: 'Main navigation',
    language: 'Language: EN',
    logo: 'Watt-Wise home',
    code: 'EN',
  },
  lt: {
    navigation: 'Pagrindinė navigacija',
    language: 'Kalba: LT',
    logo: 'Watt-Wise pradžia',
    code: 'LT',
  },
} as const

const languages = ['en', 'lt'] as const

interface RenderOptions {
  width: number
  lng?: 'en' | 'lt'
  mode?: 'light' | 'dark'
  activeKey?: string | null
  logoChip?: string
  accountMenu?: ReactNode
  aside?: ReactNode
}

const renderShell = ({
  width,
  lng = 'en',
  mode,
  activeKey = 'home',
  logoChip,
  accountMenu,
  aside,
}: RenderOptions) =>
  renderWithComponents(
    <AppShell
      items={items}
      activeKey={activeKey}
      linkComponent={TestLink}
      logoChip={logoChip}
      accountMenu={accountMenu}
      aside={aside}
    >
      <p>Page content</p>
    </AppShell>,
    { width, lng, mode },
  )

const renderMinimal = ({
  width,
  lng = 'en',
  logoChip,
  accountMenu,
}: Pick<RenderOptions, 'width' | 'lng' | 'logoChip' | 'accountMenu'>) =>
  renderWithComponents(
    <AppShell
      variant="minimal"
      linkComponent={TestLink}
      logoChip={logoChip}
      accountMenu={accountMenu}
    >
      <p>Page content</p>
    </AppShell>,
    { width, lng },
  )

const generatedClass = (element: Element) => {
  const found = Array.from(element.classList).find((name) => name.startsWith('css-'))
  if (found === undefined) throw new Error('Missing generated class')
  return found
}

// The slot's own element, found by the slot name Emotion puts in the class.
const slot = (name: string) => {
  const found = document.querySelector('[class*="-WwAppShell-' + name + '"]')
  if (found === null) throw new Error('Missing slot ' + name)
  return found
}

// Changes the width the fake `matchMedia` answers and tells `useMediaQuery`, like a window resize.
const resizeTo = (width: number) => {
  ;(window.matchMedia as MatchMediaFake).resize(width)
}

const paperRule = (root: Element) => nestedRule(root, '.MuiDrawer-paper')

// The rule Emotion emitted for `selector` nested under the slot's class.
const nestedRule = (root: Element, nested: string) => {
  const selector = '.' + generatedClass(root) + ' ' + nested + '{'
  return (
    emittedCss()
      .split('}')
      .find((rule) => rule.includes(selector)) ?? ''
  )
}

// jsdom ignores media queries, so the responsive values are read from the emitted CSS.
const content = () => within(screen.getByRole('main')).getByText('Page content').parentElement
const contentElement = () => {
  const element = content()
  if (element === null) throw new Error('Missing content')
  return element
}

describe.each(languages)('AppShell navigation in %s', (lng) => {
  const t = text[lng]

  it.each([360, 599])('shows the bottom bar at %ipx', (width) => {
    renderShell({ width, lng })
    const nav = screen.getByRole('navigation', { name: t.navigation })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(nav.querySelector('.MuiBottomNavigation-root')).not.toBeNull()
    expect(nav.closest('.MuiDrawer-root')).toBeNull()
  })

  it.each([600, 900, 1199])('shows the rail at %ipx', (width) => {
    renderShell({ width, lng })
    const nav = screen.getByRole('navigation', { name: t.navigation })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(nav.querySelector('.MuiBottomNavigation-root')).toBeNull()
    expect(nav.closest('.MuiDrawer-root')?.className).toContain('-WwAppShell-rail')
  })

  it.each([1200, 1366])('shows the drawer at %ipx', (width) => {
    renderShell({ width, lng })
    const nav = screen.getByRole('navigation', { name: t.navigation })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(nav.closest('.MuiDrawer-root')?.className).toContain('-WwAppShell-drawer')
  })

  it.each([360, 900, 1366])('links every item and marks the active one at %ipx', (width) => {
    renderShell({ width, lng, activeKey: 'inbox' })
    const home = screen.getByRole('link', { name: 'Home page' })
    const inbox = screen.getByRole('link', { name: 'Inbox page' })
    expect([home.getAttribute('href'), inbox.getAttribute('href')]).toEqual(['/', '/inbox'])
    expect([home.getAttribute('aria-current'), inbox.getAttribute('aria-current')]).toEqual([
      null,
      'page',
    ])
  })

  it.each([360, 900, 1366])('marks no item current without an active key at %ipx', (width) => {
    renderShell({ width, lng, activeKey: null })
    const links = screen.getAllByRole('link', { name: /page$/ })
    expect(links.map((link) => link.getAttribute('aria-current'))).toEqual([null, null])
  })

  it.each([900, 1366])('selects only the active item at %ipx', (width) => {
    renderShell({ width, lng, activeKey: 'inbox' })
    expect([
      screen.getByRole('link', { name: 'Home page' }).classList.contains('Mui-selected'),
      screen.getByRole('link', { name: 'Inbox page' }).classList.contains('Mui-selected'),
    ]).toEqual([false, true])
  })

  it.each([360, 900])('names the logo link and puts the children in main at %ipx', (width) => {
    renderShell({ width, lng })
    expect(screen.getByRole('link', { name: t.logo }).getAttribute('href')).toBe('/')
    expect(within(screen.getByRole('main')).getByText('Page content')).not.toBeNull()
  })
})

describe.each(languages)('AppShell minimal variant in %s', (lng) => {
  const t = text[lng]

  it.each([360, 900, 1366])('has no navigation landmark at %ipx', (width) => {
    renderMinimal({ width, lng })
    expect(screen.queryAllByRole('navigation')).toHaveLength(0)
    expect(screen.queryAllByRole('link', { name: /page$/ })).toHaveLength(0)
  })

  it.each([360, 900, 1366])('keeps the language button at %ipx', (width) => {
    renderMinimal({ width, lng })
    expect(screen.getByRole('button', { name: t.language })).not.toBeNull()
  })

  it('renders the logo, the glow switch, the theme menu and the children', () => {
    renderMinimal({ width: 360, lng })
    expect(screen.getByRole('link', { name: t.logo }).getAttribute('href')).toBe('/')
    expect(screen.getAllByRole('switch')).toHaveLength(1)
    expect(within(screen.getByRole('main')).getByText('Page content')).not.toBeNull()
  })

  it('measures no bottom bar: the main has no bottom inset', () => {
    renderMinimal({ width: 360, lng })
    expect(emittedCss()).toContain('--ww-shell-inset-bottom:0px;')
  })
})

describe.each(languages)('AppShell app bar in %s', (lng) => {
  const t = text[lng]

  it('hides the language button below 600px in the full variant', () => {
    renderShell({ width: 599, lng })
    expect(screen.queryAllByRole('button', { name: t.language })).toHaveLength(0)
  })

  it.each([600, 900])('shows the language button at %ipx in the full variant', (width) => {
    renderShell({ width, lng })
    expect(screen.getByRole('button', { name: t.language })).not.toBeNull()
  })

  it('shows the logo chip after the logo', () => {
    renderShell({ width: 900, lng, logoChip: 'Admin' })
    const logo = screen.getByRole('link', { name: t.logo })
    const chip = screen.getByText('Admin')
    expect(logo.compareDocumentPosition(chip) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  // The chip gives way to the controls at any width or text size, down to nothing.
  it('lets the logo chip shrink, clipped, when the bar is tight', () => {
    renderShell({ width: 360, lng, logoChip: 'Admin' })
    const css = cssFor(slot('logoChip'))
    expect(css).toContain('flex-shrink:1;')
    expect(css).toContain('min-width:0;')
    expect(css).toContain('overflow:hidden;')
  })

  // At 200% text the label would be clipped by MUI's fixed 24px small-chip height.
  it('grows the logo chip with its label from a 24px minimum', () => {
    renderShell({ width: 360, lng, logoChip: 'Admin' })
    const css = cssFor(slot('logoChip'))
    expect(css).toContain('height:auto;')
    expect(css).toContain('min-height:calc(3 * var(--mui-spacing));')
  })

  it('applies the chip height over the small chip height of MUI', () => {
    renderShell({ width: 360, lng, logoChip: 'Admin' })
    const css = cssFor(slot('logoChip'))
    expect(css).toContain('height:24px;')
    expect(css.indexOf('height:auto;')).toBeGreaterThan(css.lastIndexOf('height:24px;'))
  })

  it('leaves a 4px gap after the logo chip', () => {
    renderShell({ width: 360, lng, logoChip: 'Admin' })
    expect(cssFor(slot('logoChip'))).toContain('margin-inline-end:calc(0.5 * var(--mui-spacing));')
  })

  // Pins MUI's own Chip label style, which the chip's shrinking relies on.
  it('ends a chip label that does not fit in an ellipsis', () => {
    renderShell({ width: 360, lng, logoChip: 'Admin' })
    const css = cssFor(screen.getByText('Admin'))
    expect(css).toContain('overflow:hidden;')
    expect(css).toContain('text-overflow:ellipsis;')
  })

  it('keeps the logo from shrinking', () => {
    renderShell({ width: 360, lng })
    expect(cssFor(screen.getByRole('link', { name: text[lng].logo }))).toContain('flex-shrink:0;')
  })

  it('keeps the controls from shrinking', () => {
    renderShell({ width: 360, lng })
    expect(cssFor(slot('controls'))).toContain('flex-shrink:0;')
  })

  it('shows no chip without logoChip', () => {
    renderShell({ width: 900, lng })
    expect(document.querySelector('.MuiChip-root')).toBeNull()
  })

  it('renders the account menu as the last control of the app bar', () => {
    renderShell({ width: 900, lng, accountMenu: <button type="button">Account</button> })
    const buttons = within(screen.getByRole('banner')).getAllByRole('button')
    expect(buttons.map((button) => button.textContent)).toEqual(['', t.code, 'Account'])
  })

  it('renders the aside after the children', () => {
    renderShell({ width: 900, lng, aside: <p>Side text</p> })
    const main = screen.getByRole('main')
    expect(
      within(main)
        .getByText('Page content')
        .compareDocumentPosition(within(main).getByText('Side text')) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBeTruthy()
    expect(screen.getByText('Side text').closest('aside')).not.toBeNull()
  })

  it('renders the children alone without an aside', () => {
    renderShell({ width: 900, lng })
    expect(screen.queryAllByRole('complementary')).toHaveLength(0)
  })
})

describe.each(languages)('AppShell accessibility in %s', (lng) => {
  const rules = { region: { enabled: false } }
  const forms = [
    { form: 'bottom bar', width: 360 },
    { form: 'rail', width: 900 },
    { form: 'drawer', width: 1366 },
  ]

  describe.each(colorModes)('%s mode', (mode) => {
    it.each(forms)('has no violations with the $form', async ({ width }) => {
      const { container } = renderShell({
        width,
        lng,
        mode,
        logoChip: 'Admin',
        aside: <p>Side</p>,
      })
      // Every form with the chip and a side column, the markup that differs most.
      expect((await axe(container, { rules })).violations).toEqual([])
    })

    it('has no violations in the minimal variant', async () => {
      const { container } = renderMinimal({ width: 360, lng })
      expect((await axe(container, { rules })).violations).toEqual([])
    })
  })
})

describe('AppShell responsive CSS', () => {
  // `sm` is 600, `lg` 1200, `xl` 1600 and `xxl` 2400 in the theme.
  it.each([
    { name: 'rail', width: 900, size: 'calc(11 * var(--mui-spacing))' }, // 88px
    { name: 'drawer', width: 1366, size: 'calc(32 * var(--mui-spacing))' }, // 256px
  ])('sizes the $name and its paper to its width', ({ name, width, size }) => {
    renderShell({ width })
    const root = slot(name)
    expect(cssFor(root)).toContain('width:' + size + ';')
    expect(paperRule(root)).toContain('width:' + size + ';')
  })

  it.each([
    { name: 'rail', width: 900 },
    { name: 'drawer', width: 1366 },
  ])('starts the $name paper under the app bar', ({ name, width }) => {
    renderShell({ width })
    expect(paperRule(slot(name))).toContain('padding-top:var(--ww-shell-inset-top);')
  })

  it('gives the main a 16px gutter by default', () => {
    renderShell({ width: 360 })
    expect(cssFor(screen.getByRole('main'))).toContain(
      'padding-inline:calc(2 * var(--mui-spacing));',
    )
  })

  it('gives the main a 24px gutter from 600px', () => {
    renderShell({ width: 900 })
    expect(cssFor(screen.getByRole('main'), '(min-width:600px)')).toContain(
      'padding-inline:calc(3 * var(--mui-spacing));',
    )
  })

  it('gives the main a 40px gutter from 1200px', () => {
    renderShell({ width: 1366 })
    expect(cssFor(screen.getByRole('main'), '(min-width:1200px)')).toContain(
      'padding-inline:calc(5 * var(--mui-spacing));',
    )
  })

  it('lets the main shrink below its content (min-width: 0)', () => {
    renderShell({ width: 360 })
    expect(cssFor(screen.getByRole('main'))).toContain('min-width:0;')
  })

  it('lets the content shrink and centres it (min-width: 0, auto margins)', () => {
    renderShell({ width: 360 })
    const css = cssFor(contentElement())
    expect(css).toContain('min-width:0;')
    expect(css).toContain('margin-inline:auto;')
    expect(css).toContain('width:100%;')
  })

  it('caps the content at 1120px', () => {
    renderShell({ width: 1366 })
    expect(cssFor(contentElement())).toContain('max-width:1120px;')
  })

  it('caps the content at 1440px from 1600px', () => {
    renderShell({ width: 1920 })
    expect(cssFor(contentElement(), '(min-width:1600px)')).toContain('max-width:1440px;')
  })

  it('caps the content at 1600px from 2400px', () => {
    renderShell({ width: 2560 })
    expect(cssFor(contentElement(), '(min-width:2400px)')).toContain('max-width:1600px;')
  })

  it('makes the content a one-column block without an aside', () => {
    renderShell({ width: 1920 })
    expect(cssFor(contentElement())).not.toContain('display:grid;')
  })

  it('puts the aside in a 340px column from 1600px', () => {
    renderShell({ width: 1920, aside: <p>Side</p> })
    const grid = screen.getByText('Page content').parentElement?.parentElement
    if (grid === null || grid === undefined) throw new Error('Missing grid')
    expect(cssFor(grid)).toContain('display:grid;')
    expect(cssFor(grid)).toContain('gap:calc(3 * var(--mui-spacing));')
    expect(cssFor(grid, '(min-width:1600px)')).toContain(
      'grid-template-columns:minmax(0, 1fr) 340px;',
    )
  })

  it('puts the aside in a 380px column from 2400px', () => {
    renderShell({ width: 2560, aside: <p>Side</p> })
    const grid = screen.getByText('Page content').parentElement?.parentElement
    if (grid === null || grid === undefined) throw new Error('Missing grid')
    expect(cssFor(grid, '(min-width:2400px)')).toContain(
      'grid-template-columns:minmax(0, 1fr) 380px;',
    )
  })

  it('stacks the aside below 1600px (no grid columns)', () => {
    renderShell({ width: 900, aside: <p>Side</p> })
    const grid = screen.getByText('Page content').parentElement?.parentElement
    if (grid === null || grid === undefined) throw new Error('Missing grid')
    expect(cssFor(grid)).not.toContain('grid-template-columns')
  })

  it('lets the primary column and the aside shrink', () => {
    renderShell({ width: 1920, aside: <p>Side</p> })
    expect([
      cssFor(screen.getByText('Page content').parentElement ?? document.body),
      cssFor(screen.getByText('Side').closest('aside') ?? document.body),
    ]).toEqual([expect.stringContaining('min-width:0;'), expect.stringContaining('min-width:0;')])
  })

  it('sets the toolbar 56px high by default and 64px from 600px', () => {
    renderShell({ width: 900 })
    const toolbar = screen.getByRole('banner').firstElementChild
    if (toolbar === null) throw new Error('Missing toolbar')
    expect(cssFor(toolbar)).toContain('min-height:calc(7 * var(--mui-spacing));')
    expect(cssFor(toolbar, '(min-width:600px)')).toContain(
      'min-height:calc(8 * var(--mui-spacing));',
    )
  })

  // MUI's toolbar mixin drops to 48px in landscape below 600px; the shell's inset (57px) assumes 56.
  it('keeps the toolbar at 56px in landscape too', () => {
    renderShell({ width: 360 })
    const toolbar = screen.getByRole('banner').firstElementChild
    if (toolbar === null) throw new Error('Missing toolbar')
    expect(cssFor(toolbar, '(min-width:0px){@media (orientation: landscape)')).not.toContain(
      'min-height:48px',
    )
  })

  it('tightens the toolbar gutters to 8px below 400px', () => {
    renderShell({ width: 360 })
    const toolbar = screen.getByRole('banner').firstElementChild
    if (toolbar === null) throw new Error('Missing toolbar')
    expect(cssFor(toolbar, '(max-width:399.95px)')).toContain('padding-inline:var(--mui-spacing);')
  })

  it('gives the app bar buttons a 44px minimum width and a 44px height', () => {
    renderShell({ width: 360 })
    const rule = nestedRule(slot('controls'), '.MuiIconButton-root')
    expect(rule).toContain('min-width:calc(5.5 * var(--mui-spacing));')
    expect(rule).toContain('height:calc(5.5 * var(--mui-spacing));')
  })

  // A minimum, not a fixed width: the language code grows with the text size and widens its button.
  it('sets no fixed width on the app bar buttons', () => {
    renderShell({ width: 360 })
    expect(nestedRule(slot('controls'), '.MuiIconButton-root')).not.toMatch(/[{;]width:/)
  })

  it('rounds the app bar buttons with the chip radius, so a grown button is a pill', () => {
    renderShell({ width: 360 })
    expect(nestedRule(slot('controls'), '.MuiIconButton-root')).toMatch(
      /border-radius:var\(--mui-shape-radius-chip[,)]/,
    )
  })

  it('gives the app bar buttons a 40px minimum width and height below 400px', () => {
    renderShell({ width: 360 })
    const cls = generatedClass(slot('controls'))
    expect(emittedCss()).toContain(
      '@media (max-width:399.95px){.' +
        cls +
        ' .MuiIconButton-root{min-width:calc(5 * var(--mui-spacing));height:calc(5 * var(--mui-spacing));}}',
    )
  })

  // MUI sizes icons in rem, which would grow them over the buttons at 200% text.
  it('sizes the icons in the controls 24px whatever the text size', () => {
    renderShell({ width: 360 })
    expect(nestedRule(slot('controls'), '.MuiSvgIcon-root')).toContain('font-size:24px;')
  })

  it('gives the glow switch the same 44px and 40px minimum height', () => {
    renderShell({ width: 360 })
    const cls = generatedClass(slot('controls'))
    const css = emittedCss()
    expect(css).toContain(
      '.' + cls + ' .WwGlowSwitch-root{min-height:calc(5.5 * var(--mui-spacing));}',
    )
    expect(css).toContain(
      '@media (max-width:399.95px){.' +
        cls +
        ' .WwGlowSwitch-root{min-height:calc(5 * var(--mui-spacing));}}',
    )
  })

  it('lays the shell out as a full-height flex row', () => {
    renderShell({ width: 900 })
    const css = cssFor(slot('root'))
    expect(css).toContain('display:flex;')
    expect(css).toContain('min-height:100dvh;')
  })

  it('pushes the controls to the end of the app bar and centres them', () => {
    renderShell({ width: 900 })
    const css = cssFor(slot('controls'))
    expect(css).toContain('margin-inline-start:auto;')
    expect(css).toContain('display:flex;')
    expect(css).toContain('align-items:center;')
  })

  it('pads the main by 24px at the top and the bottom from 600px', () => {
    renderShell({ width: 900 })
    const css = cssFor(screen.getByRole('main'), '(min-width:600px)')
    expect(css).toContain(
      'padding-top:calc(var(--ww-shell-inset-top) + calc(3 * var(--mui-spacing)));',
    )
    expect(css).toContain(
      'padding-bottom:calc(var(--ww-shell-inset-bottom) + calc(3 * var(--mui-spacing)));',
    )
  })

  describe('the rail items', () => {
    it('stack the icon over a centred label', () => {
      renderShell({ width: 900 })
      const rule = nestedRule(slot('rail'), '.MuiListItemButton-root')
      expect(rule).toContain('flex-direction:column;')
      expect(rule).toContain('text-align:center;')
    })

    it('let the icon and the label take only their own size', () => {
      renderShell({ width: 900 })
      expect(nestedRule(slot('rail'), '.MuiListItemIcon-root')).toContain('min-width:0;')
      const text = nestedRule(slot('rail'), '.MuiListItemText-root')
      expect(text).toContain('flex:none;')
      expect(text).toContain('margin:0;')
    })

    it('wrap a long label anywhere', () => {
      renderShell({ width: 900 })
      expect(nestedRule(slot('rail'), '.MuiListItemText-primary')).toContain(
        'overflow-wrap:anywhere;',
      )
    })

    it('use the caption size for the label at 900px', () => {
      renderShell({ width: 900 })
      expect(screen.getByText('Home page').classList.contains('MuiTypography-caption')).toBe(true)
    })

    it('keep the normal label size in the drawer at 1366px', () => {
      renderShell({ width: 1366 })
      expect(screen.getByText('Home page').classList.contains('MuiTypography-caption')).toBe(false)
    })
  })

  it('sits the bottom bar on the paper surface without the dark overlay', () => {
    renderShell({ width: 360, mode: 'dark' })
    const css = cssFor(screen.getByRole('navigation', { name: 'Main navigation' }))
    expect(css).toContain('position:fixed;')
    expect(css).toContain(
      'background-color:var(--mui-palette-background-paper);background-image:none;',
    )
    expect(css).toContain('padding-bottom:env(safe-area-inset-bottom);')
  })
})

describe('AppShell insets', () => {
  const top = '--ww-shell-inset-top:calc(calc(7 * var(--mui-spacing)) + 1px);'
  const topFromSm =
    '@media (min-width:600px){html{--ww-shell-inset-top:calc(calc(8 * var(--mui-spacing)) + 1px);}}'

  it.each([
    { width: 360, start: '--ww-shell-inset-start:0px;' },
    { width: 900, start: '--ww-shell-inset-start:calc(11 * var(--mui-spacing));' },
    { width: 1366, start: '--ww-shell-inset-start:calc(32 * var(--mui-spacing));' },
  ])('publishes the app bar and start insets at $width px', ({ width, start }) => {
    renderShell({ width })
    const css = emittedCss()
    expect(css).toContain(top)
    expect(css).toContain(topFromSm)
    expect(css).toContain(start)
  })

  it('publishes the app bar inset in the minimal variant too, with no start inset', () => {
    renderMinimal({ width: 1366 })
    const css = emittedCss()
    expect(css).toContain(top)
    expect(css).toContain('--ww-shell-inset-start:0px;')
  })

  it('keeps the focused content clear of both bars through html scroll-padding', () => {
    renderShell({ width: 360 })
    const css = emittedCss()
    expect(css).toContain(
      'scroll-padding-top:calc(var(--ww-shell-inset-top) + var(--mui-spacing));',
    )
    expect(css).toContain(
      'scroll-padding-bottom:calc(var(--ww-shell-inset-bottom) + var(--mui-spacing));',
    )
  })

  it('clears the bars in the main padding instead of spacer toolbars', () => {
    renderShell({ width: 360 })
    const css = cssFor(screen.getByRole('main'))
    expect(css).toContain(
      'padding-top:calc(var(--ww-shell-inset-top) + calc(2 * var(--mui-spacing)));',
    )
    expect(css).toContain(
      'padding-bottom:calc(var(--ww-shell-inset-bottom) + calc(2 * var(--mui-spacing)));',
    )
  })

  it('falls back to the minimum bar height plus the safe area where nothing is measured', () => {
    renderShell({ width: 360 })
    expect(emittedCss()).toContain(
      '--ww-shell-inset-bottom:calc(56px + env(safe-area-inset-bottom));',
    )
  })

  it.each([900, 1366])('has no bottom inset at %ipx', (width) => {
    renderShell({ width })
    expect(emittedCss()).toContain('--ww-shell-inset-bottom:0px;')
  })

  // jsdom has no layout, so this asserts the emitted CSS (the inset variable, `scroll-padding`
  // and `main`'s padding all follow the bar's measured height); the smoke check covers the real
  // scroll of a focused last element.
  describe('a focused element at the bottom of a page is not covered by the bottom bar', () => {
    // `measureOnObserve: false` leaves the measuring to the test, which calls the callbacks.
    const stubObserver = (height: number, { measureOnObserve = true } = {}) => {
      const callbacks: (() => void)[] = []
      const disconnect = vi.fn()
      class FakeResizeObserver {
        constructor(callback: () => void) {
          callbacks.push(callback)
        }
        observe() {
          if (measureOnObserve) {
            callbacks[callbacks.length - 1]?.()
          }
        }
        disconnect = disconnect
      }
      vi.stubGlobal('ResizeObserver', FakeResizeObserver)
      const measured = { height }
      vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(
        () => ({ height: measured.height }) as DOMRect,
      )
      return { callbacks, disconnect, measured }
    }

    it('publishes the measured 72px bar as the bottom inset', () => {
      stubObserver(72)
      renderShell({ width: 360 })
      const css = emittedCss()
      expect(css).toContain('--ww-shell-inset-bottom:72px;')
      expect(css).toContain(
        'scroll-padding-bottom:calc(var(--ww-shell-inset-bottom) + var(--mui-spacing));',
      )
      expect(cssFor(screen.getByRole('main'))).toContain(
        'padding-bottom:calc(var(--ww-shell-inset-bottom) + calc(2 * var(--mui-spacing)));',
      )
    })

    it('follows the bar when its labels wrap and it grows to 90px', () => {
      const { callbacks, measured } = stubObserver(72)
      renderShell({ width: 360 })
      measured.height = 90
      act(() => {
        callbacks.forEach((callback) => {
          callback()
        })
      })
      expect(emittedCss()).toContain('--ww-shell-inset-bottom:90px;')
    })

    it('stops observing and publishes 0px when the window grows to the rail width', () => {
      const { disconnect } = stubObserver(72)
      renderShell({ width: 360 })
      act(() => {
        resizeTo(900)
      })
      expect(disconnect).toHaveBeenCalledTimes(1)
      expect(emittedCss()).toContain('--ww-shell-inset-bottom:0px;')
      expect(emittedCss()).not.toContain('--ww-shell-inset-bottom:72px;')
    })

    it('disconnects the observer when the shell unmounts', () => {
      const { disconnect } = stubObserver(72)
      const { unmount } = renderShell({ width: 360 })
      unmount()
      expect(disconnect).toHaveBeenCalledTimes(1)
    })

    it('observes the bar again when the window shrinks back, with the fallback until measured', () => {
      const { callbacks } = stubObserver(72, { measureOnObserve: false })
      renderShell({ width: 360 })
      act(() => {
        callbacks[0]?.()
      })
      expect(emittedCss()).toContain('--ww-shell-inset-bottom:72px;')
      act(() => {
        resizeTo(900)
      })
      act(() => {
        resizeTo(360)
      })
      expect(callbacks).toHaveLength(2)
      expect(emittedCss()).toContain(
        '--ww-shell-inset-bottom:calc(56px + env(safe-area-inset-bottom));',
      )
      act(() => {
        callbacks[1]?.()
      })
      expect(emittedCss()).toContain('--ww-shell-inset-bottom:72px;')
    })

    it('keeps the fallback inset and does not throw where ResizeObserver does not exist', () => {
      vi.stubGlobal('ResizeObserver', undefined)
      renderShell({ width: 360 })
      expect(emittedCss()).toContain(
        '--ww-shell-inset-bottom:calc(56px + env(safe-area-inset-bottom));',
      )
    })

    it.each([900, 1366])('observes no bar and publishes 0px at %ipx', (width) => {
      const { callbacks } = stubObserver(72)
      renderShell({ width })
      expect(callbacks).toHaveLength(0)
      expect(emittedCss()).toContain('--ww-shell-inset-bottom:0px;')
    })

    it('observes no bar in the minimal variant', () => {
      const { callbacks } = stubObserver(72)
      renderMinimal({ width: 360 })
      expect(callbacks).toHaveLength(0)
    })
  })
})
