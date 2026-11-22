import Card from '@mui/material/Card'
import Chip from '@mui/material/Chip'
import type { Components } from '@mui/material/styles'
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

// Counts the entries so each case's object is used; the `@ts-expect-error` lines are the assertions.
const countKeys = (components: Components) => Object.keys(components).length

it('rejects an unknown slot key in a Ww component styleOverrides', () => {
  expect(
    countKeys({
      WwAppShell: {
        styleOverrides: {
          root: {},
          // @ts-expect-error AppShell has no `sidebar` slot.
          sidebar: {},
        },
      },
      WwIconMenu: {
        styleOverrides: {
          root: {},
          // @ts-expect-error IconMenu has no `popup` slot.
          popup: {},
        },
      },
      WwLanguageSwitch: {
        styleOverrides: {
          root: {},
          // @ts-expect-error LanguageSwitch only has the root slot.
          menu: {},
        },
      },
      WwModeSwitch: {
        styleOverrides: {
          root: {},
          // @ts-expect-error ModeSwitch only has the root slot.
          menu: {},
        },
      },
      WwGlowSwitch: {
        styleOverrides: {
          root: {},
          icon: {},
          label: {},
          switch: {},
          // @ts-expect-error GlowSwitch has no `track` slot.
          track: {},
        },
      },
      WwAccountPage: {
        styleOverrides: {
          root: {},
          // @ts-expect-error AccountPage has no `body` slot.
          body: {},
        },
      },
      WwNotFound: {
        styleOverrides: {
          root: {},
          // @ts-expect-error NotFound only has the root slot.
          title: {},
        },
      },
      WwPageLoading: {
        styleOverrides: {
          root: {},
          // @ts-expect-error PageLoading only has the root slot.
          spinner: {},
        },
      },
      WwUpdatePrompt: {
        styleOverrides: {
          root: {},
          reloadButton: {},
          // @ts-expect-error UpdatePrompt has no `laterButton` slot.
          laterButton: {},
        },
      },
      WwFormTextField: {
        styleOverrides: {
          root: {},
          // @ts-expect-error FormTextField only has the root slot.
          input: {},
        },
      },
      WwFormPasswordField: {
        styleOverrides: {
          root: {},
          toggleButton: {},
          // @ts-expect-error FormPasswordField has no `input` slot.
          input: {},
        },
      },
      WwSignInFields: {
        styleOverrides: {
          root: {},
          // @ts-expect-error SignInFields only has the root slot.
          submitButton: {},
        },
      },
    }),
  ).toBe(12)
})

it('rejects an unknown prop in a Ww component defaultProps', () => {
  expect(
    countKeys({
      // @ts-expect-error AppShell has no `colour` prop.
      WwAppShell: { defaultProps: { colour: 'red' } },
      // @ts-expect-error IconMenu has no `colour` prop.
      WwIconMenu: { defaultProps: { colour: 'red' } },
      // @ts-expect-error AccountPage has no `colour` prop.
      WwAccountPage: { defaultProps: { colour: 'red' } },
      // @ts-expect-error NotFound has no `colour` prop.
      WwNotFound: { defaultProps: { colour: 'red' } },
      // @ts-expect-error UpdatePrompt has no `colour` prop.
      WwUpdatePrompt: { defaultProps: { colour: 'red' } },
      // @ts-expect-error FormTextField has no `colour` prop.
      WwFormTextField: { defaultProps: { colour: 'red' } },
      // @ts-expect-error FormPasswordField has no `colour` prop.
      WwFormPasswordField: { defaultProps: { colour: 'red' } },
      // @ts-expect-error SignInFields has no `colour` prop.
      WwSignInFields: { defaultProps: { colour: 'red' } },
    }),
  ).toBe(8)
})

it('accepts a known prop with its type in a Ww component defaultProps', () => {
  expect(
    countKeys({
      WwAccountPage: { defaultProps: { title: 'Title' } },
      WwFormTextField: { defaultProps: { size: 'small', fullWidth: false } },
      WwSignInFields: { defaultProps: { busy: true } },
    }),
  ).toBe(3)
})

it('rejects any key in the defaultProps of the components that take no props', () => {
  expect(
    countKeys({
      // @ts-expect-error LanguageSwitch takes no props.
      WwLanguageSwitch: { defaultProps: { label: 'x' } },
      // @ts-expect-error ModeSwitch takes no props.
      WwModeSwitch: { defaultProps: { label: 'x' } },
      // @ts-expect-error GlowSwitch takes no props.
      WwGlowSwitch: { defaultProps: { label: 'x' } },
      // @ts-expect-error PageLoading takes no props.
      WwPageLoading: { defaultProps: { label: 'x' } },
    }),
  ).toBe(4)
})

it('accepts the wide breakpoint', () => {
  expect(theme.breakpoints.up('xxl')).toBe('@media (min-width:2400px)')
})
