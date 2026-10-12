import { getContrastRatio } from '@mui/material/styles'
import { describe, expect, it } from 'vitest'
import { theme } from './theme'

const schemes = ['light', 'dark'] as const
const roles = ['primary', 'secondary', 'success', 'error', 'warning', 'info'] as const
const backgrounds = ['default', 'paper'] as const
const texts = ['primary', 'secondary'] as const

const expected = {
  light: {
    primary: '#5235D8',
    secondary: '#3A4252',
    success: '#1F7A2E',
    error: '#C62828',
    warning: '#A84B00',
    info: '#0369A1',
    backgroundDefault: '#F8F7FD',
    backgroundPaper: '#FFFFFF',
    textPrimary: '#0D0E14',
    textSecondary: '#4A4D5C',
    divider: '#DEDBEE',
  },
  dark: {
    primary: '#B0A6FF',
    secondary: '#D5DAE5',
    success: '#6FD67A',
    error: '#FF6B63',
    warning: '#FFB74D',
    info: '#4FC3F7',
    backgroundDefault: '#0D0C15',
    backgroundPaper: '#171521',
    textPrimary: '#F5F6FA',
    textSecondary: '#B4B8C8',
    divider: '#2F2C42',
  },
}

const minimum = 4.5

describe('theme palette', () => {
  it.each(schemes)('%s: keeps the agreed token values', (scheme) => {
    const p = theme.colorSchemes[scheme]?.palette
    expect(p).toBeDefined()
    if (!p) return
    const actual = {
      primary: p.primary.main,
      secondary: p.secondary.main,
      success: p.success.main,
      error: p.error.main,
      warning: p.warning.main,
      info: p.info.main,
      backgroundDefault: p.background.default,
      backgroundPaper: p.background.paper,
      textPrimary: p.text.primary,
      textSecondary: p.text.secondary,
      divider: p.divider,
    }
    const lower = (values: Record<string, string>) =>
      Object.fromEntries(Object.entries(values).map(([k, v]) => [k, v.toLowerCase()]))
    expect(lower(actual)).toEqual(lower(expected[scheme]))
  })

  const roleCases = schemes.flatMap((scheme) =>
    roles.flatMap((role) => backgrounds.map((bg) => [scheme, role, bg] as const)),
  )
  it.each(roleCases)(
    '%s: %s.main on background.%s has contrast of at least 4.5',
    (scheme, role, bg) => {
      const p = theme.colorSchemes[scheme]?.palette
      if (!p) throw new Error(`missing ${scheme} scheme`)
      expect(getContrastRatio(p[role].main, p.background[bg])).toBeGreaterThanOrEqual(minimum)
    },
  )

  const ownCases = schemes.flatMap((scheme) => roles.map((role) => [scheme, role] as const))
  it.each(ownCases)(
    '%s: %s.main against its own contrastText has contrast of at least 4.5',
    (scheme, role) => {
      const p = theme.colorSchemes[scheme]?.palette
      if (!p) throw new Error(`missing ${scheme} scheme`)
      expect(getContrastRatio(p[role].main, p[role].contrastText)).toBeGreaterThanOrEqual(minimum)
    },
  )

  const textCases = schemes.flatMap((scheme) =>
    texts.flatMap((text) => backgrounds.map((bg) => [scheme, text, bg] as const)),
  )
  it.each(textCases)(
    '%s: text.%s on background.%s has contrast of at least 4.5',
    (scheme, text, bg) => {
      const p = theme.colorSchemes[scheme]?.palette
      if (!p) throw new Error(`missing ${scheme} scheme`)
      expect(getContrastRatio(p.text[text], p.background[bg])).toBeGreaterThanOrEqual(minimum)
    },
  )
})
