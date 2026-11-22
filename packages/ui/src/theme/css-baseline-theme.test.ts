import { expect, it } from 'vitest'
import { theme } from './theme'

// A long unbroken string (the about page's version line) must wrap instead of widening the page.
it('lets a long unbroken string in the body wrap', () => {
  const overrides = theme.components?.MuiCssBaseline?.styleOverrides
  const resolved = typeof overrides === 'function' ? overrides(theme) : overrides
  if (resolved === undefined || typeof resolved !== 'object') {
    throw new Error('Missing CssBaseline overrides')
  }
  expect((resolved as Record<string, unknown>).body).toEqual({ overflowWrap: 'break-word' })
})
