import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { cssFor, renderWithComponents, resetThemeState } from '../testing/theme-test-utils'
import GlowSwitch from './glow-switch'

afterEach(() => {
  resetThemeState()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

const html = document.documentElement

const closestLabel = (element: Element) => {
  const found = element.closest('label')
  if (found === null) throw new Error('Missing label ancestor')
  return found
}

const text = {
  en: { name: 'Glow effects', word: 'Glow' },
  lt: { name: 'Švytėjimas (efektai)', word: 'Švytėjimas' },
} as const

describe.each(['en', 'lt'] as const)('GlowSwitch in %s', (lng) => {
  const t = text[lng]
  const renderSwitch = (width?: number) => renderWithComponents(<GlowSwitch />, { lng, width })

  it('is a switch with the translated name', () => {
    renderSwitch()
    expect(screen.getByRole('switch', { name: t.name })).toBeDefined()
  })

  it('has an accessible name that contains the visible word (WCAG 2.5.3)', () => {
    renderSwitch()
    const visible = screen.getByText(t.word).textContent
    expect(screen.getByRole('switch').getAttribute('aria-label')).toContain(visible)
  })

  it('styles the root as an inline flex row with a pointer cursor and a gap', () => {
    renderSwitch()
    const css = cssFor(closestLabel(screen.getByText(t.word)))
    expect(css).toContain('display:inline-flex')
    expect(css).toContain('align-items:center')
    expect(css).toContain('cursor:pointer')
    expect(css).toContain('gap:calc(0.5 * var(--mui-spacing))')
  })

  it('colours the icon and the word with text.secondary', () => {
    const { container } = renderSwitch()
    const icon = container.querySelector('svg[data-testid="LightbulbRoundedIcon"]')
    if (icon === null) throw new Error('Missing icon')
    expect(cssFor(icon)).toContain('color:var(--mui-palette-text-secondary')
    expect(cssFor(screen.getByText(t.word))).toContain('color:var(--mui-palette-text-secondary')
  })

  it('starts checked when <html> has no data-glow', () => {
    renderSwitch()
    expect(screen.getByRole('switch', { checked: true })).toBeDefined()
  })

  it('starts unchecked when <html> has data-glow="off"', () => {
    html.setAttribute('data-glow', 'off')
    renderSwitch()
    expect(screen.getByRole('switch', { checked: false })).toBeDefined()
  })

  it('turns off on click: unchecked, stored off, data-glow set', async () => {
    renderSwitch()
    await userEvent.click(screen.getByRole('switch', { name: t.name }))
    expect(screen.getByRole('switch', { name: t.name, checked: false })).toBeDefined()
    expect(localStorage.getItem('wattwise-glow')).toBe('off')
    expect(html.getAttribute('data-glow')).toBe('off')
  })

  it('turns on again on a second click: checked, stored on, data-glow removed', async () => {
    renderSwitch()
    await userEvent.click(screen.getByRole('switch', { name: t.name }))
    await userEvent.click(screen.getByRole('switch', { name: t.name }))
    expect(screen.getByRole('switch', { name: t.name, checked: true })).toBeDefined()
    expect(localStorage.getItem('wattwise-glow')).toBe('on')
    expect(html.getAttribute('data-glow')).toBeNull()
  })

  it('toggles with Space', async () => {
    renderSwitch()
    await userEvent.tab()
    expect(document.activeElement).toBe(screen.getByRole('switch', { name: t.name }))
    await userEvent.keyboard(' ')
    expect(screen.getByRole('switch', { name: t.name, checked: false })).toBeDefined()
    expect(html.getAttribute('data-glow')).toBe('off')
  })

  it('still toggles when storage throws', async () => {
    renderSwitch()
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    await userEvent.click(screen.getByRole('switch', { name: t.name }))
    expect(screen.getByRole('switch', { name: t.name, checked: false })).toBeDefined()
    expect(html.getAttribute('data-glow')).toBe('off')
  })

  it('hides the bulb icon from assistive technology', () => {
    const { container } = renderSwitch()
    const icon = container.querySelector('svg[data-testid="LightbulbRoundedIcon"]')
    expect(icon?.getAttribute('aria-hidden')).toBe('true')
  })

  it('hides the visible word below md and shows it from md (840px)', () => {
    renderSwitch()
    const word = screen.getByText(t.word)
    expect(cssFor(word)).toContain('display:none')
    expect(cssFor(word, '(min-width:840px)')).toContain('display:inline')
  })

  it.each([
    { state: 'on', width: 900 },
    { state: 'off', width: 900 },
    { state: 'on', width: 360 },
    { state: 'off', width: 360 },
  ])('has no accessibility violations when $state at $width px', async ({ state, width }) => {
    if (state === 'off') html.setAttribute('data-glow', 'off')
    const { container } = renderSwitch(width)
    expect((await axe(container, { rules: { region: { enabled: false } } })).violations).toEqual([])
  })
})
