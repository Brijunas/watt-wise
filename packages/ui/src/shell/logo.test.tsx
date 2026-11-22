import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type { AnchorHTMLAttributes } from 'react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import {
  colorModes,
  cssFor,
  emittedCss,
  renderWithComponents,
  resetThemeState,
} from '../testing/theme-test-utils'
import Logo from './logo'

afterEach(() => {
  resetThemeState()
  vi.unstubAllGlobals()
})

const TestLink = ({ to, ...rest }: { to: string } & AnchorHTMLAttributes<HTMLAnchorElement>) => (
  <a href={to} {...rest} />
)

const text = {
  en: { name: 'Watt-Wise home' },
  lt: { name: 'Watt-Wise pradžia' },
} as const

const forced = '(forced-colors: active)'

const generatedClass = (element: Element) => {
  const found = Array.from(element.classList).find((name) => name.startsWith('css-'))
  if (found === undefined) throw new Error('Missing generated class')
  return found
}

// The element is found by its class suffix, so the test reads the real markup.
const part = (container: HTMLElement, slot: string) => {
  const found = Array.from(container.querySelectorAll('img, span')).find((element) =>
    Array.from(element.classList).some((name) => name.endsWith('-WwLogo-' + slot)),
  )
  if (found === undefined) throw new Error('Missing ' + slot)
  return found
}

describe.each(['en', 'lt'] as const)('Logo in %s', (lng) => {
  const t = text[lng]
  const renderLogo = (options: { mode?: 'light' | 'dark'; width?: number } = {}) =>
    renderWithComponents(<Logo linkComponent={TestLink} to="/" />, { lng, ...options })

  it('is a link to / named by the translation', () => {
    renderLogo()
    expect(screen.getByRole('link', { name: t.name }).getAttribute('href')).toBe('/')
  })

  it('renders the light and dark lockups as decorative images with the file size', () => {
    const { container } = renderLogo()
    const images = Array.from(container.querySelectorAll('img')).map((img) => [
      img.getAttribute('src'),
      img.getAttribute('alt'),
      img.getAttribute('width'),
      img.getAttribute('height'),
    ])
    expect(images).toEqual([
      ['/logo-lockup-light.png', '', '1908', '606'],
      ['/logo-lockup-dark.png', '', '1908', '606'],
    ])
  })

  it('hides the mono mark and the wordmark from assistive technology', () => {
    const { container } = renderLogo()
    expect([
      part(container, 'mono').getAttribute('aria-hidden'),
      part(container, 'wordmark').getAttribute('aria-hidden'),
    ]).toEqual(['true', 'true'])
  })

  it('spells the wordmark WATT over WISE', () => {
    const { container } = renderLogo()
    expect(
      Array.from(part(container, 'wordmark').children).map((child) => child.textContent),
    ).toEqual(['WATT', 'WISE'])
  })

  it.each(['lightLockup', 'darkLockup'])(
    'glows the %s with the logo token and a 250ms filter transition',
    (slot) => {
      const { container } = renderLogo()
      const css = cssFor(part(container, slot))
      expect(css).toContain('filter:var(--mui-glow-logo)')
      expect(css).toContain('transition:filter 250ms')
    },
  )

  it('shows only the light lockup in light mode', () => {
    const { container } = renderLogo({ mode: 'light' })
    expect([
      getComputedStyle(part(container, 'lightLockup')).display === 'none',
      getComputedStyle(part(container, 'darkLockup')).display === 'none',
    ]).toEqual([false, true])
  })

  it('shows only the dark lockup in dark mode', () => {
    const { container } = renderLogo({ mode: 'dark' })
    expect([
      getComputedStyle(part(container, 'lightLockup')).display === 'none',
      getComputedStyle(part(container, 'darkLockup')).display === 'none',
    ]).toEqual([true, false])
  })

  // `cssFor` without a media query returns only the rules outside any `@media` block.
  it.each(['mono', 'wordmark'])('hides the %s outside forced colours', (slot) => {
    const { container } = renderLogo()
    expect(cssFor(part(container, slot))).toBe(
      '.' + generatedClass(part(container, slot)) + '{display:none;}',
    )
  })

  it.each(['lightLockup', 'darkLockup'])('sizes the %s with an automatic width', (slot) => {
    const { container } = renderLogo()
    expect(cssFor(part(container, slot))).toContain('width:auto;')
  })

  it('styles the link as a centred inline flex row without text decoration', () => {
    renderLogo()
    const css = cssFor(screen.getByRole('link', { name: t.name }))
    expect(css).toContain('display:inline-flex;')
    expect(css).toContain('align-items:center;')
    expect(css).toContain('color:inherit;')
    expect(css).toContain('text-decoration:none;')
    expect(css).toContain('gap:var(--mui-spacing);')
    expect(css).toContain('margin-inline-end:calc(1.5 * var(--mui-spacing));')
  })

  it('focuses the link with the keyboard and keeps the focus outline', async () => {
    renderLogo()
    await userEvent.tab()
    expect(document.activeElement).toBe(screen.getByRole('link', { name: t.name }))
    expect(cssFor(screen.getByRole('link', { name: t.name }))).not.toContain('outline')
  })

  it.each(['lightLockup', 'darkLockup'])(
    'hides the %s and drops its glow under forced colours',
    (slot) => {
      const { container } = renderLogo()
      expect(cssFor(part(container, slot), forced)).toContain('display:none;')
      expect(cssFor(part(container, slot), forced)).toContain('filter:none')
    },
  )

  it.each(['lightLockup', 'darkLockup'])(
    'puts the %s forced-colours rule after its dark-mode display rule',
    (slot) => {
      const { container } = renderLogo()
      const name = generatedClass(part(container, slot))
      const css = emittedCss()
      expect(
        css.indexOf('@media (forced-colors: active){.' + name + '{display:none'),
      ).toBeGreaterThan(css.indexOf(':where(.dark) .' + name + '{display:'))
    },
  )

  it('draws the mono mark as a CanvasText mask of the mono SVG under forced colours', () => {
    const { container } = renderLogo()
    const css = cssFor(part(container, 'mono'), forced)
    expect(css).toContain('display:inline-block;')
    expect(css).toContain('aspect-ratio:871/745;')
    expect(css).toContain('mask:url(/logo-mark-mono.svg) center/contain no-repeat;')
    expect(css).toContain('background-color:CanvasText;')
    expect(css).toContain('forced-color-adjust:none;')
  })

  it('draws the wordmark as a CanvasText column of WATT over WISE under forced colours', () => {
    const { container } = renderLogo()
    const css = cssFor(part(container, 'wordmark'), forced)
    expect(css).toContain('display:inline-flex;')
    expect(css).toContain('flex-direction:column;')
    expect(css).toContain('justify-content:center;')
    expect(css).toContain('color:CanvasText;')
    expect(css).toContain('line-height:1;')
  })

  it('sets WATT bold 800 with wide letter spacing and WISE 700', () => {
    const { container } = renderLogo()
    const wordmark = part(container, 'wordmark')
    const css = cssFor(wordmark, forced)
    const name = generatedClass(wordmark)
    expect(css).toContain('.' + name + '>:first-of-type{font-weight:800;letter-spacing:0.14em;}')
    expect(css).toContain('.' + name + '>:last-of-type{font-weight:700;}')
  })

  it('sizes the wordmark text 12.8px by default under forced colours', () => {
    const { container } = renderLogo()
    expect(cssFor(part(container, 'wordmark'), forced)).toContain('line-height:1;font-size:12.8px;')
  })

  it.each([
    { query: '(min-width:360px)', size: '14.4px' },
    { query: '(min-width:400px)', size: '16px' },
    { query: '(min-width:600px)', size: '19.2' },
  ])('sizes the wordmark text $size from $query under forced colours', ({ query, size }) => {
    const { container } = renderLogo()
    const wordmark = part(container, 'wordmark')
    expect(cssFor(wordmark, forced)).toContain(
      `@media ${query}{.${generatedClass(wordmark)}{font-size:${size}`,
    )
  })

  it('sizes the lockups 32px by default', () => {
    const { container } = renderLogo()
    expect(cssFor(part(container, 'lightLockup'))).toContain('height:32px;')
    expect(cssFor(part(container, 'darkLockup'))).toContain('height:32px;')
  })

  it.each([
    { query: '(min-width:360px)', rule: 'height:36px;' },
    { query: '(min-width:400px)', rule: 'height:40px;' },
    { query: '(min-width:600px)', rule: 'height:48px;' },
  ])('sizes the lockups with $rule from $query', ({ query, rule }) => {
    const { container } = renderLogo()
    expect([
      cssFor(part(container, 'lightLockup'), query),
      cssFor(part(container, 'darkLockup'), query),
    ]).toEqual([expect.stringContaining(rule) as unknown, expect.stringContaining(rule) as unknown])
  })

  it('sizes the mono mark 32px by default under forced colours', () => {
    const { container } = renderLogo()
    expect(cssFor(part(container, 'mono'), forced)).toContain(
      'forced-color-adjust:none;height:32px;',
    )
  })

  it.each([
    { query: '(min-width:360px)', height: '36px' },
    { query: '(min-width:400px)', height: '40px' },
    { query: '(min-width:600px)', height: '48px' },
  ])('sizes the mono mark $height from $query under forced colours', ({ query, height }) => {
    const { container } = renderLogo()
    const mono = part(container, 'mono')
    expect(cssFor(mono, forced)).toContain(
      `@media ${query}{.${generatedClass(mono)}{height:${height};}}`,
    )
  })

  it.each(colorModes)('has no accessibility violations in %s mode', async (mode) => {
    const { container } = renderLogo({ mode })
    expect((await axe(container, { rules: { region: { enabled: false } } })).violations).toEqual([])
  })
})
