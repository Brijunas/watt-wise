import { act, screen } from '@testing-library/react'
import { sessionEnded, sessionStarted } from '@wattwise/core'
import { createMemoryRouter, useLocation } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { describe, expect, it } from 'vitest'
import renderWithProviders from '@/testing/test-utils'
import ProtectedLayout from './protected-layout'

const Child = () => <p>Secret page</p>

const SignInProbe = () => {
  const { state } = useLocation() as { state: { from?: unknown } | null }
  return <pre data-testid="state">{JSON.stringify(state?.from)}</pre>
}

const renderGuard = (options: Parameters<typeof renderWithProviders>[1]) => {
  const router = createMemoryRouter(
    [
      { Component: ProtectedLayout, children: [{ path: '/devices', Component: Child }] },
      { path: '/sign-in', Component: SignInProbe },
    ],
    { initialEntries: ['/devices?tab=2#top'] },
  )
  return { router, ...renderWithProviders(<RouterProvider router={router} />, options) }
}

describe('ProtectedLayout', () => {
  it('shows the loader and does not redirect while the session is restoring', () => {
    const { router } = renderGuard({})

    expect(screen.getByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    expect(screen.queryByText('Secret page')).toBeNull()
    expect(router.state.location.pathname).toBe('/devices')
  })

  it('shows the page once the session is restored as signed in', () => {
    const { store, router } = renderGuard({})

    act(() => {
      store.dispatch(sessionStarted({ accessToken: 't' }))
    })

    expect(screen.getByText('Secret page')).toBeTruthy()
    expect(screen.queryByRole('progressbar')).toBeNull()
    expect(router.state.location.pathname).toBe('/devices')
  })

  it('redirects to sign-in once the restore ends signed out', () => {
    const { store, router } = renderGuard({})

    act(() => {
      store.dispatch(sessionEnded())
    })

    expect(router.state.location.pathname).toBe('/sign-in')
    expect(screen.queryByText('Secret page')).toBeNull()
  })

  it('redirects a signed-out visitor with the requested location in the state', () => {
    const { router } = renderGuard({ session: 'signedOut' })

    expect(router.state.location.pathname).toBe('/sign-in')
    expect(router.state.location.search).toBe('')
    expect(JSON.parse(screen.getByTestId('state').textContent) as unknown).toMatchObject({
      pathname: '/devices',
      search: '?tab=2',
      hash: '#top',
    })
  })

  it('replaces the entry, so back does not return to the guarded page', () => {
    const { router } = renderGuard({ session: 'signedOut' })

    expect(router.state.historyAction).toBe('REPLACE')
  })

  it('renders the child for a signed-in session', () => {
    renderGuard({ session: 'signedIn' })

    expect(screen.getByText('Secret page')).toBeTruthy()
  })
})
