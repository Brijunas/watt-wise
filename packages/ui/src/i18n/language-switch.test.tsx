import { createI18n, languageStorageKey } from '@wattwise/i18n'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { I18nextProvider } from 'react-i18next'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import AppThemeProvider from '../theme/app-theme-provider'
import LanguageSwitch from './language-switch'

beforeEach(() => {
  vi.stubGlobal('matchMedia', (query: string) => ({
    matches: false,
    media: query,
    addEventListener: () => undefined,
    removeEventListener: () => undefined,
    addListener: () => undefined,
    removeListener: () => undefined,
  }))
  localStorage.clear()
  document.documentElement.lang = ''
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderSwitch(lng?: 'en' | 'lt') {
  const user = userEvent.setup()
  const view = render(
    <I18nextProvider i18n={createI18n({ lng })}>
      <AppThemeProvider>
        <LanguageSwitch />
      </AppThemeProvider>
    </I18nextProvider>,
  )
  return { user, ...view }
}

async function choose(user: ReturnType<typeof userEvent.setup>, name: string) {
  await user.click(screen.getByRole('button'))
  await user.click(screen.getByRole('menuitemradio', { name }))
}

describe('LanguageSwitch', () => {
  it('lists English and Lietuvių with English checked by default', async () => {
    const { user } = renderSwitch()
    await user.click(screen.getByRole('button', { name: 'Language: EN' }))
    const items = screen.getAllByRole('menuitemradio')
    expect(items.map((item) => item.textContent)).toEqual(['English', 'Lietuvių'])
    expect(items.map((item) => item.getAttribute('lang'))).toEqual(['en', 'lt'])
    expect(items.map((item) => item.getAttribute('aria-checked'))).toEqual(['true', 'false'])
  })

  it('switches to Lithuanian and stores the choice', async () => {
    const { user } = renderSwitch()
    await choose(user, 'Lietuvių')
    expect(screen.getByRole('button', { name: 'Kalba: LT' })).toBeTruthy()
    expect(localStorage.getItem(languageStorageKey)).toBe('lt')
    expect(document.documentElement.lang).toBe('lt')

    await user.click(screen.getByRole('button'))
    expect(
      screen.getByRole('menuitemradio', { name: 'Lietuvių' }).getAttribute('aria-checked'),
    ).toBe('true')
  })

  it('switches back to English', async () => {
    const { user } = renderSwitch()
    await choose(user, 'Lietuvių')
    await choose(user, 'English')
    expect(screen.getByRole('button', { name: 'Language: EN' })).toBeTruthy()
    expect(localStorage.getItem(languageStorageKey)).toBe('en')
    expect(document.documentElement.lang).toBe('en')
  })

  it('picks up a stored language on mount', () => {
    localStorage.setItem(languageStorageKey, 'lt')
    renderSwitch()
    expect(screen.getByRole('button', { name: 'Kalba: LT' })).toBeTruthy()
  })

  // WCAG 2.5.3 Label in Name: the visible code is part of the accessible name.
  it.each([
    { lng: 'en', code: 'EN', name: 'Language: EN' },
    { lng: 'lt', code: 'LT', name: 'Kalba: LT' },
  ] as const)('shows the code $code in the button named $name', ({ lng, code, name }) => {
    renderSwitch(lng)
    const button = screen.getByRole('button', { name })
    expect(button.textContent).toBe(code)
  })

  it.each(['en', 'lt'] as const)('has no accessibility violations in %s', async (lng) => {
    const { container } = renderSwitch(lng)
    expect((await axe(container, { rules: { region: { enabled: false } } })).violations).toEqual([])
  })
})
