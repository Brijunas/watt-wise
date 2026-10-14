import { createI18n } from '@wattwise/i18n'
import { act, cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { I18nextProvider } from 'react-i18next'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import AppThemeProvider from './app-theme-provider'
import ModeSwitch from './mode-switch'

let prefersDark = false
const listeners = new Set<(event: MediaQueryListEvent) => void>()

function setPrefersDark(value: boolean) {
  prefersDark = value
  for (const listener of listeners) {
    listener({ matches: value, media: '(prefers-color-scheme: dark)' } as MediaQueryListEvent)
  }
}

beforeEach(() => {
  prefersDark = false
  listeners.clear()
  vi.stubGlobal('matchMedia', (query: string) => ({
    get matches() {
      return query === '(prefers-color-scheme: dark)' && prefersDark
    },
    media: query,
    addEventListener: (_type: string, listener: (event: MediaQueryListEvent) => void) => {
      listeners.add(listener)
    },
    removeEventListener: (_type: string, listener: (event: MediaQueryListEvent) => void) => {
      listeners.delete(listener)
    },
    addListener: (listener: (event: MediaQueryListEvent) => void) => {
      listeners.add(listener)
    },
    removeListener: (listener: (event: MediaQueryListEvent) => void) => {
      listeners.delete(listener)
    },
  }))
  localStorage.clear()
  document.documentElement.className = ''
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderSwitch(lng?: 'en' | 'lt') {
  const user = userEvent.setup()
  render(
    <I18nextProvider i18n={createI18n({ lng })}>
      <AppThemeProvider>
        <ModeSwitch />
      </AppThemeProvider>
    </I18nextProvider>,
  )
  return user
}

async function choose(user: ReturnType<typeof userEvent.setup>, name: string) {
  await user.click(screen.getByRole('button'))
  await user.click(screen.getByRole('menuitemradio', { name }))
}

const html = document.documentElement

describe('ModeSwitch', () => {
  it('lists Light, Dark and Auto with Auto checked by default', async () => {
    const user = renderSwitch()
    await user.click(screen.getByRole('button', { name: 'Appearance' }))
    const items = screen.getAllByRole('menuitemradio')
    expect(items.map((item) => item.textContent)).toEqual(['Light', 'Dark', 'Auto'])
    expect(items.map((item) => item.getAttribute('aria-checked'))).toEqual([
      'false',
      'false',
      'true',
    ])
  })

  it('stores the chosen mode and sets the class on html', async () => {
    const user = renderSwitch()
    await choose(user, 'Dark')
    expect(localStorage.getItem('wattwise-mode')).toBe('dark')
    expect(html.classList.contains('dark')).toBe(true)

    await choose(user, 'Light')
    expect(localStorage.getItem('wattwise-mode')).toBe('light')
    expect(html.classList.contains('light')).toBe(true)
    expect(html.classList.contains('dark')).toBe(false)
  })

  it('follows the OS in Auto', () => {
    setPrefersDark(true)
    renderSwitch()
    expect(html.classList.contains('dark')).toBe(true)

    act(() => {
      setPrefersDark(false)
    })
    expect(html.classList.contains('light')).toBe(true)
    expect(html.classList.contains('dark')).toBe(false)
  })

  it('picks up a stored mode on mount', async () => {
    localStorage.setItem('wattwise-mode', 'dark')
    const user = renderSwitch()
    expect(html.classList.contains('dark')).toBe(true)
    await user.click(screen.getByRole('button'))
    expect(screen.getByRole('menuitemradio', { name: 'Dark' }).getAttribute('aria-checked')).toBe(
      'true',
    )
  })

  it('is translated to Lithuanian', async () => {
    const user = renderSwitch('lt')
    await user.click(screen.getByRole('button', { name: 'Išvaizda' }))
    expect(screen.getAllByRole('menuitemradio').map((item) => item.textContent)).toEqual([
      'Šviesi',
      'Tamsi',
      'Automatinė',
    ])
  })
})
