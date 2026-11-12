import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/testing/mocks/server'
import { authUrl, problemResponse } from '@/testing/mocks/handlers/auth'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

const at = { pathname: '/check-email', state: { email: 'jonas@example.com' } }

describe('Check-email route', () => {
  it.each([
    [360, 'en', 'Check your email', 'Send again'],
    [900, 'en', 'Check your email', 'Send again'],
    [360, 'lt', 'Patikrinkite el. paštą', 'Siųsti dar kartą'],
    [900, 'lt', 'Patikrinkite el. paštą', 'Siųsti dar kartą'],
  ] as const)('renders at %i px in %s', async (width, language, heading, button) => {
    renderRoutesAt(routes, at, { width, language })

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeTruthy()
    expect(screen.getByRole('button', { name: button })).toBeTruthy()
  })

  it('says the link went to the address and sends it again on request', async () => {
    const user = userEvent.setup()
    let body: unknown
    server.use(
      http.post(authUrl('/resend-confirmation'), async ({ request }) => {
        body = await request.json()
        return new HttpResponse(null, { status: 202 })
      }),
    )
    renderRoutesAt(routes, at, { language: 'en' })
    expect(
      await screen.findByText(
        'We sent a confirmation link to jonas@example.com. Open it to finish creating your account.',
      ),
    ).toBeTruthy()
    await user.click(screen.getByRole('button', { name: 'Send again' }))

    expect(await screen.findByText('We sent the link again.')).toBeTruthy()
    expect(body).toEqual({ email: 'jonas@example.com' })
  })

  it('shows the error when sending again fails', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/resend-confirmation'), () =>
        problemResponse({ status: 500, code: 'General.Unexpected' }),
      ),
    )
    renderRoutesAt(routes, at, { language: 'en' })
    await user.click(await screen.findByRole('button', { name: 'Send again' }))

    expect(
      await screen.findByText('Something went wrong on our side. Try again in a moment.'),
    ).toBeTruthy()
    expect(screen.queryByText('We sent the link again.')).toBeNull()
  })

  it('shows a generic text and no button without the address', async () => {
    renderRoutesAt(routes, '/check-email', { language: 'en' })

    expect(
      await screen.findByText(
        'We sent you a confirmation link. Open it to finish creating your account.',
      ),
    ).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Send again' })).toBeNull()
  })
})
