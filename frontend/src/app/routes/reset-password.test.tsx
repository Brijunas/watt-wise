import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/testing/mocks/server'
import { authUrl } from '@/testing/mocks/handlers/auth'
import { problemResponse } from '@wattwise/core/testing'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

const link = '/reset-password?id=abc&token=t%2Bk'
const strongPassword = 'Correct-Horse-9-Battery'
const invalidLink = 'This link is invalid or has expired. Request a new one.'

const fillAndSubmit = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(
    await screen.findByLabelText('New password', { selector: 'input' }),
    strongPassword,
  )
  await user.click(screen.getByRole('button', { name: 'Set the new password' }))
}

describe('Reset-password route', () => {
  it.each([
    [360, 'en', 'Reset password', 'Set the new password', 'New password'],
    [900, 'en', 'Reset password', 'Set the new password', 'New password'],
    [360, 'lt', 'Slaptažodžio atkūrimas', 'Nustatyti naują slaptažodį', 'Naujas slaptažodis'],
    [900, 'lt', 'Slaptažodžio atkūrimas', 'Nustatyti naują slaptažodį', 'Naujas slaptažodis'],
  ] as const)('renders at %i px in %s', async (width, language, heading, button, label) => {
    renderRoutesAt(routes, link, { width, language })

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeTruthy()
    expect(screen.getByRole('button', { name: button })).toBeTruthy()
    expect(screen.getByLabelText(label, { selector: 'input' }).getAttribute('autocomplete')).toBe(
      'new-password',
    )
  })

  it('resets the password and goes to sign-in with a notice', async () => {
    const user = userEvent.setup()
    let body: unknown
    server.use(
      http.post(authUrl('/reset-password'), async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const { router } = renderRoutesAt(routes, link, { language: 'en', session: 'signedOut' })
    await fillAndSubmit(user)

    expect(
      await screen.findByText('Your password was changed. Sign in with the new one.'),
    ).toBeTruthy()
    expect(router.state.location.pathname).toBe('/sign-in')
    expect(body).toEqual({ id: 'abc', token: 't+k', newPassword: strongPassword })
  })

  it('shows the password rules and sends nothing for a weak password', async () => {
    const user = userEvent.setup()
    let calls = 0
    server.use(
      http.post(authUrl('/reset-password'), () => {
        calls += 1
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderRoutesAt(routes, link, { language: 'en' })
    await user.type(await screen.findByLabelText('New password', { selector: 'input' }), 'short')
    await user.click(screen.getByRole('button', { name: 'Set the new password' }))

    expect(await screen.findByText('The password must be at least 14 characters long')).toBeTruthy()
    expect(calls).toBe(0)
  })

  it.each(['/reset-password', '/reset-password?id=abc', '/reset-password?token=x'])(
    'shows the invalid-link state for %s',
    async (path) => {
      const user = userEvent.setup()
      const { router } = renderRoutesAt(routes, path, { language: 'en' })

      expect(await screen.findByText(invalidLink)).toBeTruthy()
      expect(screen.queryByLabelText('New password', { selector: 'input' })).toBeNull()
      await user.click(screen.getByRole('link', { name: 'Request a new link' }))
      await waitFor(() => {
        expect(router.state.location.pathname).toBe('/forgot-password')
      })
    },
  )

  it('shows the invalid-link state when the server rejects the link', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/reset-password'), () =>
        problemResponse({ status: 400, code: 'Accounts.InvalidEmailLink' }),
      ),
    )
    renderRoutesAt(routes, link, { language: 'en' })
    await fillAndSubmit(user)

    expect(await screen.findByText(invalidLink)).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Request a new link' })).toBeTruthy()
  })

  it('puts a server error on newPassword on the field', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/reset-password'), () =>
        problemResponse({
          status: 400,
          code: 'Validation.Failed',
          errors: { newPassword: ['Passwords must have a digit'] },
        }),
      ),
    )
    renderRoutesAt(routes, link, { language: 'en' })
    await fillAndSubmit(user)

    expect(await screen.findByText('This value is not valid')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('shows the network message', async () => {
    const user = userEvent.setup()
    server.use(http.post(authUrl('/reset-password'), () => HttpResponse.error()))
    renderRoutesAt(routes, link, { language: 'en' })
    await fillAndSubmit(user)

    expect(
      await screen.findByText('Could not reach the server. Check your connection and try again.'),
    ).toBeTruthy()
  })

  it('shows the message of another error', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/reset-password'), () =>
        problemResponse({ status: 500, code: 'General.Unexpected' }),
      ),
    )
    renderRoutesAt(routes, link, { language: 'en' })
    await fillAndSubmit(user)

    expect(
      await screen.findByText('Something went wrong on our side. Try again in a moment.'),
    ).toBeTruthy()
  })
})
