import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeAll, describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
import { createGate, problemResponse } from '@wattwise/core/testing'
import { authUrl } from '@/testing/mocks/handlers/auth'
import { server } from '@/testing/mocks/server'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

// What S5A.5 changed on every existing page: the h1 and its card, one gradient (contained) button
// per form, and no accessibility violations in the page's `main`. The behaviour of each page is
// tested in its own route test.

beforeAll(() => {
  process.env.TZ = 'UTC'
})

type Language = 'en' | 'lt'
type Path = string | { pathname: string; state?: unknown }

interface Page {
  name: string
  path: Path
  session: 'signedIn' | 'signedOut'
  title: Record<Language, string>
  /** The state the page is in when it is checked: a final element to wait for. */
  ready: Record<Language, () => Promise<unknown>>
  /** Sets up the Api before rendering. */
  prepare?: () => void
  /** Walks the page into the state, after rendering. */
  interact?: (user: ReturnType<typeof userEvent.setup>, language: Language) => Promise<void>
  /** The `.MuiButton-contained` buttons inside `main`. */
  contained: number
  /** The h1 sits inside the WwAccountPage card. */
  accountCard?: true
}

const none = () => Promise.resolve()
const readyNone = { en: none, lt: none }
const button = (name: string) => () => screen.findByRole('button', { name })
const link = (name: string) => () => screen.findByRole('link', { name })
const text = (value: string) => () => screen.findByText(value)

const email = { en: 'Email', lt: 'El. paštas' }
const password = { en: 'Password', lt: 'Slaptažodis' }
const signInButton = { en: 'Sign in', lt: 'Prisijungti' }

const pages: Page[] = [
  {
    name: 'sign-up',
    path: '/sign-up',
    session: 'signedOut',
    title: { en: 'Create an account', lt: 'Sukurti paskyrą' },
    ready: readyNone,
    contained: 1,
    accountCard: true,
  },
  {
    name: 'check-email',
    path: { pathname: '/check-email', state: { email: 'jonas@example.com' } },
    session: 'signedOut',
    title: { en: 'Check your email', lt: 'Patikrinkite el. paštą' },
    ready: { en: button('Send again'), lt: button('Siųsti dar kartą') },
    contained: 0,
    accountCard: true,
  },
  {
    name: 'confirm-email, confirming',
    path: '/confirm-email?id=abc&token=t',
    session: 'signedOut',
    title: { en: 'Confirm your email', lt: 'Patvirtinkite el. paštą' },
    prepare: () => {
      const gate = createGate()
      server.use(
        http.post(authUrl('/confirm-email'), async () => {
          await gate.wait
          return new HttpResponse(null, { status: 204 })
        }),
      )
    },
    ready: {
      en: text('Confirming your email address...'),
      lt: text('Patvirtinamas jūsų el. pašto adresas...'),
    },
    contained: 0,
    accountCard: true,
  },
  {
    name: 'confirm-email, confirmed',
    path: '/confirm-email?id=abc&token=t',
    session: 'signedOut',
    title: { en: 'Confirm your email', lt: 'Patvirtinkite el. paštą' },
    ready: { en: link('Sign in'), lt: link('Prisijungti') },
    contained: 1,
    accountCard: true,
  },
  {
    name: 'confirm-email, invalid link with the resend form',
    path: '/confirm-email',
    session: 'signedOut',
    title: { en: 'Confirm your email', lt: 'Patvirtinkite el. paštą' },
    ready: { en: button('Send a new link'), lt: button('Siųsti naują nuorodą') },
    contained: 1,
    accountCard: true,
  },
  {
    name: 'sign-in',
    path: '/sign-in',
    session: 'signedOut',
    title: { en: 'Sign in', lt: 'Prisijungimas' },
    ready: { en: button('Sign in'), lt: button('Prisijungti') },
    contained: 1,
    accountCard: true,
  },
  {
    name: 'sign-in, email not confirmed',
    path: '/sign-in',
    session: 'signedOut',
    title: { en: 'Sign in', lt: 'Prisijungimas' },
    prepare: () => {
      server.use(
        http.post(authUrl('/sign-in'), () =>
          problemResponse({ status: 403, code: 'Accounts.EmailNotConfirmed' }),
        ),
      )
    },
    interact: async (user, language) => {
      await user.type(screen.getByLabelText(email[language], { selector: 'input' }), 'a@b.test')
      await user.type(screen.getByLabelText(password[language], { selector: 'input' }), 'whatever')
      await user.click(screen.getByRole('button', { name: signInButton[language] }))
    },
    ready: { en: button('Send the link again'), lt: button('Siųsti nuorodą dar kartą') },
    contained: 1,
    accountCard: true,
  },
  {
    name: 'forgot password',
    path: '/forgot-password',
    session: 'signedOut',
    title: { en: 'Forgot password', lt: 'Pamiršau slaptažodį' },
    ready: { en: button('Send the link'), lt: button('Siųsti nuorodą') },
    contained: 1,
    accountCard: true,
  },
  {
    name: 'reset password, form',
    path: '/reset-password?id=abc&token=t',
    session: 'signedOut',
    title: { en: 'Reset password', lt: 'Slaptažodžio atkūrimas' },
    ready: { en: button('Set the new password'), lt: button('Nustatyti naują slaptažodį') },
    contained: 1,
    accountCard: true,
  },
  {
    name: 'reset password, invalid link',
    path: '/reset-password',
    session: 'signedOut',
    title: { en: 'Reset password', lt: 'Slaptažodžio atkūrimas' },
    ready: { en: link('Request a new link'), lt: link('Paprašyti naujos nuorodos') },
    contained: 1,
    accountCard: true,
  },
  {
    name: 'change password',
    path: '/change-password',
    session: 'signedIn',
    title: { en: 'Change password', lt: 'Slaptažodžio keitimas' },
    ready: { en: button('Change password'), lt: button('Keisti slaptažodį') },
    contained: 1,
    accountCard: true,
  },
  {
    name: 'devices',
    path: '/devices',
    session: 'signedIn',
    title: { en: 'Devices', lt: 'Įrenginiai' },
    ready: { en: text('Safari on iPhone'), lt: text('Safari on iPhone') },
    contained: 0,
  },
  {
    name: 'settings',
    path: '/settings',
    session: 'signedIn',
    title: { en: 'Settings', lt: 'Nustatymai' },
    ready: { en: link('Devices'), lt: link('Įrenginiai') },
    contained: 0,
  },
  {
    name: 'about',
    path: '/about',
    session: 'signedIn',
    title: { en: 'About', lt: 'Apie' },
    ready: { en: text('Version: 1.2.3'), lt: text('Versija: 1.2.3') },
    contained: 0,
  },
  {
    name: 'home',
    path: '/',
    session: 'signedIn',
    title: { en: 'Home', lt: 'Pradžia' },
    ready: readyNone,
    contained: 0,
  },
  {
    name: 'placeholder',
    path: '/placeholder',
    session: 'signedIn',
    title: { en: 'Placeholder', lt: 'Pavyzdys' },
    ready: readyNone,
    contained: 0,
  },
  {
    name: 'not found',
    path: '/no-such-page',
    session: 'signedIn',
    title: { en: 'Page not found', lt: 'Puslapis nerastas' },
    ready: { en: link('Go to home'), lt: link('Grįžti į pradžią') },
    contained: 1,
  },
]

const open = async (page: Page, language: Language, width: number) => {
  page.prepare?.()
  renderRoutesAt(routes, page.path, { width, language, session: page.session })
  await screen.findByRole('heading', { level: 1, name: page.title[language] })
  await page.interact?.(userEvent.setup(), language)
  await page.ready[language]()
  return screen.getByRole('main')
}

describe.each(['en', 'lt'] as const)('Existing pages in %s', (language) => {
  describe.each(pages)('$name', (page) => {
    it('has exactly one h1, with the page name', async () => {
      await open(page, language, 1366)

      expect(screen.getAllByRole('heading', { level: 1 }).map((h1) => h1.textContent)).toEqual([
        page.title[language],
      ])
    })

    it('has one contained button for the whole page', async () => {
      const main = await open(page, language, 1366)

      expect(main.querySelectorAll('.MuiButton-contained')).toHaveLength(page.contained)
    })

    it.each([360, 1366])('has no accessibility violations at %i px', async (width) => {
      const main = await open(page, language, width)

      expect((await axe(main)).violations).toEqual([])
    })

    if (page.accountCard) {
      it('puts the h1 inside the account card', async () => {
        const main = await open(page, language, 360)

        const card = main.querySelector('[class*="WwAccountPage-card"]')
        expect(card?.contains(screen.getByRole('heading', { level: 1 }))).toBe(true)
      })
    }
  })
})

const devicesText = {
  en: {
    title: 'Devices',
    thisDevice: 'This device',
    endSession: 'End session on Firefox on Linux',
    everywhere: 'Sign out everywhere',
  },
  lt: {
    title: 'Įrenginiai',
    thisDevice: 'Šis įrenginys',
    endSession: 'Užbaigti seansą įrenginyje Firefox on Linux',
    everywhere: 'Atsijungti visur',
  },
} as const

describe.each(['en', 'lt'] as const)('Devices page markup in %s', (language) => {
  const t = devicesText[language]
  const openDevices = async () => {
    renderRoutesAt(routes, '/devices', { language, session: 'signedIn' })
    await screen.findByText('Safari on iPhone')
    return screen.getByRole('main')
  }

  it('titles each session with an h2 and uses no h3', async () => {
    const main = await openDevices()

    expect([
      within(main)
        .getAllByRole('heading', { level: 2 })
        .map((h2) => h2.textContent),
      within(main).queryAllByRole('heading', { level: 3 }),
    ]).toEqual([['Firefox on Linux', 'Safari on iPhone'], []])
  })

  it('renders each session as a default (not outlined) card list item', async () => {
    const main = await openDevices()

    const items = within(main).getAllByRole('listitem')
    expect(
      items.map((item) => [
        item.tagName,
        item.classList.contains('MuiCard-root'),
        item.classList.contains('MuiPaper-outlined'),
      ]),
    ).toEqual([
      ['LI', true, false],
      ['LI', true, false],
    ])
  })

  it('shows the current-device chip in the default colour', async () => {
    const main = await openDevices()

    const chip = within(main).getByText(t.thisDevice).closest('.MuiChip-root')
    expect(chip?.classList.contains('MuiChip-colorPrimary')).toBe(false)
  })

  it('shows "End session" as an outlined button named after the device', async () => {
    const main = await openDevices()

    const end = within(main).getByRole('button', { name: t.endSession })
    expect(end.classList.contains('MuiButton-outlined')).toBe(true)
  })

  it('shows "Sign out everywhere" outlined without the error colour', async () => {
    const main = await openDevices()

    const everywhere = within(main).getByRole('button', { name: t.everywhere })
    expect([
      everywhere.classList.contains('MuiButton-outlined'),
      everywhere.classList.contains('MuiButton-colorError'),
    ]).toEqual([true, false])
  })
})

describe.each([
  ['en', 'Change password', 'Devices', 'API: Watt-Wise API', 'Version: 1.2.3'],
  ['lt', 'Keisti slaptažodį', 'Įrenginiai', 'API: Watt-Wise API', 'Versija: 1.2.3'],
] as const)(
  'Cards on the settings and about pages in %s',
  (language, changePassword, devices, name, version) => {
    it('wraps the settings links in one card', async () => {
      renderRoutesAt(routes, '/settings', { language, session: 'signedIn' })
      const card = (await screen.findByRole('link', { name: devices })).closest('.MuiCard-root')

      expect(
        within(card as HTMLElement)
          .getAllByRole('link')
          .map((anchor) => anchor.textContent),
      ).toEqual([changePassword, devices])
    })

    it('wraps the API name and version in one card', async () => {
      renderRoutesAt(routes, '/about', { language, session: 'signedIn' })
      const card = (await screen.findByText(name)).closest('.MuiCard-root')

      expect(card?.textContent).toBe(name + version)
    })
  },
)
