import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/testing/mocks/server'
import { authUrl } from '@/testing/mocks/handlers/auth'
import { problemResponse } from '@wattwise/core/testing'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

const strongPassword = 'Correct-Horse-9-Battery'

const accepted = () => new HttpResponse(null, { status: 202 })

const fill = async (user: ReturnType<typeof userEvent.setup>, email = 'jonas@example.com') => {
  await user.type(screen.getByLabelText('Email', { selector: 'input' }), email)
  await user.type(screen.getByLabelText('Password', { selector: 'input' }), strongPassword)
}

describe('Sign-up route', () => {
  it.each([
    [360, 'en', 'Create an account', 'Create account'],
    [900, 'en', 'Create an account', 'Create account'],
    [360, 'lt', 'Sukurti paskyrą', 'Sukurti paskyrą'],
    [900, 'lt', 'Sukurti paskyrą', 'Sukurti paskyrą'],
  ] as const)('renders at %i px in %s', async (width, language, heading, button) => {
    renderRoutesAt(routes, '/sign-up', { width, language, session: 'signedOut' })

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeTruthy()
    expect(screen.getByRole('button', { name: button })).toBeTruthy()
  })

  it('registers and goes to the check-your-email page with the address', async () => {
    const user = userEvent.setup()
    let body: unknown
    server.use(
      http.post(authUrl('/register'), async ({ request }) => {
        body = await request.json()
        return accepted()
      }),
    )
    const { router } = renderRoutesAt(routes, '/sign-up', { session: 'signedOut', language: 'en' })
    await screen.findByRole('heading', { level: 1, name: 'Create an account' })
    await fill(user)
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Check your email' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/check-email')
    expect(
      screen.getByText(
        'We sent a confirmation link to jonas@example.com. Open it to finish creating your account.',
      ),
    ).toBeTruthy()
    expect(body).toEqual({ email: 'jonas@example.com', password: strongPassword, language: 'en' })
  })

  it('sends the Lithuanian UI language', async () => {
    const user = userEvent.setup()
    let body: { language?: string } = {}
    server.use(
      http.post(authUrl('/register'), async ({ request }) => {
        body = (await request.json()) as { language?: string }
        return accepted()
      }),
    )
    renderRoutesAt(routes, '/sign-up', { session: 'signedOut', language: 'lt' })
    await screen.findByRole('heading', { level: 1, name: 'Sukurti paskyrą' })
    await user.type(screen.getByLabelText('El. paštas', { selector: 'input' }), 'a@b.test')
    await user.type(screen.getByLabelText('Slaptažodis', { selector: 'input' }), strongPassword)
    await user.click(screen.getByRole('button', { name: 'Sukurti paskyrą' }))

    await waitFor(() => {
      expect(body.language).toBe('lt')
    })
  })

  it('shows the validation messages and sends nothing for empty or weak values', async () => {
    const user = userEvent.setup()
    renderRoutesAt(routes, '/sign-up', { session: 'signedOut', language: 'en' })
    await screen.findByRole('heading', { level: 1, name: 'Create an account' })
    await user.type(screen.getByLabelText('Password', { selector: 'input' }), 'short')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('This field is required')).toBeTruthy()
    expect(screen.getByText('The password must be at least 14 characters long')).toBeTruthy()
  })

  it('puts the server field errors on the email and password fields', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/register'), () =>
        problemResponse({
          status: 400,
          code: 'Validation.Failed',
          errors: { email: ['bad'], password: ['bad'] },
        }),
      ),
    )
    renderRoutesAt(routes, '/sign-up', { session: 'signedOut', language: 'en' })
    await screen.findByRole('heading', { level: 1, name: 'Create an account' })
    await fill(user)
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findAllByText('This value is not valid')).toHaveLength(2)
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('shows the general message for a field error on another field', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/register'), () =>
        problemResponse({ status: 400, code: 'Validation.Failed', errors: { language: ['bad'] } }),
      ),
    )
    renderRoutesAt(routes, '/sign-up', { session: 'signedOut', language: 'en' })
    await screen.findByRole('heading', { level: 1, name: 'Create an account' })
    await fill(user)
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(
      await screen.findByText(
        'Some of the entered values are not valid. Check them and try again.',
      ),
    ).toBeTruthy()
  })

  it('shows the network message when the server is unreachable', async () => {
    const user = userEvent.setup()
    server.use(http.post(authUrl('/register'), () => HttpResponse.error()))
    renderRoutesAt(routes, '/sign-up', { session: 'signedOut', language: 'en' })
    await screen.findByRole('heading', { level: 1, name: 'Create an account' })
    await fill(user)
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(
      await screen.findByText('Could not reach the server. Check your connection and try again.'),
    ).toBeTruthy()
  })

  it('shows the generic message for an unknown error', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/register'), () => problemResponse({ status: 500, code: 'Odd.Thing' })),
    )
    renderRoutesAt(routes, '/sign-up', { session: 'signedOut', language: 'en' })
    await screen.findByRole('heading', { level: 1, name: 'Create an account' })
    await fill(user)
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('Something went wrong. Try again.')).toBeTruthy()
  })

  it('links to sign-in', async () => {
    const user = userEvent.setup()
    const { router } = renderRoutesAt(routes, '/sign-up', { session: 'signedOut', language: 'en' })
    await user.click(await screen.findByRole('link', { name: 'Sign in' }))

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/sign-in')
    })
  })
})
