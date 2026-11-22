import { act, cleanup, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { sessionEnded } from '@wattwise/core'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { renderRoutesAt } from '@/testing/test-utils'
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

function renderAt(
  path: string,
  width: number,
  session: 'signedIn' | 'signedOut' | 'restoring' = 'signedIn',
) {
  return renderRoutesAt(routes, path, {
    width,
    ...(session === 'restoring' ? {} : { session }),
  })
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
    expect(nav.closest('.MuiDrawer-root')?.className).toContain('-WwAppShell-drawer')
  })

  it.each([
    { language: 'en', logo: 'Watt-Wise home' },
    { language: 'lt', logo: 'Watt-Wise pradžia' },
  ] as const)(
    'shows the Admin chip after the logo in $language on the full bar',
    async ({ language, logo: logoName }) => {
      localStorage.setItem('wattwise-language', language)
      renderAt('/', 360)
      await screen.findByRole('heading', { level: 1 })
      const logo = screen.getByRole('link', { name: logoName })
      const chipElement = within(screen.getByRole('banner')).getByText('Admin')
      expect(
        logo.compareDocumentPosition(chipElement) & Node.DOCUMENT_POSITION_FOLLOWING,
      ).toBeTruthy()
    },
  )

  it('moves to the Catalog review page and the current marker with a click', async () => {
    const user = userEvent.setup()
    renderAt('/', 360)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('link', { name: 'Catalog review' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Catalog review' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Catalog review' }).getAttribute('aria-current')).toBe(
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

  it('renders the Catalog review page directly', async () => {
    renderAt('/catalog-review', 900)
    expect(await screen.findByRole('heading', { level: 1, name: 'Catalog review' })).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Catalog review' }).getAttribute('aria-current')).toBe(
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
    expect(within(nav).getByRole('link', { name: 'Katalogo peržiūra' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Išvaizda' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Paskyra' })).toBeTruthy()
    expect(localStorage.getItem('wattwise-language')).toBe('lt')
    expect(document.documentElement.lang).toBe('lt')
  })

  it('switches the shell to Lithuanian from the language button at 900 px', async () => {
    const user = userEvent.setup()
    renderAt('/', 900)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Language: EN' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Lietuvių' }))
    expect(screen.getByRole('heading', { level: 1, name: 'Pradžia' })).toBeTruthy()
    const nav = screen.getByRole('navigation', { name: 'Pagrindinė navigacija' })
    expect(within(nav).getByRole('link', { name: 'Katalogo peržiūra' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Kalba: LT' })).toBeTruthy()
    expect(localStorage.getItem('wattwise-language')).toBe('lt')
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

  it.each([
    { language: 'en', account: 'Account', signOut: 'Sign out' },
    { language: 'lt', account: 'Paskyra', signOut: 'Atsijungti' },
  ] as const)(
    'offers only $signOut in the account menu at 900 px in $language, with no language',
    async ({ language, account, signOut }) => {
      localStorage.setItem('wattwise-language', language)
      const user = userEvent.setup()
      renderAt('/', 900)
      await screen.findByRole('heading', { level: 1 })
      await user.click(screen.getByRole('button', { name: account }))
      expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([signOut])
      expect(screen.queryAllByRole('menuitemradio')).toHaveLength(0)
    },
  )

  it('offers Sign out and the language in the account menu at 360 px', async () => {
    const user = userEvent.setup()
    renderAt('/', 360)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual(['Sign out'])
    expect(screen.getAllByRole('menuitemradio').map((item) => item.textContent)).toEqual([
      'English',
      'Lietuvių',
    ])
  })

  // The admin guard shows the loader inside the full shell while the session restores.
  it('offers only the two languages at 360 px while the session restores', async () => {
    const user = userEvent.setup()
    renderAt('/', 360, 'restoring')
    await screen.findByRole('progressbar', { name: 'Loading' })
    await user.click(screen.getByRole('button', { name: 'Account' }))
    expect(screen.queryAllByRole('menuitem')).toHaveLength(0)
    expect(screen.getAllByRole('menuitemradio').map((item) => item.textContent)).toEqual([
      'English',
      'Lietuvių',
    ])
  })

  it('shows no account menu at 900 px while the session restores', async () => {
    renderAt('/', 900, 'restoring')
    await screen.findByRole('progressbar', { name: 'Loading' })
    expect(screen.queryByRole('button', { name: 'Account' })).toBeNull()
  })

  it('shows no account menu on the sign-in page while signed out', async () => {
    renderAt('/sign-in', 360, 'signedOut')
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    expect(screen.queryByRole('button', { name: 'Account' })).toBeNull()
  })

  it('shows no account menu on the sign-in page while the session restores', async () => {
    renderAt('/sign-in', 360, 'restoring')
    await screen.findByRole('progressbar', { name: 'Loading' })
    expect(screen.queryByRole('button', { name: 'Account' })).toBeNull()
  })

  it('shows no Sign out item at 900 px once the session has ended', async () => {
    const { store } = renderAt('/', 900)
    await screen.findByRole('heading', { level: 1, name: 'Home' })
    act(() => {
      store.dispatch(sessionEnded())
    })
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    expect(screen.queryByRole('button', { name: 'Account' })).toBeNull()
  })
})
