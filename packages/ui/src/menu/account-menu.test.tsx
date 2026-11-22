import { languageStorageKey } from '@wattwise/i18n'
import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { AnchorHTMLAttributes } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { colorModes, renderWithComponents, resetThemeState } from '../testing/theme-test-utils'
import AccountMenu, { type AccountMenuItem } from './account-menu'

afterEach(() => {
  resetThemeState()
  vi.unstubAllGlobals()
  document.documentElement.lang = ''
})

function TestLink({ to, ...rest }: { to: string } & AnchorHTMLAttributes<HTMLAnchorElement>) {
  return <a href={to} {...rest} />
}

const text = {
  en: {
    label: 'Account',
    settings: 'Settings',
    signOut: 'Sign out',
    checked: ['true', 'false'],
    other: 'Lietuvių',
    otherCode: 'lt',
  },
  lt: {
    label: 'Paskyra',
    settings: 'Nustatymai',
    signOut: 'Atsijungti',
    checked: ['false', 'true'],
    other: 'English',
    otherCode: 'en',
  },
} as const

const languages = ['en', 'lt'] as const

const itemsFor = (lng: (typeof languages)[number], onSelect = vi.fn()): AccountMenuItem[] => [
  { key: 'settings', label: text[lng].settings, to: '/settings' },
  { key: 'sign-out', label: text[lng].signOut, onSelect },
]

const renderMenu = (
  lng: (typeof languages)[number],
  width: number,
  items: readonly AccountMenuItem[] = itemsFor(lng),
  mode?: 'light' | 'dark',
) => {
  const user = userEvent.setup()
  const view = renderWithComponents(
    <AccountMenu label={text[lng].label} linkComponent={TestLink} items={items} />,
    { lng, width, mode },
  )
  return { user, ...view }
}

describe.each(languages)('AccountMenu in %s', (lng) => {
  const t = text[lng]

  it('names the trigger by its label and starts collapsed', () => {
    renderMenu(lng, 900)
    const trigger = screen.getByRole('button', { name: t.label })
    expect([
      trigger.getAttribute('aria-haspopup'),
      trigger.getAttribute('aria-expanded'),
      trigger.getAttribute('aria-controls'),
    ]).toEqual(['menu', 'false', null])
  })

  it('points aria-controls at the open menu', async () => {
    const { user } = renderMenu(lng, 900)
    const trigger = screen.getByRole('button', { name: t.label })
    await user.click(trigger)
    expect(trigger.getAttribute('aria-expanded')).toBe('true')
    const controlled = document.getElementById(trigger.getAttribute('aria-controls') ?? '')
    expect(controlled?.contains(screen.getByRole('menu'))).toBe(true)
  })

  it('lists a link item and an action item at 900px, with no language', async () => {
    const { user } = renderMenu(lng, 900)
    await user.click(screen.getByRole('button', { name: t.label }))
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([
      t.settings,
      t.signOut,
    ])
    expect(screen.getByRole('menuitem', { name: t.settings }).getAttribute('href')).toBe(
      '/settings',
    )
    expect(screen.queryAllByRole('menuitemradio')).toHaveLength(0)
    expect(screen.queryAllByRole('separator')).toHaveLength(0)
  })

  it('has no language items at 600px', async () => {
    const { user } = renderMenu(lng, 600)
    await user.click(screen.getByRole('button', { name: t.label }))
    expect(screen.queryAllByRole('menuitemradio')).toHaveLength(0)
  })

  it('adds a separator and the language below 600px', async () => {
    const { user } = renderMenu(lng, 599)
    await user.click(screen.getByRole('button', { name: t.label }))
    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([
      t.settings,
      t.signOut,
    ])
    expect(screen.getAllByRole('separator')).toHaveLength(1)
    const radios = screen.getAllByRole('menuitemradio')
    expect(radios.map((item) => item.textContent)).toEqual(['English', 'Lietuvių'])
    expect(radios.map((item) => item.getAttribute('lang'))).toEqual(['en', 'lt'])
    expect(radios.map((item) => item.getAttribute('aria-checked'))).toEqual(t.checked)
  })

  it('shows only the language below 600px when there are no items', async () => {
    const { user } = renderMenu(lng, 360, [])
    await user.click(screen.getByRole('button', { name: t.label }))
    expect(screen.queryAllByRole('menuitem')).toHaveLength(0)
    expect(screen.queryAllByRole('separator')).toHaveLength(0)
    expect(screen.getAllByRole('menuitemradio')).toHaveLength(2)
  })

  it('renders nothing from 600px up when there are no items', () => {
    const { container } = renderMenu(lng, 900, [])
    expect(container.innerHTML).toBe('')
  })

  it('switches the language by choosing a language item', async () => {
    const { user } = renderMenu(lng, 360)
    await user.click(screen.getByRole('button', { name: t.label }))
    await user.click(screen.getByRole('menuitemradio', { name: t.other }))
    expect(localStorage.getItem(languageStorageKey)).toBe(t.otherCode)
    await waitFor(() => {
      expect(document.documentElement.lang).toBe(t.otherCode)
    })
  })

  it('puts the initial focus on the first app item, not the ticked language, at 360px', async () => {
    const { user } = renderMenu(lng, 360)
    await user.click(screen.getByRole('button', { name: t.label }))
    expect(document.activeElement).toBe(screen.getByRole('menuitem', { name: t.settings }))
  })

  it('puts the initial focus on the first language item with no items at 360px', async () => {
    const { user } = renderMenu(lng, 360, [])
    await user.click(screen.getByRole('button', { name: t.label }))
    expect(document.activeElement).toBe(screen.getAllByRole('menuitemradio')[0])
  })

  it('closes the menu and returns focus to the trigger after choosing a language', async () => {
    const { user } = renderMenu(lng, 360)
    const trigger = screen.getByRole('button', { name: t.label })
    await user.click(trigger)
    await user.click(screen.getByRole('menuitemradio', { name: 'English' }))
    await waitFor(() => {
      expect(screen.queryAllByRole('menu')).toHaveLength(0)
    })
    expect(document.activeElement).toBe(trigger)
    expect(trigger.getAttribute('aria-expanded')).toBe('false')
  })

  it('runs an action item once and closes the menu', async () => {
    const onSelect = vi.fn()
    const { user } = renderMenu(lng, 900, itemsFor(lng, onSelect))
    await user.click(screen.getByRole('button', { name: t.label }))
    await user.click(screen.getByRole('menuitem', { name: t.signOut }))
    expect(onSelect).toHaveBeenCalledTimes(1)
    await waitFor(() => {
      expect(screen.queryAllByRole('menu')).toHaveLength(0)
    })
  })

  it('closes on a link item and runs no action', async () => {
    const onSelect = vi.fn()
    const { user } = renderMenu(lng, 900, itemsFor(lng, onSelect))
    await user.click(screen.getByRole('button', { name: t.label }))
    await user.click(screen.getByRole('menuitem', { name: t.settings }))
    expect(onSelect).not.toHaveBeenCalled()
    await waitFor(() => {
      expect(screen.queryAllByRole('menu')).toHaveLength(0)
    })
  })

  it('returns focus to the trigger after Escape', async () => {
    const { user } = renderMenu(lng, 900)
    const trigger = screen.getByRole('button', { name: t.label })
    await user.click(trigger)
    await user.keyboard('{Escape}')
    await waitFor(() => {
      expect(screen.queryAllByRole('menu')).toHaveLength(0)
    })
    expect(document.activeElement).toBe(trigger)
  })

  describe.each(colorModes)('accessibility in %s mode', (mode) => {
    const rules = { region: { enabled: false } }

    it.each([360, 900])('has no violations closed at %ipx', async (width) => {
      const { container } = renderMenu(lng, width, undefined, mode)
      expect((await axe(container, { rules })).violations).toEqual([])
    })

    it.each([360, 900])('has no violations open at %ipx', async (width) => {
      const { user } = renderMenu(lng, width, undefined, mode)
      await user.click(screen.getByRole('button', { name: t.label }))
      expect((await axe(document.body, { rules })).violations).toEqual([])
    })
  })
})
