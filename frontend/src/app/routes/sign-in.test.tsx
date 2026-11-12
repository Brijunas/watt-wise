import { act, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { sessionEnded, sessionStarted } from '@wattwise/core'
import { describe, expect, it } from 'vitest'
import { server } from '@/testing/mocks/server'
import { authUrl, signedInResponse } from '@/testing/mocks/handlers/auth'
import { createGate, problemResponse } from '@wattwise/core/testing'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

const signedOutEn = { session: 'signedOut', language: 'en' } as const

const submit = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(screen.getByLabelText('Email', { selector: 'input' }), 'jonas@example.com')
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

  it('signs in and goes home', async () => {
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
    expect(body).toEqual({ email: 'jonas@example.com', password: 'whatever' })
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

  it('sends a signed-in user who opens sign-in home', async () => {
    const { router } = renderRoutesAt(routes, '/sign-in', { session: 'signedIn', language: 'en' })

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/')
    })
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

  it('shows the password-reset notice', async () => {
    renderRoutesAt(
      routes,
      { pathname: '/sign-in', state: { notice: 'passwordReset' } },
      signedOutEn,
    )

    expect(
      await screen.findByText('Your password was changed. Sign in with the new one.'),
    ).toBeTruthy()
  })

  it('links to forgot-password and sign-up', async () => {
    const user = userEvent.setup()
    const { router } = renderRoutesAt(routes, '/sign-in', signedOutEn)
    await user.click(await screen.findByRole('link', { name: 'Forgot password?' }))
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/forgot-password')
    })
    await act(() => router.navigate('/sign-in'))
    await user.click(await screen.findByRole('link', { name: 'Create an account' }))
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/sign-up')
    })
  })

  it('shows the wrong-credentials message', async () => {
    const user = userEvent.setup()
    signInFails('Accounts.InvalidCredentials')
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    expect(await screen.findByText('The email or password is wrong.')).toBeTruthy()
  })

  it('offers to send the confirmation link again when the email is not confirmed', async () => {
    const user = userEvent.setup()
    let resent: unknown
    signInFails('Accounts.EmailNotConfirmed', 403)
    server.use(
      http.post(authUrl('/resend-confirmation'), async ({ request }) => {
        resent = await request.json()
        return new HttpResponse(null, { status: 202 })
      }),
    )
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    expect(
      await screen.findByText(
        'Confirm your email address first. We sent you a link when you signed up.',
      ),
    ).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Send the link again' }))

    expect(await screen.findByText('We sent the confirmation link again.')).toBeTruthy()
    expect(resent).toEqual({ email: 'jonas@example.com' })
    expect(screen.queryByRole('button', { name: 'Send the link again' })).toBeNull()
  })

  it('shows an error when sending the link again fails', async () => {
    const user = userEvent.setup()
    signInFails('Accounts.EmailNotConfirmed', 403)
    server.use(http.post(authUrl('/resend-confirmation'), () => HttpResponse.error()))
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)
    await user.click(await screen.findByRole('button', { name: 'Send the link again' }))

    expect(
      await screen.findByText('Could not reach the server. Check your connection and try again.'),
    ).toBeTruthy()
    expect(screen.queryByText('We sent the confirmation link again.')).toBeNull()
  })

  it('shows the lockout wait in minutes, rounded up from Retry-After', async () => {
    const user = userEvent.setup()
    signInFails('Accounts.LockedOut', 423, 90)
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    expect(await screen.findByText('Too many failed attempts. Try again in 2 min.')).toBeTruthy()
  })

  it('shows the lockout message without a time when Retry-After is missing', async () => {
    const user = userEvent.setup()
    signInFails('Accounts.LockedOut', 423)
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    expect(await screen.findByText('Too many failed attempts. Try again later.')).toBeTruthy()
  })

  it('shows the lockout in Lithuanian', async () => {
    const user = userEvent.setup()
    signInFails('Accounts.LockedOut', 423, 90)
    renderRoutesAt(routes, '/sign-in', { session: 'signedOut', language: 'lt' })
    await screen.findByRole('heading', { level: 1, name: 'Prisijungimas' })
    await user.type(screen.getByLabelText('El. paštas', { selector: 'input' }), 'a@b.test')
    await user.type(screen.getByLabelText('Slaptažodis', { selector: 'input' }), 'x')
    await user.click(screen.getByRole('button', { name: 'Prisijungti' }))

    expect(
      await screen.findByText('Per daug nesėkmingų bandymų. Bandykite dar kartą po 2 min.'),
    ).toBeTruthy()
  })

  it('shows the network message', async () => {
    const user = userEvent.setup()
    server.use(http.post(authUrl('/sign-in'), () => HttpResponse.error()))
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    expect(
      await screen.findByText('Could not reach the server. Check your connection and try again.'),
    ).toBeTruthy()
  })

  it('shows the generic message for an unknown error', async () => {
    const user = userEvent.setup()
    signInFails('Odd.Thing', 500)
    renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)

    expect(await screen.findByText('Something went wrong. Try again.')).toBeTruthy()
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

    expect(
      await screen
        .findByRole('button', { name: /Sign in/ })
        .then((b) => b.hasAttribute('disabled')),
    ).toBe(true)
    gate.open()
  })

  it('changes nothing when the answer arrives after the user left the page', async () => {
    const user = userEvent.setup()
    const gate = createGate()
    server.use(
      http.post(authUrl('/sign-in'), async () => {
        await gate.wait
        return problemResponse({ status: 401, code: 'Accounts.InvalidCredentials' })
      }),
    )
    const { router } = renderRoutesAt(routes, '/sign-in', signedOutEn)
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)
    await act(() => router.navigate('/about'))
    await screen.findByRole('heading', { level: 1, name: 'About' })
    gate.open()
    await new Promise((resolve) => setTimeout(resolve, 50))

    expect(router.state.location.pathname).toBe('/about')
    expect(screen.queryByText('The email or password is wrong.')).toBeNull()
  })

  it('does not navigate away from the page the user moved to after a successful late answer', async () => {
    const user = userEvent.setup()
    const gate = createGate()
    server.use(
      http.post(authUrl('/sign-in'), async () => {
        await gate.wait
        return HttpResponse.json(signedInResponse)
      }),
    )
    const { router } = renderRoutesAt(
      routes,
      { pathname: '/sign-in', state: { from: { pathname: '/change-password' } } },
      signedOutEn,
    )
    await screen.findByRole('heading', { level: 1, name: 'Sign in' })
    await submit(user)
    await act(() => router.navigate('/about'))
    await screen.findByRole('heading', { level: 1, name: 'About' })
    gate.open()
    await new Promise((resolve) => setTimeout(resolve, 50))

    expect(router.state.location.pathname).toBe('/about')
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

  it('goes home once the restore ends signed in', async () => {
    const { store, router } = renderRoutesAt(routes, '/sign-in', { language: 'en' })
    await screen.findByRole('progressbar', { name: 'Loading' })
    act(() => {
      store.dispatch(sessionStarted({ accessToken: 't' }))
    })

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/')
    })
    expect(screen.queryByRole('button', { name: 'Sign in' })).toBeNull()
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

describe('Protected routes', () => {
  it('sends a signed-out visitor to sign-in and back after signing in', async () => {
    const user = userEvent.setup()
    const { router } = renderRoutesAt(routes, '/change-password', signedOutEn)

    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(router.state.location.search).toBe('')
    await submit(user)

    expect(await screen.findByRole('heading', { level: 1, name: 'Change password' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/change-password')
  })

  it('does not bounce while the session is restoring, and shows the page once it is signed in', async () => {
    const { router, store } = renderRoutesAt(routes, '/change-password', { language: 'en' })

    expect(await screen.findByRole('progressbar', { name: 'Loading' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/change-password')
    act(() => {
      store.dispatch(sessionStarted({ accessToken: 'access-token' }))
    })

    expect(await screen.findByRole('heading', { level: 1, name: 'Change password' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/change-password')
  })
})
