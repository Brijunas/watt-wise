import { decomposeColor, getContrastRatio } from '@mui/material/styles'
import { describe, expect, it } from 'vitest'
import { theme } from './theme'

const modes = ['light', 'dark'] as const
const statuses = ['success', 'error', 'warning'] as const
const action = 'linear-gradient(100deg, #0E7490, #5B21B6, #BE185D)'
const schemes = {
  light: {
    selector: ':root, .light',
    plain: '0 1px 2px rgba(18 15 36 / 0.06), 0 8px 24px -12px rgba(18 15 36 / 0.14)',
    tokens: {
      palette: {
        background: { default: '#F7F7FC', paper: '#FFFFFF' },
        surface2: '#F0EFF8',
        divider: '#E0DEEE',
        fieldOutline: '#7D7A98',
        text: { primary: '#120F24', secondary: '#56546E' },
        primary: { main: '#6D28D9' },
        secondary: { main: '#BE185D' },
        success: { main: '#047857', tint: '#E3F5EC' },
        error: { main: '#C62828', tint: 'rgba(var(--mui-palette-error-mainChannel) / 0.12)' },
        warning: { main: '#A84B00', tint: 'rgba(var(--mui-palette-warning-mainChannel) / 0.12)' },
      },
      gradient: { brand: 'linear-gradient(100deg, #0891B2, #6D28D9, #DB2777)', action },
      glow: {
        halo: '0 0 28px -6px rgba(0, 229, 255, 0.275), 0 0 56px -10px rgba(255, 43, 214, 0.225)',
        text: 'none',
        logo: 'drop-shadow(0 0 10px rgba(124, 77, 255, 0.25))',
        haloOff:
          '0 1px 2px rgba(var(--mui-palette-text-primaryChannel) / 0.06), 0 8px 24px -12px rgba(var(--mui-palette-text-primaryChannel) / 0.14)',
      },
    },
  },
  dark: {
    selector: '.dark',
    plain: '0 1px 2px rgba(0, 0, 0, 0.5)',
    tokens: {
      palette: {
        background: { default: '#05060E', paper: '#0D0F1F' },
        surface2: '#151832',
        divider: '#262A4D',
        fieldOutline: '#6B7096',
        text: { primary: '#F2F3FA', secondary: '#A9AECB' },
        primary: { main: '#A98BFF' },
        secondary: { main: '#FF2BD6', contrastText: '#05060E' },
        success: { main: '#34D399', tint: 'rgba(var(--mui-palette-success-mainChannel) / 0.14)' },
        error: { main: '#FF6B63', tint: 'rgba(var(--mui-palette-error-mainChannel) / 0.14)' },
        warning: { main: '#FFB74D', tint: 'rgba(var(--mui-palette-warning-mainChannel) / 0.14)' },
      },
      gradient: { brand: 'linear-gradient(100deg, #00E5FF, #7C4DFF, #FF2BD6)', action },
      glow: {
        halo: '0 0 28px -6px rgba(0, 229, 255, 0.55), 0 0 56px -10px rgba(255, 43, 214, 0.45)',
        text: 'drop-shadow(0 0 18px rgba(124, 77, 255, 0.6))',
        logo: 'drop-shadow(0 0 10px rgba(124, 77, 255, 0.5))',
        haloOff: '0 1px 2px rgba(0, 0, 0, 0.5)',
      },
    },
  },
}

it.each(modes)('%s implements the design token values', (mode) => {
  expect(theme.colorSchemes[mode]).toMatchObject(schemes[mode].tokens)
})

it('uses the documented radius, spacing and breakpoint scales', () => {
  expect(theme.shape.radius).toEqual({
    hero: '24px',
    card: '20px',
    panel: '16px',
    button: '14px',
    field: '12px',
    chip: '9999px',
  })
  expect(theme.generateStyleSheets().find((sheet) => ':root' in sheet)).toMatchObject({
    ':root': { '--mui-spacing': '8px' },
  })
  expect(theme.breakpoints.values).toEqual({
    xs: 0,
    sm: 600,
    md: 840,
    lg: 1200,
    xl: 1600,
    xxl: 2400,
  })
})

const mobile = '@media (max-width:599.95px)'
const amount = { fontWeight: 800, fontVariantNumeric: 'tabular-nums' }
it('sets the Red Hat Display type scale and compact sizes', () => {
  expect(theme.typography).toMatchObject({
    fontFamily: "'Red Hat Display Variable', sans-serif",
    h1: { fontSize: '2.125rem', fontWeight: 800, [mobile]: { fontSize: '1.625rem' } },
    h2: { fontSize: '1.3125rem', fontWeight: 700 },
    h3: { fontSize: '1.0625rem', fontWeight: 700 },
    body1: { fontSize: '1rem', fontWeight: 400, lineHeight: 1.6 },
    body2: { fontSize: '0.9375rem', fontWeight: 400 },
    button: { fontSize: '1rem', fontWeight: 700, textTransform: 'none' },
    overline: {
      fontSize: '0.8125rem',
      fontWeight: 700,
      letterSpacing: '0.1em',
      textTransform: 'uppercase',
    },
    hero: { ...amount, fontSize: '3.75rem', [mobile]: { fontSize: '2.75rem' } },
    amountCard: { ...amount, fontSize: '1.75rem' },
    amountInline: { ...amount, fontSize: '1.25rem' },
  })
})

// Serialize the custom properties with their actual selectors. Inserting the override
// first forces its specificity to win even when the scheme declarations come later.
function variableRules(sheet: object) {
  return Object.entries(sheet)
    .map(([selector, declarations]: [string, unknown]) => {
      if (!declarations || typeof declarations !== 'object') return ''
      const css = Object.entries(declarations)
        .filter(([name]) => name.startsWith('--mui-'))
        .map(([name, value]) => name + ': ' + String(value) + ';')
        .join('\n')
      return selector + ' {' + css + '}'
    })
    .join('\n')
}

it.each(modes)('%s glow switches to plain elevation on the document element', (mode) => {
  const overrides = theme.components?.MuiCssBaseline?.styleOverrides
  const off = typeof overrides === 'function' ? overrides(theme) : overrides
  if (!off || typeof off !== 'object') throw new Error('Missing glow override')
  const styles = document.createElement('style')
  const sheets = theme.generateStyleSheets()
  const { selector, plain } = schemes[mode]
  expect(sheets.find((sheet) => selector in sheet)).toMatchObject({
    [selector]: { '--mui-glow-haloOff': schemes[mode].tokens.glow.haloOff },
  })
  const offCSS = variableRules(off)
  expect(offCSS).toContain('html[data-glow="off"] {')
  expect(offCSS).toMatch(/--mui-glow-halo: var\(--mui-glow-haloOff(?:, [^;]+)?\);/)
  expect(offCSS).toContain('--mui-glow-text: none;')
  expect(offCSS).toContain('--mui-glow-logo: none;')
  styles.textContent = [offCSS, ...sheets.map(variableRules)].join('\n')
  const root = document.documentElement
  root.classList.add(mode)
  document.head.append(styles)
  try {
    const glow = schemes[mode].tokens.glow
    const on = getComputedStyle(root)
    expect(on.getPropertyValue('--mui-glow-halo')).toBe(glow.halo)
    expect(on.getPropertyValue('--mui-glow-text')).toBe(glow.text)
    expect(on.getPropertyValue('--mui-glow-logo')).toBe(glow.logo)
    root.dataset.glow = 'off'
    const disabled = getComputedStyle(root)
    expect(disabled.getPropertyValue('--mui-glow-haloOff')).toBe(plain)
    expect(disabled.getPropertyValue('--mui-glow-halo')).toBe(plain)
    expect(disabled.getPropertyValue('--mui-glow-text')).toBe('none')
    expect(disabled.getPropertyValue('--mui-glow-logo')).toBe('none')
  } finally {
    root.classList.remove(mode)
    delete root.dataset.glow
    styles.remove()
  }
})

function tintOverPaper(tint: string, main: string, paper: string) {
  if (tint.startsWith('#')) return tint
  const opacity = /\s\/ (0\.\d+)\)$/.exec(tint)
  if (!opacity) throw new Error('Missing tint opacity')
  const alpha = Number(opacity[1])
  const values = decomposeColor(main).values
  const background = decomposeColor(paper).values
  const channels = background.map((channel, index) => {
    const foreground = values[index]
    if (foreground === undefined) throw new Error('Missing tint channel')
    return Math.round(foreground * alpha + channel * (1 - alpha))
  })
  return 'rgb(' + channels.join(', ') + ')'
}

describe.each(modes)('%s contrast', (mode) => {
  const scheme = theme.colorSchemes[mode]
  if (!scheme) throw new Error('Missing ' + mode + ' scheme')
  const p = scheme.palette
  const foregrounds = [
    ['text.primary', p.text.primary, 4.5],
    ['text.secondary', p.text.secondary, 4.5],
    ['success', p.success.main, 4.5],
    ['warning', p.warning.main, 4.5],
    ['error', p.error.main, 4.5],
    ['fieldOutline', p.fieldOutline, 3],
    ['primary', p.primary.main, 3],
    ['secondary', p.secondary.main, 3],
  ] as const
  describe.each([
    ['page', p.background.default],
    ['paper', p.background.paper],
    ['inset', p.surface2],
  ])('%s surface', (_name, surface) => {
    it.each(foregrounds)('%s (%s) meets %d:1', (_role, color, minimum) => {
      expect(getContrastRatio(color, surface)).toBeGreaterThanOrEqual(minimum)
    })
  })
  it.each(['primary', 'secondary', ...statuses] as const)('%s fill supports its label', (role) => {
    expect(getContrastRatio(p[role].contrastText, p[role].main)).toBeGreaterThanOrEqual(4.5)
  })
  it.each(statuses)('%s text on its tint over paper meets 4.5:1', (role) => {
    const { main, tint } = p[role]
    if (!tint) throw new Error('Missing ' + role + ' tint')
    expect(
      getContrastRatio(main, tintOverPaper(tint, main, p.background.paper)),
    ).toBeGreaterThanOrEqual(4.5)
  })
})
