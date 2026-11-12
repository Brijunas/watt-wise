import { act, screen } from '@testing-library/react'
import { sessionEnded } from '@wattwise/core'
import { describe, expect, it } from 'vitest'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

describe('No-access route', () => {
  it.each([
    [360, 'en', 'No access', 'Sign in'],
    [900, 'en', 'No access', 'Sign in'],
    [360, 'lt', 'Nėra prieigos', 'Prisijungti'],
    [900, 'lt', 'Nėra prieigos', 'Prisijungti'],
  ] as const)('renders at %i px in %s', async (width, language, heading, action) => {
    renderRoutesAt(routes, '/no-access', { width, language, session: 'signedOut' })

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeTruthy()
    expect(await screen.findByRole('link', { name: action })).toBeTruthy()
  })

  it('shows the text and a Sign in link to the sign-in page', async () => {
    const { router } = renderRoutesAt(routes, '/no-access', {
      session: 'signedOut',
      language: 'en',
    })

    expect(
      await screen.findByText(
        "This account can't use the admin app, so you were signed out. Sign in with an admin account.",
      ),
    ).toBeTruthy()
    const link = screen.getByRole('link', { name: 'Sign in' })
    expect(link.getAttribute('href')).toBe('/sign-in')
    expect(router.state.location.pathname).toBe('/no-access')
  })

  it('hides the Sign in link while the session is still signed in, and shows it once signed out', async () => {
    const { store, router } = renderRoutesAt(routes, '/no-access', {
      session: 'signedIn',
      language: 'en',
    })

    expect(await screen.findByRole('heading', { level: 1, name: 'No access' })).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'Sign in' })).toBeNull()
    act(() => {
      store.dispatch(sessionEnded())
    })

    expect(await screen.findByRole('link', { name: 'Sign in' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/no-access')
  })
})
