import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
import { renderRoutesAt } from '@/testing/test-utils'
import { routes } from '../router'

// What S5A.5 changed on every existing page: the h1 and its card, one gradient (contained) button
// per form, and no accessibility violations in the page's `main`. The behaviour of each page is
// tested in its own route test.

type Language = 'en' | 'lt'

interface Page {
  name: string
  path: string
  session: 'signedIn' | 'signedOut'
  title: Record<Language, string>
  /** A final element of the page to wait for. */
  ready: Record<Language, { role: 'button' | 'link' | 'heading'; name: string }>
  /** The `.MuiButton-contained` buttons inside `main`. */
  contained: number
  /** The h1 sits inside the WwAccountPage card. */
  accountCard?: true
}

const pages: Page[] = [
  {
    name: 'sign-in',
    path: '/sign-in',
    session: 'signedOut',
    title: { en: 'Sign in', lt: 'Prisijungimas' },
    ready: {
      en: { role: 'button', name: 'Sign in' },
      lt: { role: 'button', name: 'Prisijungti' },
    },
    contained: 1,
    accountCard: true,
  },
  {
    name: 'no-access, signed out',
    path: '/no-access',
    session: 'signedOut',
    title: { en: 'No access', lt: 'Nėra prieigos' },
    ready: { en: { role: 'link', name: 'Sign in' }, lt: { role: 'link', name: 'Prisijungti' } },
    contained: 1,
    accountCard: true,
  },
  {
    name: 'no-access, still signed in',
    path: '/no-access',
    session: 'signedIn',
    title: { en: 'No access', lt: 'Nėra prieigos' },
    ready: {
      en: { role: 'heading', name: 'No access' },
      lt: { role: 'heading', name: 'Nėra prieigos' },
    },
    contained: 0,
    accountCard: true,
  },
  {
    name: 'home',
    path: '/',
    session: 'signedIn',
    title: { en: 'Home', lt: 'Pradžia' },
    ready: { en: { role: 'heading', name: 'Home' }, lt: { role: 'heading', name: 'Pradžia' } },
    contained: 0,
  },
  {
    name: 'catalog review',
    path: '/catalog-review',
    session: 'signedIn',
    title: { en: 'Catalog review', lt: 'Katalogo peržiūra' },
    ready: {
      en: { role: 'heading', name: 'Catalog review' },
      lt: { role: 'heading', name: 'Katalogo peržiūra' },
    },
    contained: 0,
  },
  {
    name: 'about',
    path: '/about',
    session: 'signedIn',
    title: { en: 'About', lt: 'Apie' },
    ready: { en: { role: 'heading', name: 'About' }, lt: { role: 'heading', name: 'Apie' } },
    contained: 0,
  },
  {
    name: 'not found',
    path: '/no-such-page',
    session: 'signedIn',
    title: { en: 'Page not found', lt: 'Puslapis nerastas' },
    ready: {
      en: { role: 'link', name: 'Go to home' },
      lt: { role: 'link', name: 'Grįžti į pradžią' },
    },
    contained: 1,
  },
]

const aboutVersion = { en: 'Version: 1.2.3', lt: 'Versija: 1.2.3' }

const open = async (page: Page, language: Language, width: number) => {
  renderRoutesAt(routes, page.path, { width, language, session: page.session })
  await screen.findByRole('heading', { level: 1, name: page.title[language] })
  const ready = page.ready[language]
  await screen.findByRole(ready.role, { name: ready.name })
  if (page.name === 'about') {
    await screen.findByText(aboutVersion[language])
  }
  return screen.getByRole('main')
}

describe.each(['en', 'lt'] as const)('Existing admin pages in %s', (language) => {
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

describe.each([
  ['en', 'API: Watt-Wise API', 'Version: 1.2.3', 'Sign in'],
  ['lt', 'API: Watt-Wise API', 'Versija: 1.2.3', 'Prisijungti'],
] as const)('Card and button row in %s', (language, name, version, signIn) => {
  it('wraps the API name and version in one card', async () => {
    renderRoutesAt(routes, '/about', { language, session: 'signedIn' })
    const card = (await screen.findByText(name)).closest('.MuiCard-root')

    expect(card?.textContent).toBe(name + version)
  })

  it('puts the no-access Sign in button in a row, not stretched in a column', async () => {
    renderRoutesAt(routes, '/no-access', { language, session: 'signedOut' })
    const row = (await screen.findByRole('link', { name: signIn })).parentElement

    expect([
      row?.classList.contains('MuiStack-root'),
      row === null ? '' : getComputedStyle(row).getPropertyValue('flex-direction'),
    ]).toEqual([true, 'row'])
  })
})
