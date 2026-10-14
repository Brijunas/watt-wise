import HomeIcon from '@mui/icons-material/Home'
import InboxIcon from '@mui/icons-material/Inbox'
import { createI18n } from '@wattwise/i18n'
import { cleanup, render, screen, within } from '@testing-library/react'
import type { AnchorHTMLAttributes } from 'react'
import { I18nextProvider } from 'react-i18next'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AppThemeProvider from '../theme/app-theme-provider'
import createMatchMedia from '../testing/create-match-media'
import AppShell from './app-shell'
import type { AppShellNavItem } from './app-shell-nav-item'

function TestLink({ to, ...rest }: { to: string } & AnchorHTMLAttributes<HTMLAnchorElement>) {
  return <a href={to} {...rest} />
}

const items: readonly AppShellNavItem[] = [
  { key: 'home', label: 'Home page', icon: <HomeIcon />, to: '/' },
  { key: 'inbox', label: 'Inbox page', icon: <InboxIcon />, to: '/inbox' },
]

beforeEach(() => {
  localStorage.clear()
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderShell({
  width,
  activeKey = 'home',
  lng,
}: {
  width: number
  activeKey?: string | null
  lng?: 'en' | 'lt'
}) {
  vi.stubGlobal('matchMedia', createMatchMedia({ width }))
  render(
    <I18nextProvider i18n={createI18n({ lng })}>
      <AppThemeProvider>
        <AppShell
          title="Watt-Wise"
          items={items}
          activeKey={activeKey}
          linkComponent={TestLink}
          actions={<button type="button">Action</button>}
        >
          <p>Page content</p>
        </AppShell>
      </AppThemeProvider>
    </I18nextProvider>,
  )
}

describe('AppShell', () => {
  it('shows the bottom navigation on a phone', () => {
    renderShell({ width: 360 })
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(nav.querySelector('.MuiBottomNavigation-root')).toBeTruthy()
    expect(within(nav).queryByRole('list')).toBeNull()
  })

  it('shows the side list from md up', () => {
    renderShell({ width: 900 })
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(within(nav).getByRole('list')).toBeTruthy()
    expect(nav.querySelector('.MuiBottomNavigation-root')).toBeNull()
  })

  it.each([360, 900])('links every item and marks the active one at %i px', (width) => {
    renderShell({ width, activeKey: 'inbox' })
    const home = screen.getByRole('link', { name: 'Home page' })
    const inbox = screen.getByRole('link', { name: 'Inbox page' })
    expect(home.getAttribute('href')).toBe('/')
    expect(inbox.getAttribute('href')).toBe('/inbox')
    expect(inbox.getAttribute('aria-current')).toBe('page')
    expect(home.getAttribute('aria-current')).toBeNull()
  })

  it.each([360, 900])('marks no item active without an active key at %i px', (width) => {
    renderShell({ width, activeKey: null })
    for (const link of screen.getAllByRole('link')) {
      expect(link.getAttribute('aria-current')).toBeNull()
    }
  })

  it.each([360, 900])('renders the title, actions and children at %i px', (width) => {
    renderShell({ width })
    expect(screen.getByText('Watt-Wise')).toBeTruthy()
    expect(screen.queryByRole('heading')).toBeNull()
    expect(screen.getByRole('button', { name: 'Action' })).toBeTruthy()
    expect(within(screen.getByRole('main')).getByText('Page content')).toBeTruthy()
  })

  it('names the landmark in Lithuanian', () => {
    renderShell({ width: 360, lng: 'lt' })
    expect(screen.getByRole('navigation', { name: 'Pagrindinė navigacija' })).toBeTruthy()
  })
})
