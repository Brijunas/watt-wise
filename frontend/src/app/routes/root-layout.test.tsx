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
    expect(screen.getByRole('link', { name: 'Watt-Wise home' }).getAttribute('href')).toBe('/')
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(nav.querySelector('.MuiBottomNavigation-root')).toBeTruthy()
    expect(within(nav).queryByRole('list')).toBeNull()
    expect(within(nav).getByRole('link', { name: 'Home' }).getAttribute('aria-current')).toBe(
      'page',
    )
  })

  it.each([600, 900, 1199])('renders the rail at %i px', async (width) => {
    renderAt('/', width)
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Watt-Wise home' }).getAttribute('href')).toBe('/')
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(nav.querySelector('.MuiBottomNavigation-root')).toBeNull()
    expect(nav.closest('.MuiDrawer-root')?.className).toContain('-WwAppShell-rail')
    expect(within(nav).getByRole('link', { name: 'Home' }).getAttribute('aria-current')).toBe(
      'page',
    )
  })

  it.each([1200, 1366])('renders the drawer at %i px', async (width) => {
    renderAt('/', width)
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(screen.getAllByRole('navigation')).toHaveLength(1)
    expect(nav.closest('.MuiDrawer-root')?.className).toContain('-WwAppShell-drawer')
  })

  it('keeps the full shell for the About page and marks it current', async () => {
    renderAt('/about', 900)
    expect(await screen.findByRole('heading', { level: 1, name: 'About' })).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Main navigation' })
    expect(within(nav).getByRole('link', { name: 'About' }).getAttribute('aria-current')).toBe(
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

  it.each([
    { language: 'en', name: 'Glow effects' },
    { language: 'lt', name: 'Švytėjimas (efektai)' },
  ])('shows the glow switch in the app bar in $language', async ({ language, name }) => {
    localStorage.setItem('wattwise-language', language)
    renderAt('/', 360)
    await screen.findByRole('heading', { level: 1 })
    expect(screen.getByRole('switch', { name, checked: true })).toBeTruthy()
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

  // Below 600px the app bar has no language button: the account menu holds the language.
  it('switches the shell to Lithuanian from the account menu at 360 px', async () => {
    const user = userEvent.setup()
    renderAt('/', 360)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    expect(screen.queryByRole('button', { name: 'Language: EN' })).toBeNull()
    await user.click(screen.getByRole('button', { name: 'Account' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Lietuvių' }))
    expect(screen.getByRole('heading', { level: 1, name: 'Pradžia' })).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Pagrindinė navigacija' })
    expect(within(nav).getByRole('link', { name: 'Pradžia' })).toBeTruthy()
    expect(within(nav).getByRole('link', { name: 'Pavyzdys' })).toBeTruthy()
    expect(within(nav).getByRole('link', { name: 'Apie' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Išvaizda' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Paskyra' })).toBeTruthy()
    expect(localStorage.getItem('wattwise-language')).toBe('lt')
    expect(document.documentElement.lang).toBe('lt')
  })

  it.each([600, 900])(
    'switches the shell to Lithuanian from the language button at %i px',
    async (width) => {
      const user = userEvent.setup()
      renderAt('/', width)
      await screen.findByRole('heading', { level: 1, name: 'Home' })
      await user.click(screen.getByRole('button', { name: 'Language: EN' }))
      await user.click(screen.getByRole('menuitemradio', { name: 'Lietuvių' }))
      expect(screen.getByRole('heading', { level: 1, name: 'Pradžia' })).toBeTruthy()
      const nav = screen.getByRole('navigation', { name: 'Pagrindinė navigacija' })
      expect(within(nav).getByRole('link', { name: 'Pradžia' })).toBeTruthy()
      expect(screen.getByRole('button', { name: 'Išvaizda' })).toBeTruthy()
      expect(screen.getByRole('button', { name: 'Kalba: LT' })).toBeTruthy()
      expect(localStorage.getItem('wattwise-language')).toBe('lt')
      expect(document.documentElement.lang).toBe('lt')
    },
  )

  it('shows only the language in the account menu at 360 px while the session is restored', async () => {
    const user = userEvent.setup()
    renderAt('/', 360)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    const menu = screen.getByRole('menu')
    expect(within(menu).queryAllByRole('menuitem')).toHaveLength(0)
    expect(
      within(menu)
        .getAllByRole('menuitemradio')
        .map((item) => item.textContent),
    ).toEqual(['English', 'Lietuvių'])
  })

  it('shows no account menu at 900 px while the session is restored', async () => {
    renderAt('/', 900)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    expect(screen.queryByRole('button', { name: 'Account' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Language: EN' })).toBeTruthy()
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

  it('offers Sign in and Sign up to a signed-out visitor at 900 px, with no language', async () => {
    const user = userEvent.setup()
    renderAt('/', 900, 'signedOut')
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    expect(screen.queryAllByRole('menuitemradio')).toHaveLength(0)
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([
      'Sign in',
      'Sign up',
    ])
  })

  it.each([360, 900])(
    'offers Settings and Sign out to a signed-in user at %i px',
    async (width) => {
      const user = userEvent.setup()
      const { router } = renderAt('/', width, 'signedIn')
      await screen.findByRole('heading', { level: 1, name: 'Home' })
      await user.click(screen.getByRole('button', { name: 'Account' }))
      const menu = screen.getByRole('menu')
      expect(
        within(menu)
          .getAllByRole('menuitem')
          .map((item) => item.textContent),
      ).toEqual(['Settings', 'Sign out'])
      await user.click(within(menu).getByRole('menuitem', { name: 'Settings' }))
      expect(await screen.findByRole('heading', { level: 1, name: 'Settings' })).toBeTruthy()
      expect(router.state.location.pathname).toBe('/settings')
    },
  )

  it('no longer lists Devices or Change password in the account menu', async () => {
    const user = userEvent.setup()
    renderAt('/', 900, 'signedIn')
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    expect(screen.queryAllByRole('menuitem', { name: /Devices|Change password/ })).toHaveLength(0)
  })

  it('signs out from the menu and lands on the sign-in page without an account menu', async () => {
    const user = userEvent.setup()
    const { router, store } = renderAt('/', 360, 'signedIn')
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    await user.click(screen.getByRole('menuitem', { name: 'Sign out' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(router.state.historyAction).toBe('REPLACE')
    expect(selectSessionStatus(store.getState())).toBe('signedOut')

    // The sign-in page has the minimal app bar: the language button, no account menu.
    expect(screen.queryByRole('button', { name: 'Account' })).toBeNull()
    expect(screen.getByRole('button', { name: 'Language: EN' })).toBeTruthy()
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
    ).toEqual(['Nustatymai', 'Atsijungti'])
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
