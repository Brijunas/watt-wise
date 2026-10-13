import { cleanup, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { stubMatchMedia } from '@/testing/match-media'
import { renderWithProviders } from '@/testing/test-utils'
import { routes } from '../router'

beforeEach(() => {
  localStorage.clear()
  document.documentElement.className = ''
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderAt(path: string, width = 360) {
  stubMatchMedia({ width })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  return renderWithProviders(<RouterProvider router={router} />)
}

describe('RootLayout', () => {
  it('shows the phone shell with the Home page at 360 px', async () => {
    renderAt('/', 360)
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeTruthy()
    expect(screen.getByText('Watt-Wise Admin')).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(nav.querySelector('.MuiBottomNavigation-root')).toBeTruthy()
    expect(within(nav).queryByRole('list')).toBeNull()
    expect(within(nav).getByRole('link', { name: 'Home' }).getAttribute('aria-current')).toBe(
      'page',
    )
  })

  it('shows the side navigation with the Home page at 900 px', async () => {
    renderAt('/', 900)
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeTruthy()
    expect(screen.getByText('Watt-Wise Admin')).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(within(nav).getByRole('list')).toBeTruthy()
    expect(nav.querySelector('.MuiBottomNavigation-root')).toBeNull()
    expect(within(nav).getByRole('link', { name: 'Home' }).getAttribute('aria-current')).toBe(
      'page',
    )
  })

  it('opens Catalog review from the navigation and moves the current item', async () => {
    const user = userEvent.setup()
    renderAt('/')
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('link', { name: 'Catalog review' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Catalog review' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Catalog review' }).getAttribute('aria-current')).toBe(
      'page',
    )
    expect(screen.getByRole('link', { name: 'Home' }).getAttribute('aria-current')).toBeNull()
  })

  it('renders Catalog review directly at its URL', async () => {
    renderAt('/catalog-review')
    expect(await screen.findByRole('heading', { level: 1, name: 'Catalog review' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Catalog review' }).getAttribute('aria-current')).toBe(
      'page',
    )
  })

  it('shows Page not found inside the shell and goes home', async () => {
    const user = userEvent.setup()
    renderAt('/nope')
    expect(await screen.findByRole('heading', { level: 1, name: 'Page not found' })).toBeTruthy()
    expect(screen.getByRole('navigation', { name: 'Main navigation' })).toBeTruthy()
    for (const link of within(screen.getByRole('navigation')).getAllByRole('link')) {
      expect(link.getAttribute('aria-current')).toBeNull()
    }
    await user.click(screen.getByRole('link', { name: 'Go to home' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeTruthy()
  })

  it('stores Dark and sets the class on html', async () => {
    const user = userEvent.setup()
    renderAt('/')
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Appearance' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Dark' }))
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(localStorage.getItem('wattwise-mode')).toBe('dark')
  })
})
