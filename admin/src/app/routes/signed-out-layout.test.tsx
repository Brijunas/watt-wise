import { screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

afterEach(() => {
  vi.unstubAllGlobals()
})

const text = {
  en: { language: 'Language: EN', logo: 'Watt-Wise home', account: 'Account' },
  lt: { language: 'Kalba: LT', logo: 'Watt-Wise pradžia', account: 'Paskyra' },
} as const

describe.each(['en', 'lt'] as const)('SignedOutLayout in %s', (lng) => {
  const t = text[lng]

  describe.each(['/sign-in', '/no-access'])('at %s', (path) => {
    it.each([360, 900, 1366])('has the minimal app bar at %i px', async (width) => {
      renderRoutesAt(routes, path, { width, language: lng, session: 'signedOut' })
      await screen.findByRole('heading', { level: 1 })
      expect(screen.queryAllByRole('navigation')).toHaveLength(0)
      expect(screen.queryAllByRole('button', { name: t.account })).toHaveLength(0)
      expect(screen.getByRole('button', { name: t.language })).toBeTruthy()
    })

    it('shows the Admin chip after the logo', async () => {
      renderRoutesAt(routes, path, { width: 360, language: lng, session: 'signedOut' })
      await screen.findByRole('heading', { level: 1 })
      const logo = screen.getByRole('link', { name: t.logo })
      const chip = within(screen.getByRole('banner')).getByText('Admin')
      expect(logo.compareDocumentPosition(chip) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    })
  })

  it('has no accessibility violations on the sign-in page at 360 px', async () => {
    const { container } = renderRoutesAt(routes, '/sign-in', {
      language: lng,
      session: 'signedOut',
    })
    await screen.findByRole('heading', { level: 1 })
    expect((await axe(container)).violations).toEqual([])
  })
})
