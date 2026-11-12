import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { selectAccessToken, selectSessionStatus, sessionEnded } from '@wattwise/core'
import { describe, expect, it, vi } from 'vitest'
import { server } from '@/testing/mocks/server'
import { authUrl } from '@/testing/mocks/handlers/auth'
import { createGate, problemResponse } from '@wattwise/core/testing'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

const signedInEn = { session: 'signedIn', language: 'en' } as const
const strongPassword = 'Correct-Horse-9-Battery'
const successText = 'Your password was changed. Your other devices were signed out.'

const fillAndSubmit = async (user: ReturnType<typeof userEvent.setup>) => {
  await user.type(
    await screen.findByLabelText('Current password', { selector: 'input' }),
    'old-password',
  )
  await user.type(screen.getByLabelText('New password', { selector: 'input' }), strongPassword)
  await user.click(screen.getByRole('button', { name: 'Change password' }))
}

const changeFails = (code: string, status: number, retryAfterSeconds?: number) => {
  server.use(
    http.post(authUrl('/change-password'), () =>
      problemResponse({ status, code, retryAfterSeconds }),
    ),
  )
}

describe('Change-password route', () => {
  it.each([
    [360, 'en', 'Change password', 'Change password', 'Current password'],
    [900, 'en', 'Change password', 'Change password', 'Current password'],
    [360, 'lt', 'Slaptažodžio keitimas', 'Keisti slaptažodį', 'Dabartinis slaptažodis'],
    [900, 'lt', 'Slaptažodžio keitimas', 'Keisti slaptažodį', 'Dabartinis slaptažodis'],
  ] as const)('renders at %i px in %s', async (width, language, heading, button, label) => {
    renderRoutesAt(routes, '/change-password', { width, language, session: 'signedIn' })

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeTruthy()
    expect(screen.getByRole('button', { name: button })).toBeTruthy()
    expect(screen.getByLabelText(label, { selector: 'input' }).getAttribute('autocomplete')).toBe(
      'current-password',
    )
  })

  it('changes the password, shows the notice, clears the form and keeps the new token', async () => {
    const user = userEvent.setup()
    let body: unknown
    server.use(
      http.post(authUrl('/change-password'), async ({ request }) => {
        body = await request.json()
        return HttpResponse.json({
          accessToken: 'replaced-token',
          accessTokenExpiresAt: '2026-01-01T00:15:00Z',
        })
      }),
    )
    const { store } = renderRoutesAt(routes, '/change-password', signedInEn)
    await fillAndSubmit(user)

    expect(await screen.findByText(successText)).toBeTruthy()
    expect(body).toEqual({ currentPassword: 'old-password', newPassword: strongPassword })
    expect(
      screen.getByLabelText<HTMLInputElement>('Current password', { selector: 'input' }).value,
    ).toBe('')
    expect(
      screen.getByLabelText<HTMLInputElement>('New password', { selector: 'input' }).value,
    ).toBe('')
    expect(selectAccessToken(store.getState())).toBe('replaced-token')
  })

  it('requires the current password and checks the new one against the rules', async () => {
    const user = userEvent.setup()
    renderRoutesAt(routes, '/change-password', signedInEn)
    await user.type(await screen.findByLabelText('New password', { selector: 'input' }), 'short')
    await user.click(screen.getByRole('button', { name: 'Change password' }))

    expect(await screen.findByText('This field is required')).toBeTruthy()
    expect(screen.getByText('The password must be at least 14 characters long')).toBeTruthy()
  })

  it('marks the current password when it is wrong', async () => {
    const user = userEvent.setup()
    changeFails('Accounts.InvalidCurrentPassword', 400)
    renderRoutesAt(routes, '/change-password', signedInEn)
    await fillAndSubmit(user)

    expect(await screen.findByText('The current password is wrong.')).toBeTruthy()
    expect(
      screen.getByLabelText('Current password', { selector: 'input' }).getAttribute('aria-invalid'),
    ).toBe('true')
    expect(screen.queryByText(successText)).toBeNull()
  })

  it('shows the lockout wait in minutes', async () => {
    const user = userEvent.setup()
    changeFails('Accounts.LockedOut', 423, 90)
    renderRoutesAt(routes, '/change-password', signedInEn)
    await fillAndSubmit(user)

    expect(await screen.findByText('Too many failed attempts. Try again in 2 min.')).toBeTruthy()
  })

  it('shows the lockout message without a time when Retry-After is missing', async () => {
    const user = userEvent.setup()
    changeFails('Accounts.LockedOut', 423)
    renderRoutesAt(routes, '/change-password', signedInEn)
    await fillAndSubmit(user)

    expect(await screen.findByText('Too many failed attempts. Try again later.')).toBeTruthy()
  })

  it('asks to try again after a conflict', async () => {
    const user = userEvent.setup()
    changeFails('Accounts.PasswordChangeConflict', 409)
    renderRoutesAt(routes, '/change-password', signedInEn)
    await fillAndSubmit(user)

    expect(
      await screen.findByText('Your account was changed at the same time. Try again.'),
    ).toBeTruthy()
  })

  it('shows the message for an account that was not found', async () => {
    const user = userEvent.setup()
    changeFails('Accounts.AccountNotFound', 404)
    renderRoutesAt(routes, '/change-password', signedInEn)
    await fillAndSubmit(user)

    expect(await screen.findByText('We could not find this account.')).toBeTruthy()
  })

  it('puts a server error on newPassword on the field', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/change-password'), () =>
        problemResponse({
          status: 400,
          code: 'Validation.Failed',
          errors: { newPassword: ['bad'] },
        }),
      ),
    )
    renderRoutesAt(routes, '/change-password', signedInEn)
    await fillAndSubmit(user)

    expect(await screen.findByText('This value is not valid')).toBeTruthy()
    expect(screen.queryByRole('alert')).toBeNull()
  })

  it('shows the network message', async () => {
    const user = userEvent.setup()
    server.use(http.post(authUrl('/change-password'), () => HttpResponse.error()))
    renderRoutesAt(routes, '/change-password', signedInEn)
    await fillAndSubmit(user)

    expect(
      await screen.findByText('Could not reach the server. Check your connection and try again.'),
    ).toBeTruthy()
  })

  it('shows the generic message for an unknown error', async () => {
    const user = userEvent.setup()
    changeFails('Odd.Thing', 500)
    renderRoutesAt(routes, '/change-password', signedInEn)
    await fillAndSubmit(user)

    expect(await screen.findByText('Something went wrong. Try again.')).toBeTruthy()
  })

  it('shows no success notice when the answer arrives after the user signed out', async () => {
    const user = userEvent.setup()
    const gate = createGate()
    let answered = false
    server.use(
      http.post(authUrl('/change-password'), async () => {
        await gate.wait
        answered = true
        return HttpResponse.json({
          accessToken: 'late-token',
          accessTokenExpiresAt: '2026-01-01T00:15:00Z',
        })
      }),
    )
    const { store, router } = renderRoutesAt(routes, '/change-password', signedInEn)
    await fillAndSubmit(user)
    act(() => {
      store.dispatch(sessionEnded())
    })
    expect(await screen.findByRole('heading', { level: 1, name: 'Sign in' })).toBeTruthy()
    expect(router.state.location.pathname).toBe('/sign-in')
    gate.open()
    await vi.waitFor(() => {
      expect(answered).toBe(true)
    })
    await new Promise((resolve) => setTimeout(resolve, 50))

    expect(screen.queryByText(successText)).toBeNull()
    expect(selectSessionStatus(store.getState())).toBe('signedOut')
    expect(selectAccessToken(store.getState())).toBeNull()
    expect(router.state.location.pathname).toBe('/sign-in')
  })
})
