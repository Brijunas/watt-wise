import { act, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import {
  selectAccessToken,
  selectSessionStatus,
  sessionEnded,
  sessionStarted,
} from '@wattwise/core'
import { createGate, problemResponse } from '@wattwise/core/testing'
import { describe, expect, it } from 'vitest'
import { authUrl, signedInResponse } from '@/testing/mocks/handlers/auth'
import { server } from '@/testing/mocks/server'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

const signedOutEn = { session: 'signedOut', language: 'en' } as const
const tick = () => new Promise((resolve) => setTimeout(resolve, 50))

const submit = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(screen.getByLabelText('Email', { selector: 'input' }), 'admin@example.com')
  await user.type(screen.getByLabelText('Password', { selector: 'input' }), 'whatever')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
}

const signInFails = (code: string, status = 401, retryAfterSeconds?: number) => {
  server.use(
    http.post(authUrl('/sign-in'), () => problemResponse({ status, code, retryAfterSeconds })),
  )
}

describe('Sign-in route', () => {
  it.each([
    [360, 'en', 'Sign in', 'Sign in'],
    [900, 'en', 'Sign in', 'Sign in'],
    [360, 'lt', 'Prisijungimas', 'Prisijungti'],
    [900, 'lt', 'Prisijungimas', 'Prisijungti'],
  ] as const)('renders at %i px in %s', async (width, language, heading, button) => {
    renderRoutesAt(routes, '/sign-in', { width, language, session: 'signedOut' })

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeTruthy()
    expect(screen.getByRole('button', { name: button })).toBeTruthy()
  })

  it('posts the email and password and goes home', async () => {
    const user = userEvent.setup()
    let body: unknown
    server.use(
      http.post(authUrl('/sign-in'), async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(signedInResponse)
      }),
    )
    const { router } = renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/')
    })
    expect(body).toEqual({ email: 'admin@example.com', password: 'whatever' })
    expect(await screen.findByRole('heading', { level: 1, name: 'Home' })).toBeTruthy()
  })

  it('returns to the page the user came from, with its query and hash', async () => {
    const user = userEvent.setup()
    const from = { pathname: '/about', search: '?a=1', hash: '#top' }
    const { router } = renderRoutesAt(
      routes,
      { pathname: '/sign-in', state: { from } },
      signedOutEn,
    )
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/about')
    })
    expect(router.state.location.search).toBe('?a=1')
    expect(router.state.location.hash).toBe('#top')
    expect(router.state.historyAction).toBe('REPLACE')
  })

  it.each([['//evil.example'], ['https://evil.example'], ['evil']])(
    'ignores the return-to %s and goes home',
    async (pathname) => {
      const user = userEvent.setup()
      const { router } = renderRoutesAt(
        routes,
        { pathname: '/sign-in', state: { from: { pathname } } },
        signedOutEn,
      )
      await screen.findByRole('heading', { level: 1, name: 'Sign in' })
      await submit(user)

      await waitFor(() => {
        expect(router.state.location.pathname).toBe('/')
      })
    },
  )

  it('sends a signed-in user who opens sign-in home, replacing the entry', async () => {
    const { router } = renderRoutesAt(routes, '/sign-in', { session: 'signedIn', language: 'en' })

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/')
    })
    expect(router.state.historyAction).toBe('REPLACE')
  })

  it('sends a signed-in user to the page in the state', async () => {
    const { router } = renderRoutesAt(
      routes,
      { pathname: '/sign-in', state: { from: { pathname: '/about' } } },
      { session: 'signedIn', language: 'en' },
    )

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/about')
    })
  })

  it('has no sign-up link and links forgot-password to the frontend app', async () => {
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    const link = await screen.findByRole('link', { name: 'Forgot password?' })

    expect(link.getAttribute('href')).toBe('https://app.test/forgot-password')
    expect(screen.queryByRole('link', { name: 'Create an account' })).toBeNull()
  })

  it('shows the wrong-credentials message', async () => {
    const user = userEvent.setup()
    signInFails('Accounts.InvalidCredentials')
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    expect(await screen.findByText('The email or password is wrong.')).toBeTruthy()
  })

  it('shows the lockout wait in minutes, rounded up from Retry-After', async () => {
    const user = userEvent.setup()
    signInFails('Accounts.LockedOut', 423, 90)
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    expect(await screen.findByText('Too many failed attempts. Try again in 2 min.')).toBeTruthy()
  })

  it('disables the button while the answer is pending', async () => {
    const user = userEvent.setup()
    const gate = createGate()
    server.use(
      http.post(authUrl('/sign-in'), async () => {
        await gate.wait
        return HttpResponse.json(signedInResponse)
      }),
    )
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Sign in/ }).hasAttribute('disabled')).toBe(true)
    })
    gate.open()
  })

  it('ignores a sign-in answer that arrives after the session ended', async () => {
    const user = userEvent.setup()
    const gate = createGate()
    let answered = false
    server.use(
      http.post(authUrl('/sign-in'), async () => {
        await gate.wait
        answered = true
        return HttpResponse.json(signedInResponse)
      }),
    )
    const { store, router } = renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)
    act(() => {
      store.dispatch(sessionEnded())
    })
    gate.open()
    await waitFor(() => {
      expect(answered).toBe(true)
    })
    await tick()

    expect(selectSessionStatus(store.getState())).toBe('signedOut')
    expect(selectAccessToken(store.getState())).toBeNull()
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeTruthy()
  })

  it('shows no error when a failed answer arrives after the user moved to no-access', async () => {
    const user = userEvent.setup()
    const gate = createGate()
    let answered = false
    server.use(
      http.post(authUrl('/sign-in'), async () => {
        await gate.wait
        answered = true
        return problemResponse({ status: 401, code: 'Accounts.InvalidCredentials' })
      }),
    )
    const { router } = renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)
    await act(() => router.navigate('/no-access'))
    await screen.findByRole('heading', { level: 1, name: 'No access' })
    gate.open()
    await waitFor(() => {
      expect(answered).toBe(true)
    })
    await tick()

    expect(router.state.location.pathname).toBe('/no-access')
    expect(screen.queryByText('The email or password is wrong.')).toBeNull()
    expect(screen.queryByRole('alert')).toBeNull()
  })
})

describe('Sign-in route while the session restores', () => {
  it('shows the loader and no form, then the form once the restore ends signed out', async () => {
    const { store } = renderRoutesAt(routes, '/sign-in', { language: 'en' })

    expect(await screen.findByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Sign in' })).toBeNull()
    act(() => {
      store.dispatch(sessionEnded())
    })

    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeTruthy()
    expect(screen.queryByRole('progressbar')).toBeNull()
  })

  it('goes to the page in the state once the restore ends signed in', async () => {
    const { store, router } = renderRoutesAt(
      routes,
      { pathname: '/sign-in', state: { from: { pathname: '/about' } } },
      { language: 'en' },
    )
    await screen.findByRole('progressbar', { name: 'Loading' })
    act(() => {
      store.dispatch(sessionStarted({ accessToken: 't' }))
    })

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/about')
    })
  })
})
