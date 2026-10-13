import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { IconMenu } from './icon-menu'

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
})
