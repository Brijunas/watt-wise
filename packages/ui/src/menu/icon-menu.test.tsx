import { cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import IconMenu from './icon-menu'

afterEach(cleanup)

function renderMenu() {
  const onSelect = vi.fn()
  const user = userEvent.setup()
  render(
    <IconMenu
      label="Pick"
      icon={<span>icon</span>}
      options={[
        { key: 'a', selected: true, content: 'Alpha', lang: 'en' },
        { key: 'b', selected: false, content: 'Beta', lang: 'lt' },
      ]}
      onSelect={onSelect}
    />,
  )
  return { user, onSelect }
}

describe('IconMenu', () => {
  it('starts closed', () => {
    renderMenu()
    const button = screen.getByRole('button', { name: 'Pick' })
    expect(button.getAttribute('aria-haspopup')).toBe('menu')
    expect(button.getAttribute('aria-expanded')).toBe('false')
    expect(button.hasAttribute('aria-controls')).toBe(false)
    expect(screen.queryByRole('menu')).toBeNull()
  })

  it('opens a menu the button controls, with radio items', async () => {
    const { user } = renderMenu()
    await user.click(screen.getByRole('button', { name: 'Pick' }))

    const button = screen.getByRole('button', { name: 'Pick', hidden: true })
    expect(button.getAttribute('aria-expanded')).toBe('true')
    const menuId = button.getAttribute('aria-controls')
    expect(menuId).toBeTruthy()
    expect(document.getElementById(menuId ?? '')?.contains(screen.getByRole('menu'))).toBe(true)

    const items = screen.getAllByRole('menuitemradio')
    expect(items.map((item) => item.textContent)).toEqual(['Alpha', 'Beta'])
    expect(items.map((item) => item.getAttribute('aria-checked'))).toEqual(['true', 'false'])
    expect(items.map((item) => item.getAttribute('lang'))).toEqual(['en', 'lt'])
  })

  it('calls onSelect with the key and closes', async () => {
    const { user, onSelect } = renderMenu()
    await user.click(screen.getByRole('button', { name: 'Pick' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Beta' }))

    expect(onSelect).toHaveBeenCalledExactlyOnceWith('b')
    await vi.waitFor(() => {
      expect(screen.queryByRole('menu')).toBeNull()
    })
    expect(screen.getByRole('button', { name: 'Pick' }).getAttribute('aria-expanded')).toBe('false')
  })

  it('closes on Escape without selecting', async () => {
    const { user, onSelect } = renderMenu()
    await user.click(screen.getByRole('button', { name: 'Pick' }))
    await user.keyboard('{Escape}')

    await vi.waitFor(() => {
      expect(screen.queryByRole('menu')).toBeNull()
    })
    expect(onSelect).not.toHaveBeenCalled()
  })

  it('returns focus to the button after Escape', async () => {
    const { user } = renderMenu()
    await user.click(screen.getByRole('button', { name: 'Pick' }))
    await user.keyboard('{Escape}')

    await vi.waitFor(() => {
      expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Pick' }))
    })
  })

  it('returns focus to the button after a selection', async () => {
    const { user } = renderMenu()
    await user.click(screen.getByRole('button', { name: 'Pick' }))
    await user.click(screen.getByRole('menuitemradio', { name: 'Beta' }))

    await vi.waitFor(() => {
      expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Pick' }))
    })
  })

  describe.each([
    { language: 'en', label: 'Appearance', names: ['Light', 'Dark'] },
    { language: 'lt', label: 'Išvaizda', names: ['Šviesi', 'Tamsi'] },
  ])('in $language', ({ label, names }) => {
    const open = async () => {
      const user = userEvent.setup()
      const view = render(
        <IconMenu
          label={label}
          icon={<span>icon</span>}
          options={[
            { key: 'a', selected: false, content: names[0] },
            { key: 'b', selected: true, content: names[1] },
          ]}
          onSelect={vi.fn()}
        />,
      )
      await user.click(screen.getByRole('button', { name: label }))
      return view
    }

    it('shows the tick on the selected item only', async () => {
      await open()

      const [first, second] = screen.getAllByRole('menuitemradio')

      expect([
        within(first ?? document.body).queryAllByTestId('CheckRoundedIcon').length,
        within(second ?? document.body).queryAllByTestId('CheckRoundedIcon').length,
      ]).toEqual([0, 1])
    })

    it('hides the tick from assistive technology', async () => {
      await open()

      expect(screen.getByTestId('CheckRoundedIcon').getAttribute('aria-hidden')).toBe('true')
    })

    it('keeps the item names and checked state unchanged by the tick', async () => {
      await open()

      const items = screen.getAllByRole('menuitemradio')

      expect(items.map((item) => item.getAttribute('aria-checked'))).toEqual(['false', 'true'])
      expect(screen.getAllByRole('menuitemradio', { name: names[1] })).toHaveLength(1)
    })

    it('has no accessibility violations with the menu open', async () => {
      await open()

      // The open menu is a portal fragment, not a page: the landmark rule doesn't apply.
      const results = await axe(document.body, { rules: { region: { enabled: false } } })

      expect(results.violations).toEqual([])
    })
  })
})
