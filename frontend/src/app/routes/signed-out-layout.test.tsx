import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { paths } from '@/config/routes'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

afterEach(() => {
  vi.unstubAllGlobals()
})

// The email links open /confirm-email and /reset-password with `?id=&token=`; check-email takes the
// address in the location state.
const pages = [
  { path: paths.signIn, at: paths.signIn },
  { path: paths.signUp, at: paths.signUp },
  { path: paths.forgotPassword, at: paths.forgotPassword },
  {
    path: paths.checkEmail,
    at: { pathname: paths.checkEmail, state: { email: 'jonas@example.com' } },
  },
  { path: paths.confirmEmail, at: paths.confirmEmail + '?id=abc&token=t%2Bk' },
  { path: paths.resetPassword, at: paths.resetPassword + '?id=abc&token=t%2Bk' },
] as const

const language = { en: 'Language: EN', lt: 'Kalba: LT' } as const
const home = { en: 'Watt-Wise home', lt: 'Watt-Wise pradžia' } as const

describe.each(['en', 'lt'] as const)('SignedOutLayout in %s', (lng) => {
  describe.each(pages)('at $path', ({ at }) => {
    it.each([360, 900, 1366])('has the minimal app bar at %i px', async (width) => {
      renderRoutesAt(routes, at, { width, language: lng, session: 'signedOut' })
      await screen.findByRole('heading', { level: 1 })
      expect(screen.queryAllByRole('navigation')).toHaveLength(0)
      expect(screen.queryAllByRole('button', { name: /^(Account|Paskyra)$/ })).toHaveLength(0)
      expect(screen.getByRole('button', { name: language[lng] })).toBeTruthy()
      expect(screen.getByRole('link', { name: home[lng] }).getAttribute('href')).toBe('/')
      expect(screen.getByRole('switch')).toBeTruthy()
    })
  })

  it('has no accessibility violations on the sign-in page at 360 px', async () => {
    const { container } = renderRoutesAt(routes, paths.signIn, {
      language: lng,
      session: 'signedOut',
    })
    await screen.findByRole('heading', { level: 1 })
    expect((await axe(container)).violations).toEqual([])
  })
})
