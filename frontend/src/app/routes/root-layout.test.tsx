import { cleanup, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { http } from 'msw'
import { selectSessionStatus } from '@wattwise/core'
import stubMatchMedia from '@/testing/match-media'
import { server } from '@/testing/mocks/server'
import { authUrl } from '@/testing/mocks/handlers/auth'
import { problemResponse } from '@wattwise/core/testing'
import renderWithProviders from '@/testing/test-utils'
import { routes } from '../router'

beforeEach(() => {
  localStorage.clear()
  document.documentElement.className = ''
  document.documentElement.lang = ''
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderAt(path: string, width: number, session?: 'signedIn' | 'signedOut') {
  stubMatchMedia({ width })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  return {
    router,
    ...renderWithProviders(<RouterProvider router={router} />, { session }),
  }
}

describe('RootLayout', () => {
  it('shows the shell with a loading indicator while the first page loads', async () => {
    renderAt('/', 360)
    expect(screen.getByRole('navigation', { name: 'Main navigation' })).toBeTruthy()
    expect(screen.getByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeTruthy()
    expect(screen.queryByRole('progressbar')).toBeNull()
  })

  it('renders the phone shell with the Home page', async () => {
    renderAt('/', 360)
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeTruthy()
    expect(screen.getByText('Watt-Wise')).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(nav.querySelector('.MuiBottomNavigation-root')).toBeTruthy()
    expect(within(nav).queryByRole('list')).toBeNull()
    expect(within(nav).getByRole('link', { name: 'Home' }).getAttribute('aria-current')).toBe(
      'page',
    )
  })

  it('renders the side navigation from md up', async () => {
    renderAt('/', 900)
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeTruthy()
    expect(screen.getByText('Watt-Wise')).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(within(nav).getByRole('list')).toBeTruthy()
    expect(nav.querySelector('.MuiBottomNavigation-root')).toBeNull()
    expect(within(nav).getByRole('link', { name: 'Home' }).getAttribute('aria-current')).toBe(
      'page',
    )
  })

  it('moves to the Placeholder page and the current marker with a click', async () => {
    const user = userEvent.setup()
    renderAt('/', 360)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('link', { name: 'Placeholder' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Placeholder' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Placeholder' }).getAttribute('aria-current')).toBe(
      'page',
    )
    expect(screen.getByRole('link', { name: 'Home' }).getAttribute('aria-current')).toBeNull()
  })

  it('lists the About link', async () => {
    renderAt('/', 900)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(within(nav).getByRole('link', { name: 'About' })).toBeTruthy()
  })

  it('renders the Placeholder page directly', async () => {
    renderAt('/placeholder', 900)
    expect(await screen.findByRole('heading', { level: 1, name: 'Placeholder' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Placeholder' }).getAttribute('aria-current')).toBe(
      'page',
    )
  })

  it('shows Page not found inside the shell and links back home', async () => {
    const user = userEvent.setup()
    renderAt('/nope', 900)
    expect(await screen.findByRole('heading', { level: 1, name: 'Page not found' })).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    for (const link of within(nav).getAllByRole('link')) {
      expect(link.getAttribute('aria-current')).toBeNull()
    }
    await user.click(screen.getByRole('link', { name: 'Go to home' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeTruthy()
  })

  it('stores Dark and sets the class on html', async () => {
    const user = userEvent.setup()
    renderAt('/', 360)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Appearance' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Dark' }))
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    expect(localStorage.getItem('wattwise-mode')).toBe('dark')
  })

  it.each([360, 900])('switches the shell to Lithuanian at %i px', async (width) => {
    const user = userEvent.setup()
    renderAt('/', width)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Language' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Lietuvių' }))
    expect(screen.getByRole('heading', { level: 1, name: 'Pradžia' })).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Pagrindinė navigacija' })
    expect(within(nav).getByRole('link', { name: 'Pradžia' })).toBeTruthy()
    expect(within(nav).getByRole('link', { name: 'Pavyzdys' })).toBeTruthy()
    expect(within(nav).getByRole('link', { name: 'Apie' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Išvaizda' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Kalba' })).toBeTruthy()
    expect(localStorage.getItem('wattwise-language')).toBe('lt')
    expect(document.documentElement.lang).toBe('lt')
  })

  it('shows no account menu while the session is restored', async () => {
    renderAt('/', 360)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    expect(screen.queryByRole('button', { name: 'Account' })).toBeNull()
  })

  it('offers Sign in and Sign up to a signed-out visitor', async () => {
    const user = userEvent.setup()
    const { router } = renderAt('/', 360, 'signedOut')
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    const menu = screen.getByRole('menu')
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Sign in', 'Sign up'])
    await user.click(within(menu).getByRole('menuitem', { name: 'Sign up' }))
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/sign-up')
    })
    expect(screen.queryByRole('menu')).toBeNull()
  })

  it('offers Devices, Change password and Sign out to a signed-in user', async () => {
    const user = userEvent.setup()
    const { router } = renderAt('/', 900, 'signedIn')
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    const menu = screen.getByRole('menu')
    expect(
      within(menu)
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Devices', 'Change password', 'Sign out'])
    await user.click(within(menu).getByRole('menuitem', { name: 'Devices' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Devices' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/devices')
  })

  it('opens the change-password page from the menu', async () => {
    const user = userEvent.setup()
    const { router } = renderAt('/', 360, 'signedIn')
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    await user.click(screen.getByRole('menuitem', { name: 'Change password' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Change password' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/change-password')
  })

  it('signs out from the menu, lands on sign-in and then offers Sign in', async () => {
    const user = userEvent.setup()
    const { router, store } = renderAt('/', 360, 'signedIn')
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    await user.click(screen.getByRole('menuitem', { name: 'Sign out' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(router.state.historyAction).toBe('REPLACE')
    expect(selectSessionStatus(store.getState())).toBe('signedOut')

    await user.click(screen.getByRole('button', { name: 'Account' }))
    expect(
      within(screen.getByRole('menu'))
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Sign in', 'Sign up'])
  })

  it('signs out even when the request fails', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/sign-out'), () =>
        problemResponse({ status: 500, code: 'General.Unexpected' }),
      ),
    )
    const { router } = renderAt('/', 360, 'signedIn')
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    await user.click(screen.getByRole('menuitem', { name: 'Sign out' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/sign-in')
  })

  it('names the account menu in Lithuanian', async () => {
    const user = userEvent.setup()
    stubMatchMedia({ width: 360 })
    const router = createMemoryRouter(routes, { initialEntries: ['/'] })
    renderWithProviders(<RouterProvider router={router} />, { session: 'signedIn', language: 'lt' })
    await screen.findByRole('heading', { level: 1, name: 'Pradžia' })
    await user.click(screen.getByRole('button', { name: 'Paskyra' }))
    expect(
      within(screen.getByRole('menu'))
        .getAllByRole('menuitem')
        .map((item) => item.textContent),
    ).toEqual(['Įrenginiai', 'Keisti slaptažodį', 'Atsijungti'])
  })

  it('starts in Lithuanian when the browser prefers it', async () => {
    vi.spyOn(navigator, 'languages', 'get').mockReturnValue(['lt-LT', 'en'])
    renderAt('/', 360)
    await screen.findByRole('heading', { level: 1, name: 'Pradžia' })
    const nav = screen.getByRole('navigation', { name: 'Pagrindinė navigacija' })
    expect(within(nav).getByRole('link', { name: 'Pradžia' })).toBeTruthy()
  })

  it('starts in the stored language', async () => {
    localStorage.setItem('wattwise-language', 'lt')
    renderAt('/', 360)
    await screen.findByRole('heading', { level: 1, name: 'Pradžia' })
    const nav = screen.getByRole('navigation', { name: 'Pagrindinė navigacija' })
    expect(within(nav).getByRole('link', { name: 'Pradžia' })).toBeTruthy()
  })
})
