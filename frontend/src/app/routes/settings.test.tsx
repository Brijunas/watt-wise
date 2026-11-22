import { act, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { sessionEnded, sessionStarted } from '@wattwise/core'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

afterEach(() => {
  vi.unstubAllGlobals()
})

const text = {
  en: { title: 'Settings', changePassword: 'Change password', devices: 'Devices' },
  lt: { title: 'Nustatymai', changePassword: 'Keisti slaptažodį', devices: 'Įrenginiai' },
} as const

describe.each(['en', 'lt'] as const)('Settings page in %s', (language) => {
  const t = text[language]

  it.each([360, 900])('shows the title and the two links at %i px', async (width) => {
    renderRoutesAt(routes, '/settings', { width, language, session: 'signedIn' })
    expect(await screen.findByRole('heading', { level: 1, name: t.title })).toBeTruthy()
    const main = screen.getByRole('main')
    expect(
      within(main)
        .getAllByRole('link')
        .map((link) => [link.textContent, link.getAttribute('href')]),
    ).toEqual([
      [t.changePassword, '/change-password'],
      [t.devices, '/devices'],
    ])
  })

  it('opens the devices page from its link', async () => {
    const user = userEvent.setup()
    const { router } = renderRoutesAt(routes, '/settings', { language, session: 'signedIn' })
    await screen.findByRole('heading', { level: 1, name: t.title })
    await user.click(within(screen.getByRole('main')).getByRole('link', { name: t.devices }))
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/devices')
    })
  })

  // The page itself (its `main`); the shell around it is checked in ww-components and app-shell.
  it.each([360, 900])('has no accessibility violations at %i px', async (width) => {
    renderRoutesAt(routes, '/settings', { width, language, session: 'signedIn' })
    await screen.findByRole('heading', { level: 1, name: t.title })
    expect((await axe(screen.getByRole('main'))).violations).toEqual([])
  })

  it('redirects a signed-out visitor to sign-in', async () => {
    const { router } = renderRoutesAt(routes, '/settings', { language, session: 'signedOut' })
    await screen.findByRole('heading', { level: 1 })
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(screen.queryAllByRole('link', { name: t.devices })).toHaveLength(0)
  })

  it('shows a loader and no links while the session is restored, then the page', async () => {
    const { store, router } = renderRoutesAt(routes, '/settings', { language })
    expect(await screen.findByRole('progressbar')).toBeTruthy()
    expect(screen.queryAllByRole('link', { name: t.devices })).toHaveLength(0)
    expect(router.state.location.pathname).toBe('/settings')
    act(() => {
      store.dispatch(sessionStarted({ accessToken: 'access-token' }))
    })
    expect(await screen.findByRole('heading', { level: 1, name: t.title })).toBeTruthy()
  })

  it('leaves for sign-in when the session ends while it is open', async () => {
    const { store, router } = renderRoutesAt(routes, '/settings', { language, session: 'signedIn' })
    await screen.findByRole('heading', { level: 1, name: t.title })
    act(() => {
      store.dispatch(sessionEnded())
    })
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/sign-in')
    })
  })
})
