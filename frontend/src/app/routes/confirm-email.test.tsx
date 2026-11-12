import { StrictMode } from 'react'
import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { createMemoryRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { describe, expect, it } from 'vitest'
import AppProvider from '@/app/provider'
import { createAppI18n } from '@/lib/i18n'
import createAppStore from '@/stores/create-app-store'
import { server } from '@/testing/mocks/server'
import { authUrl } from '@/testing/mocks/handlers/auth'
import { createGate, problemResponse } from '@wattwise/core/testing'
import stubMatchMedia from '@/testing/match-media'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

const link = '/confirm-email?id=abc&token=t%2Bk'

const countingHandler = (counter: { calls: number; body?: unknown }, status = 204) =>
  http.post(authUrl('/confirm-email'), async ({ request }) => {
    counter.calls += 1
    counter.body = await request.json()
    return new HttpResponse(null, { status })
  })

describe('Confirm-email route', () => {
  it.each([
    [360, 'en', 'Confirm your email', 'Your email address is confirmed. You can sign in now.'],
    [900, 'en', 'Confirm your email', 'Your email address is confirmed. You can sign in now.'],
    [
      360,
      'lt',
      'Patvirtinkite el. paštą',
      'Jūsų el. pašto adresas patvirtintas. Dabar galite prisijungti.',
    ],
    [
      900,
      'lt',
      'Patvirtinkite el. paštą',
      'Jūsų el. pašto adresas patvirtintas. Dabar galite prisijungti.',
    ],
  ] as const)('renders at %i px in %s', async (width, language, heading, message) => {
    renderRoutesAt(routes, link, { width, language })

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeTruthy()
    expect(await screen.findByText(message)).toBeTruthy()
  })

  it('confirms the link once and offers sign-in', async () => {
    const counter = { calls: 0, body: undefined as unknown }
    server.use(countingHandler(counter))
    const { router } = renderRoutesAt(routes, link, { language: 'en' })

    expect(
      await screen.findByText('Your email address is confirmed. You can sign in now.'),
    ).toBeTruthy()
    expect(counter).toEqual({ calls: 1, body: { id: 'abc', token: 't+k' } })
    await userEvent.setup().click(screen.getByRole('link', { name: 'Sign in' }))
    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/sign-in')
    })
  })

  it('sends exactly one request under StrictMode', async () => {
    const counter = { calls: 0, body: undefined as unknown }
    server.use(countingHandler(counter))
    stubMatchMedia({ width: 360 })
    const router = createMemoryRouter(routes, { initialEntries: [link] })
    const i18n = createAppI18n()
    void i18n.changeLanguage('en')
    render(
      <StrictMode>
        <AppProvider store={createAppStore()} i18n={i18n}>
          <RouterProvider router={router} />
        </AppProvider>
      </StrictMode>,
    )

    expect(
      await screen.findByText('Your email address is confirmed. You can sign in now.'),
    ).toBeTruthy()
    expect(counter.calls).toBe(1)
  })

  it('shows the invalid-link message and a resend form for a bad link', async () => {
    const user = userEvent.setup()
    let resent: unknown
    server.use(
      http.post(authUrl('/confirm-email'), () =>
        problemResponse({ status: 400, code: 'Accounts.InvalidEmailLink' }),
      ),
      http.post(authUrl('/resend-confirmation'), async ({ request }) => {
        resent = await request.json()
        return new HttpResponse(null, { status: 202 })
      }),
    )
    renderRoutesAt(routes, link, { language: 'en' })

    expect(
      await screen.findByText('This link is invalid or has expired. Request a new one.'),
    ).toBeTruthy()
    await user.type(screen.getByLabelText('Email', { selector: 'input' }), 'jonas@example.com')
    await user.click(screen.getByRole('button', { name: 'Send a new link' }))

    expect(
      await screen.findByText('If this address needs confirming, we sent a new link.'),
    ).toBeTruthy()
    expect(resent).toEqual({ email: 'jonas@example.com' })
  })

  it.each(['/confirm-email', '/confirm-email?id=abc', '/confirm-email?token=x'])(
    'treats %s as an invalid link without a request',
    async (path) => {
      const counter = { calls: 0, body: undefined as unknown }
      server.use(countingHandler(counter))
      renderRoutesAt(routes, path, { language: 'en' })

      expect(
        await screen.findByText('This link is invalid or has expired. Request a new one.'),
      ).toBeTruthy()
      expect(screen.getByRole('button', { name: 'Send a new link' })).toBeTruthy()
      expect(counter.calls).toBe(0)
    },
  )

  it('shows the resend form errors', async () => {
    const user = userEvent.setup()
    server.use(http.post(authUrl('/resend-confirmation'), () => HttpResponse.error()))
    renderRoutesAt(routes, '/confirm-email', { language: 'en' })
    await user.type(await screen.findByLabelText('Email', { selector: 'input' }), 'a@b.test')
    await user.click(screen.getByRole('button', { name: 'Send a new link' }))

    expect(
      await screen.findByText('Could not reach the server. Check your connection and try again.'),
    ).toBeTruthy()
  })

  it('shows other errors by their message', async () => {
    server.use(
      http.post(authUrl('/confirm-email'), () =>
        problemResponse({ status: 500, code: 'General.Unexpected' }),
      ),
    )
    renderRoutesAt(routes, link, { language: 'en' })

    expect(
      await screen.findByText('Something went wrong on our side. Try again in a moment.'),
    ).toBeTruthy()
    expect(screen.queryByRole('button', { name: 'Send a new link' })).toBeNull()
  })

  it('shows the confirming text while the answer is pending', async () => {
    const gate = createGate()
    server.use(
      http.post(authUrl('/confirm-email'), async () => {
        await gate.wait
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderRoutesAt(routes, link, { language: 'en' })

    expect(await screen.findByText('Confirming your email address...')).toBeTruthy()
    gate.open()
    expect(
      await screen.findByText('Your email address is confirmed. You can sign in now.'),
    ).toBeTruthy()
  })

  it('does nothing when the answer arrives after the page is gone', async () => {
    const gate = createGate()
    const counter = { calls: 0 }
    server.use(
      http.post(authUrl('/confirm-email'), async () => {
        counter.calls += 1
        await gate.wait
        return new HttpResponse(null, { status: 204 })
      }),
    )
    renderRoutesAt(routes, link, { language: 'en' })
    await screen.findByText('Confirming your email address...')
    cleanup()
    gate.open()
    await waitFor(() => {
      expect(counter.calls).toBe(1)
    })

    expect(screen.queryByText('Your email address is confirmed. You can sign in now.')).toBeNull()
  })
})
