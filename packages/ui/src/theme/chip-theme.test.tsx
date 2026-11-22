import Chip from '@mui/material/Chip'
import { screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
import {
  at,
  colorModes,
  renderThemed,
  resetThemeState,
  rootStyle,
  variantStyle,
} from '../testing/theme-test-utils'

// The rendered text is a fixture, not translated UI, so these run language-neutral like
// alert-theme.test.tsx; the language-dependent states (form-group, switch, icon-menu) run in LT and EN.
afterEach(resetThemeState)

describe('chip theme', () => {
  // Style-object cases read the theme's overrides; jsdom has no layout to measure.
  describe.each(colorModes)('in %s mode', (mode) => {
    it('has no accessibility violations for the default chip', async () => {
      const { container } = renderThemed(<Chip label="Tomorrow" />, mode)

      expect((await axe(container)).violations).toEqual([])
    })

    it('has no accessibility violations for the lowestCost chip', async () => {
      const { container } = renderThemed(<Chip variant="lowestCost" label="Lowest cost" />, mode)

      expect((await axe(container)).violations).toEqual([])
    })

    it('has no accessibility violations for the pastPrices chip', async () => {
      const { container } = renderThemed(<Chip variant="pastPrices" label="Past prices" />, mode)

      expect((await axe(container)).violations).toEqual([])
    })
  })

  it('renders both custom variants with a name', () => {
    renderThemed(
      <>
        <Chip variant="lowestCost" label="Lowest cost" />
        <Chip variant="pastPrices" label="Past prices" />
      </>,
    )

    expect([
      screen.getByText('Lowest cost').parentElement?.className.includes('MuiChip-lowestCost'),
      screen.getByText('Past prices').parentElement?.className.includes('MuiChip-pastPrices'),
    ]).toEqual([true, true])
  })

  it('fills the lowestCost chip with the action gradient and white text, without a glow', () => {
    const style = variantStyle(rootStyle('MuiChip'), { variant: 'lowestCost' })

    expect([style.backgroundColor, style.boxShadow, style.filter]).toEqual([
      'transparent',
      undefined,
      undefined,
    ])
    expect(String(style.backgroundImage).startsWith('var(--mui-gradient-action')).toBe(true)
    expect(String(style.color).startsWith('var(--mui-palette-common-white')).toBe(true)
  })

  it('drops the lowestCost gradient under forced colours', () => {
    const style = variantStyle(rootStyle('MuiChip'), { variant: 'lowestCost' })

    expect(style['@media (forced-colors: active)']).toEqual({ backgroundImage: 'none' })
  })

  it('tints the pastPrices chip 12% primary with primary text', () => {
    const style = variantStyle(rootStyle('MuiChip'), { variant: 'pastPrices' })

    expect(String(style.backgroundColor)).toMatch(
      /^rgba\(var\(--mui-palette-primary-mainChannel[^)]*\) \/ 0\.12\)$/,
    )
    expect(String(style.color).startsWith('var(--mui-palette-primary-main')).toBe(true)
  })

  it('gives the default colour the surface2 fill and secondary text', () => {
    const style = variantStyle(rootStyle('MuiChip'), { color: 'default' })

    expect(String(style.backgroundColor)).toBe('var(--mui-palette-surface2, #F0EFF8)')
    expect(String(style.color)).toBe('var(--mui-palette-text-secondary, #56546E)')
  })

  it('draws a system border on every chip under forced colours', () => {
    expect(at(rootStyle('MuiChip'), '@media (forced-colors: active)')).toEqual({
      border: '1px solid ButtonBorder',
    })
  })

  it('shows the CancelRounded icon as the delete icon, 24px', () => {
    renderThemed(<Chip label="Tomorrow" onDelete={() => undefined} />)

    const icon = within(screen.getByRole('button', { name: 'Tomorrow' })).getByTestId(
      'CancelRoundedIcon',
    )

    expect(icon.tagName).toBe('svg')
    expect(at(rootStyle('MuiChip'), '& .MuiChip-deleteIcon').fontSize).toBe('1.5rem')
  })

  it('keeps the lowestCost gradient with a transparent fill on a clickable chip hover and focus', () => {
    const style = variantStyle(rootStyle('MuiChip'), { variant: 'lowestCost' })

    expect(at(style, '&.MuiChip-clickable', '&:hover, &.Mui-focusVisible')).toEqual({
      backgroundColor: 'transparent',
    })
  })

  it('keeps the pastPrices 12% tint on a clickable chip hover and focus', () => {
    const style = variantStyle(rootStyle('MuiChip'), { variant: 'pastPrices' })

    expect(at(style, '&.MuiChip-clickable', '&:hover, &.Mui-focusVisible').backgroundColor).toMatch(
      /^rgba\(var\(--mui-palette-primary-mainChannel[^)]*\) \/ 0\.12\)$/,
    )
  })

  it('deepens the clickable pastPrices tint to 20% only inside the hover media query', () => {
    const clickable = at(
      variantStyle(rootStyle('MuiChip'), { variant: 'pastPrices' }),
      '&.MuiChip-clickable',
    )

    expect(
      at(clickable, '@media (hover: hover) and (pointer: fine)', '&:hover').backgroundColor,
    ).toMatch(/^rgba\(var\(--mui-palette-primary-mainChannel[^)]*\) \/ 0\.2\)$/)
  })

  it('has no accessibility violations for clickable variant chips', async () => {
    const { container } = renderThemed(
      <>
        <Chip variant="lowestCost" label="Lowest cost" onClick={() => undefined} />
        <Chip variant="pastPrices" label="Past prices" onClick={() => undefined} />
      </>,
    )

    expect((await axe(container)).violations).toEqual([])
  })

  it('colours the default chip delete icon text.secondary, text.primary on hover', () => {
    const icon = at(
      variantStyle(rootStyle('MuiChip'), { color: 'default' }),
      '& .MuiChip-deleteIcon',
    )

    expect([icon.color, at(icon, '&:hover').color]).toEqual([
      'var(--mui-palette-text-secondary, #56546E)',
      'var(--mui-palette-text-primary, #120F24)',
    ])
  })
})
