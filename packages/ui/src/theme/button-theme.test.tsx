import Button from '@mui/material/Button'
import { afterEach, describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
import {
  colorModes,
  rootStyle,
  renderThemed,
  resetThemeState,
  variantStyle,
} from '../testing/theme-test-utils'

// The rendered text is a fixture, not translated UI, so these run language-neutral like
// alert-theme.test.tsx; the language-dependent states (form-group, switch, icon-menu) run in LT and EN.
afterEach(resetThemeState)

describe('button theme', () => {
  // Style-object cases read the theme's overrides; jsdom has no layout to measure.
  describe.each(colorModes)('in %s mode', (mode) => {
    it('has no accessibility violations for the contained variant', async () => {
      const { container } = renderThemed(<Button variant="contained">Save</Button>, mode)

      expect((await axe(container)).violations).toEqual([])
    })

    it('has no accessibility violations for the outlined variant', async () => {
      const { container } = renderThemed(<Button variant="outlined">Cancel</Button>, mode)

      expect((await axe(container)).violations).toEqual([])
    })
  })

  it('puts the action gradient and the halo on the contained primary button only', () => {
    const style = variantStyle(rootStyle('MuiButton'), { variant: 'contained', color: 'primary' })

    expect(String(style.backgroundImage).startsWith('var(--mui-gradient-action')).toBe(true)
    expect((style['&&:not(.Mui-disabled)'] as { boxShadow: string }).boxShadow).toMatch(
      /^var\(--mui-glow-halo/,
    )
  })

  it('leaves the contained primary halo off a disabled button', () => {
    const style = variantStyle(rootStyle('MuiButton'), { variant: 'contained', color: 'primary' })

    expect(style['&.Mui-disabled']).toEqual({ backgroundImage: 'none', boxShadow: 'none' })
  })

  it('gives no variant a gradient for another colour', () => {
    const variants = rootStyle('MuiButton').variants as {
      props: { color?: string }
      style: { backgroundImage?: string }
    }[]

    expect(
      variants
        .filter((variant) => variant.props.color !== undefined && variant.props.color !== 'primary')
        .map((variant) => variant.style.backgroundImage),
    ).toEqual([])
  })

  it('draws the contained primary button with a system border and no gradient or glow under forced colours', () => {
    const style = variantStyle(rootStyle('MuiButton'), { variant: 'contained', color: 'primary' })

    expect(style['@media (forced-colors: active)']).toEqual({
      backgroundImage: 'none',
      boxShadow: 'none',
      border: '1px solid ButtonBorder',
      '&&:not(.Mui-disabled)': { boxShadow: 'none' },
    })
  })

  it('outlines the outlined primary button 1.5px in the field outline colour', () => {
    const style = variantStyle(rootStyle('MuiButton'), { variant: 'outlined', color: 'primary' })

    expect(style).toMatchObject({
      borderWidth: 1.5,
      '--variant-outlinedBorder': 'var(--mui-palette-fieldOutline, #7D7A98)',
      '--variant-outlinedBg': 'transparent',
    })
  })

  it('keeps medium and large buttons six spacing units (48px) tall', () => {
    const root = rootStyle('MuiButton')

    expect([
      variantStyle(root, { size: 'medium' }).minHeight,
      variantStyle(root, { size: 'large' }).minHeight,
    ]).toEqual(['calc(6 * var(--mui-spacing, 8px))', 'calc(6 * var(--mui-spacing, 8px))'])
  })
})
