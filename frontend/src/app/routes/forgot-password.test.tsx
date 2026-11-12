import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/testing/mocks/server'
import { authUrl, problemResponse } from '@/testing/mocks/handlers/auth'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

const sentText = "If an account uses this address, we've sent a link to reset the password."

const accept = (body: { current?: unknown }) =>
  http.post(authUrl('/forgot-password'), async ({ request }) => {
    body.current = await request.json()
    return new HttpResponse(null, { status: 202 })
  })

describe('Forgot-password route', () => {
  it.each([
    [360, 'en', 'Forgot password', 'Send the link'],
    [900, 'en', 'Forgot password', 'Send the link'],
    [360, 'lt', 'Pamiršau slaptažodį', 'Siųsti nuorodą'],
    [900, 'lt', 'Pamiršau slaptažodį', 'Siųsti nuorodą'],
  ] as const)('renders at %i px in %s', async (width, language, heading, button) => {
    renderRoutesAt(routes, '/forgot-password', { width, language })

    expect(await screen.findByRole('heading', { level: 1, name: heading })).toBeTruthy()
    expect(screen.getByRole('button', { name: button })).toBeTruthy()
  })

  it('says a link was sent, whatever the address', async () => {
    const user = userEvent.setup()
    const body: { current?: unknown } = {}
    server.use(accept(body))
    renderRoutesAt(routes, '/forgot-password', { language: 'en' })
    await user.type(
      await screen.findByLabelText('Email', { selector: 'input' }),
      'nobody@example.com',
    )
    await user.click(screen.getByRole('button', { name: 'Send the link' }))

    expect(await screen.findByText(sentText)).toBeTruthy()
    expect(body.current).toEqual({ email: 'nobody@example.com' })
  })

  it('shows the validation message for an empty address and sends nothing', async () => {
    const user = userEvent.setup()
    const body: { current?: unknown } = {}
    server.use(accept(body))
    renderRoutesAt(routes, '/forgot-password', { language: 'en' })
    await user.click(await screen.findByRole('button', { name: 'Send the link' }))

    expect(await screen.findByText('This field is required')).toBeTruthy()
    expect(body.current).toBeUndefined()
    expect(screen.queryByText(sentText)).toBeNull()
  })

  it('shows the network message', async () => {
    const user = userEvent.setup()
    server.use(http.post(authUrl('/forgot-password'), () => HttpResponse.error()))
    renderRoutesAt(routes, '/forgot-password', { language: 'en' })
    await user.type(await screen.findByLabelText('Email', { selector: 'input' }), 'a@b.test')
    await user.click(screen.getByRole('button', { name: 'Send the link' }))

    expect(
      await screen.findByText('Could not reach the server. Check your connection and try again.'),
    ).toBeTruthy()
    expect(screen.queryByText(sentText)).toBeNull()
  })

  it('shows the message of a server error', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(authUrl('/forgot-password'), () =>
        problemResponse({ status: 500, code: 'General.Unexpected' }),
      ),
    )
    renderRoutesAt(routes, '/forgot-password', { language: 'en' })
    await user.type(await screen.findByLabelText('Email', { selector: 'input' }), 'a@b.test')
    await user.click(screen.getByRole('button', { name: 'Send the link' }))

    expect(
      await screen.findByText('Something went wrong on our side. Try again in a moment.'),
    ).toBeTruthy()
  })

  it('links back to sign-in', async () => {
    const user = userEvent.setup()
    const { router } = renderRoutesAt(routes, '/forgot-password', {
      language: 'en',
      session: 'signedOut',
    })
    await user.click(await screen.findByRole('link', { name: 'Back to sign in' }))

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/sign-in')
    })
  })
})
