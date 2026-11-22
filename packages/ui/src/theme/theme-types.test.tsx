import Card from '@mui/material/Card'
import Chip from '@mui/material/Chip'
import Typography from '@mui/material/Typography'
import { expect, expectTypeOf, it } from 'vitest'
import { theme } from './theme'

const { vars } = theme
it.each([
  ['--mui-palette-surface2', vars.palette.surface2],
  ['--mui-palette-fieldOutline', vars.palette.fieldOutline],
  ['--mui-palette-success-tint', vars.palette.success.tint],
  ['--mui-palette-error-tint', vars.palette.error.tint],
  ['--mui-palette-warning-tint', vars.palette.warning.tint],
  ['--mui-gradient-brand', vars.gradient.brand],
  ['--mui-gradient-action', vars.gradient.action],
  ['--mui-glow-halo', vars.glow.halo],
  ['--mui-glow-hover', vars.glow.hover],
  ['--mui-glow-text', vars.glow.text],
  ['--mui-glow-logo', vars.glow.logo],
  ['--mui-glow-haloOff', vars.glow.haloOff],
  ['--mui-shape-radius-hero', vars.shape.radius.hero],
  ['--mui-shape-radius-card', vars.shape.radius.card],
  ['--mui-shape-radius-panel', vars.shape.radius.panel],
  ['--mui-shape-radius-button', vars.shape.radius.button],
  ['--mui-shape-radius-field', vars.shape.radius.field],
  ['--mui-shape-radius-chip', vars.shape.radius.chip],
  ['--mui-font-hero', vars.font.hero],
  ['--mui-font-amountCard', vars.font.amountCard],
  ['--mui-font-amountInline', vars.font.amountInline],
] as const)('references %s', (name, value) => {
  if (value === undefined) throw new Error('Missing CSS variable ' + name)
  expect(value.startsWith('var(' + name + ', ') || value === 'var(' + name + ')').toBe(true)
})

it('types the palette and typography at the theme root', () => {
  expectTypeOf(theme.palette.surface2).toEqualTypeOf<string>()
  expectTypeOf(theme.palette.fieldOutline).toEqualTypeOf<string>()
  expectTypeOf(theme.palette.success.tint).toEqualTypeOf<string | undefined>()
  expectTypeOf(theme.palette.error.tint).toEqualTypeOf<string | undefined>()
  expectTypeOf(theme.palette.warning.tint).toEqualTypeOf<string | undefined>()
  for (const variant of ['hero', 'amountCard', 'amountInline'] as const) {
    expectTypeOf(theme.typography[variant]).toEqualTypeOf<React.CSSProperties>()
    expectTypeOf(<Typography variant={variant} />).toEqualTypeOf<React.JSX.Element>()
  }
  // @ts-expect-error An undeclared gradient must not be accepted by the theme.
  expect(vars.gradient.undeclared).toBeUndefined()
  // @ts-expect-error Glow only exposes the documented effects.
  expect(vars.glow.undeclared).toBeUndefined()
  // @ts-expect-error Typography rejects undeclared variants.
  expectTypeOf(<Typography variant="undeclared" />)
})

it('types the highlighted card and the two chip variants', () => {
  expectTypeOf(<Card variant="highlighted" />).toEqualTypeOf<React.JSX.Element>()
  expectTypeOf(<Chip variant="lowestCost" />).toEqualTypeOf<React.JSX.Element>()
  expectTypeOf(<Chip variant="pastPrices" />).toEqualTypeOf<React.JSX.Element>()
  // @ts-expect-error Card rejects undeclared variants.
  expectTypeOf(<Card variant="undeclared" />)
  // @ts-expect-error Chip rejects undeclared variants.
  expectTypeOf(<Chip variant="undeclared" />)
})

it('accepts the wide breakpoint', () => {
  expect(theme.breakpoints.up('xxl')).toBe('@media (min-width:2400px)')
})
