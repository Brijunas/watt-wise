import AppBar from '@mui/material/AppBar'
import Toolbar from '@mui/material/Toolbar'
import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
import { colorModes, cssFor, renderThemed, resetThemeState } from '../testing/theme-test-utils'

// The bar's text is a language-neutral fixture; the AppShell with its switches is axe-checked in
// ww-components.test.tsx in LT and EN.
afterEach(resetThemeState)

const renderBar = (mode: 'light' | 'dark', props: { color?: 'primary' } = {}) => {
  const result = renderThemed(
    <AppBar position="static" {...props}>
      <Toolbar>Watt-Wise</Toolbar>
    </AppBar>,
    mode,
  )
  return { ...result, bar: screen.getByRole('banner') }
}

describe.each(colorModes)('app bar theme in %s mode', (mode) => {
  it('fills the bar with the paper background', () => {
    const { bar } = renderBar(mode)
    expect(cssFor(bar)).toContain('background-color:var(--mui-palette-background-paper')
  })

  it('does not fill the bar with the primary colour', () => {
    const { bar } = renderBar(mode)
    expect(cssFor(bar)).not.toContain('--mui-palette-primary-main')
  })

  it('clears the background image (the dark elevation overlay)', () => {
    const { bar } = renderBar(mode)
    expect(cssFor(bar)).toContain('background-image:none')
  })

  it('uses the primary text colour', () => {
    const { bar } = renderBar(mode)
    expect(cssFor(bar)).toContain('color:var(--mui-palette-text-primary')
  })

  it('has no shadow', () => {
    const { bar } = renderBar(mode)
    expect(cssFor(bar)).toContain('box-shadow:none')
  })

  it('draws a 1px solid divider along the bottom', () => {
    const { bar } = renderBar(mode)
    expect(cssFor(bar)).toContain('border-bottom:1px solid var(--mui-palette-divider')
  })

  it('defaults to the inherit colour', () => {
    const { bar } = renderBar(mode)
    expect(bar.classList.contains('MuiAppBar-colorInherit')).toBe(true)
  })

  it('has no accessibility violations', async () => {
    const { container } = renderBar(mode)
    expect((await axe(container, { rules: { region: { enabled: false } } })).violations).toEqual([])
  })
})

it('still lets a page ask for the primary colour', () => {
  const { bar } = renderBar('light', { color: 'primary' })
  expect(bar.classList.contains('MuiAppBar-colorPrimary')).toBe(true)
})
