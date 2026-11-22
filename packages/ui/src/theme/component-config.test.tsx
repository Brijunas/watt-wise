import BottomNavigation from '@mui/material/BottomNavigation'
import BottomNavigationAction from '@mui/material/BottomNavigationAction'
import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import Radio from '@mui/material/Radio'
import NativeSelect from '@mui/material/NativeSelect'
import Select from '@mui/material/Select'
import { screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
import {
  at,
  colorModes,
  renderThemed,
  resetThemeState,
  rootStyle,
} from '../testing/theme-test-utils'
import { theme } from './theme'

// The rendered text is a fixture, not translated UI, so these run language-neutral like
// alert-theme.test.tsx; the language-dependent states (form-group, switch, icon-menu) run in LT and EN.
afterEach(resetThemeState)

type Style = Record<string, unknown>

const baseline = (): Style => {
  const overrides = theme.components?.MuiCssBaseline?.styleOverrides
  const resolved = typeof overrides === 'function' ? overrides(theme) : overrides
  if (!resolved || typeof resolved !== 'object') throw new Error('Missing CssBaseline overrides')
  return resolved as Style
}

describe('icons', () => {
  // The icon test ids are the last resort: the icons are decorative and have no role.
  it('shows the Rounded unchecked and checked radio icons', () => {
    renderThemed(
      <>
        <Radio />
        <Radio checked onChange={() => undefined} />
      </>,
    )

    const [off, on] = screen.getAllByRole('radio')

    expect([
      within(off?.parentElement ?? document.body).queryAllByTestId(
        'RadioButtonUncheckedRoundedIcon',
      ).length,
      within(on?.parentElement ?? document.body).queryAllByTestId('RadioButtonCheckedRoundedIcon')
        .length,
    ]).toEqual([1, 1])
  })

  it.each(colorModes)('shows the ExpandMoreRounded arrow on a Select in %s mode', async (mode) => {
    const { container } = renderThemed(
      <Select value="a" aria-label="Tariff" onChange={() => undefined}>
        <MenuItem value="a">Alpha</MenuItem>
      </Select>,
      mode,
    )

    expect(container.querySelectorAll('[data-testid="ExpandMoreRoundedIcon"]')).toHaveLength(1)
    expect((await axe(container)).violations).toEqual([])
  })

  it.each(colorModes)('shows the Rounded checkbox icons in %s mode', async (mode) => {
    const { container } = renderThemed(
      <>
        <FormControlLabel label="Off" control={<Checkbox />} />
        <FormControlLabel label="On" control={<Checkbox checked onChange={() => undefined} />} />
        <FormControlLabel label="Some" control={<Checkbox indeterminate />} />
      </>,
      mode,
    )

    expect(
      [
        'CheckBoxOutlineBlankRoundedIcon',
        'CheckBoxRoundedIcon',
        'IndeterminateCheckBoxRoundedIcon',
      ].map((id) => container.querySelectorAll(`[data-testid="${id}"]`).length),
    ).toEqual([1, 1, 1])
    expect((await axe(container)).violations).toEqual([])
  })

  it.each(colorModes)(
    'shows the ExpandMoreRounded arrow on a NativeSelect in %s mode',
    async (mode) => {
      const { container } = renderThemed(
        <NativeSelect inputProps={{ 'aria-label': 'Tariff' }}>
          <option value="a">Alpha</option>
        </NativeSelect>,
        mode,
      )

      expect(container.querySelectorAll('[data-testid="ExpandMoreRoundedIcon"]')).toHaveLength(1)
      expect((await axe(container)).violations).toEqual([])
    },
  )

  it('keeps the TextField default variant outlined', () => {
    expect(theme.components?.MuiTextField?.defaultProps?.variant).toBe('outlined')
  })
})

describe('focus ring', () => {
  // Style-object cases read the theme's overrides; jsdom has no layout to measure.
  it('rings every ButtonBase 2px in primary at a 3px offset', () => {
    expect(at(rootStyle('MuiButtonBase'), '&.Mui-focusVisible')).toEqual({
      outline: '2px solid var(--mui-palette-primary-main, #6D28D9)',
      outlineOffset: 3,
    })
  })

  it('rings native links the same way', () => {
    expect(baseline()['a:not(.MuiButtonBase-root):focus-visible']).toEqual({
      outline: '2px solid var(--mui-palette-primary-main, #6D28D9)',
      outlineOffset: 3,
    })
  })

  it('draws the ring inside bottom navigation actions, list items and menu items', () => {
    expect([
      at(rootStyle('MuiBottomNavigationAction'), '&.Mui-focusVisible').outlineOffset,
      at(rootStyle('MuiListItemButton'), '&.Mui-focusVisible').outlineOffset,
      at(rootStyle('MuiMenuItem'), '&.Mui-focusVisible').outlineOffset,
    ]).toEqual([-4, -2, -2])
  })
})

describe('css baseline', () => {
  it('points the hover glow at the plain elevation when the glow is off', () => {
    expect(at(baseline(), 'html[data-glow="off"]')['--mui-glow-hover']).toBe(
      'var(--mui-glow-haloOff, 0 1px 2px rgba(var(--mui-palette-text-primaryChannel) / 0.06), 0 8px 24px -12px rgba(var(--mui-palette-text-primaryChannel) / 0.14))',
    )
  })

  it('makes every animation and transition instant under reduced motion', () => {
    expect(
      at(baseline(), '@media (prefers-reduced-motion: reduce)', '*, *::before, *::after'),
    ).toEqual({
      animationDuration: '0s !important',
      animationDelay: '0s !important',
      transitionDuration: '0s !important',
      transitionDelay: '0s !important',
    })
  })
})

describe('navigation indicators', () => {
  it('draws the bottom navigation indicator in Highlight under forced colours', () => {
    expect(
      at(
        rootStyle('MuiBottomNavigationAction'),
        '&.Mui-selected',
        '&::before',
        '@media (forced-colors: active)',
      ),
    ).toEqual({ forcedColorAdjust: 'none', backgroundImage: 'none', backgroundColor: 'Highlight' })
  })

  it('draws the list item indicator in Highlight under forced colours', () => {
    expect(
      at(
        rootStyle('MuiListItemButton'),
        '&.Mui-selected',
        '&::before',
        '@media (forced-colors: active)',
      ),
    ).toEqual({ forcedColorAdjust: 'none', backgroundImage: 'none', backgroundColor: 'Highlight' })
  })

  it.each(colorModes)(
    'has no accessibility violations for bottom navigation in %s mode',
    async (mode) => {
      const { container } = renderThemed(
        <BottomNavigation showLabels value={0} aria-label="Main">
          <BottomNavigationAction label="Prices" />
          <BottomNavigationAction label="Devices" />
        </BottomNavigation>,
        mode,
      )

      expect((await axe(container)).violations).toEqual([])
    },
  )
})

describe('card content', () => {
  it('pads 20px, and 24px from 600px up', () => {
    const style = rootStyle('MuiCardContent')

    expect([style.padding, at(style, '@media (min-width:600px)').padding]).toEqual([
      'calc(2.5 * var(--mui-spacing, 8px))',
      'calc(3 * var(--mui-spacing, 8px))',
    ])
  })
})

describe('theme composition', () => {
  it.each(colorModes)('keeps the %s colour scheme after enhanceHighContrast', (mode) => {
    expect(theme.colorSchemes[mode]?.palette.primary.main).toBe(
      mode === 'light' ? '#6D28D9' : '#A98BFF',
    )
  })

  it('keeps the CSS variables after enhanceHighContrast', () => {
    expect(theme.vars.palette.primary.main).toBe('var(--mui-palette-primary-main, #6D28D9)')
  })
})

const paper = /^var\(--mui-palette-background-paper(, [^)]+)?\)$/
const tint = /^rgba\(var\(--mui-palette-primary-mainChannel[^)]*\) \/ 0\.12\)$/

describe('surfaces', () => {
  it('gives bottom navigation a paper background and a divider top rule', () => {
    const style = rootStyle('MuiBottomNavigation')

    expect([style.backgroundColor, style.borderTop]).toEqual([
      expect.stringMatching(paper),
      '1px solid var(--mui-palette-divider, #E0DEEE)',
    ])
  })

  it('gives the drawer paper a paper background and no image', () => {
    const style = rootStyle('MuiDrawer', 'paper')

    expect([style.backgroundColor, style.backgroundImage]).toEqual([
      expect.stringMatching(paper),
      'none',
    ])
  })

  it('gives the menu paper a paper background, no image and a divider border', () => {
    const style = rootStyle('MuiMenu', 'paper')

    expect([style.backgroundColor, style.backgroundImage, style.border]).toEqual([
      expect.stringMatching(paper),
      'none',
      '1px solid var(--mui-palette-divider, #E0DEEE)',
    ])
  })

  it('gives the outlined input the field radius and the field outline colour', () => {
    expect([
      rootStyle('MuiOutlinedInput').borderRadius,
      rootStyle('MuiOutlinedInput', 'notchedOutline').borderColor,
    ]).toEqual(['var(--mui-shape-radius-field, 12px)', 'var(--mui-palette-fieldOutline, #7D7A98)'])
  })

  it('shows the selected menu item in primary text on a 12% tint', () => {
    const selected = at(rootStyle('MuiMenuItem'), '&.Mui-selected')

    expect(selected.color).toBe('var(--mui-palette-primary-main, #6D28D9)')
    expect(selected.backgroundColor).toMatch(tint)
  })
})

describe('navigation items', () => {
  it('positions the bottom navigation action and its selected indicator', () => {
    const root = rootStyle('MuiBottomNavigationAction')
    const selected = at(root, '&.Mui-selected')

    expect([root.position, selected.color]).toEqual([
      'relative',
      'var(--mui-palette-primary-main, #6D28D9)',
    ])
    expect(at(selected, '&::before')).toMatchObject({
      position: 'absolute',
      top: 0,
      left: '50%',
      transform: 'translateX(-50%)',
    })
  })

  it('positions the list item and its selected indicator, and lets the icon inherit the colour', () => {
    const root = rootStyle('MuiListItemButton')
    const selected = at(root, '&.Mui-selected')

    expect(root.position).toBe('relative')
    expect(at(selected, '&::before')).toMatchObject({ position: 'absolute', insetInlineStart: 0 })
    expect(at(selected, '& .MuiListItemIcon-root')).toEqual({ color: 'inherit' })
  })

  it('keeps the card content bottom padding equal to its padding at both widths', () => {
    const style = rootStyle('MuiCardContent')
    const wide = at(style, '@media (min-width:600px)')

    expect([
      at(style, '&:last-child').paddingBottom,
      at(wide, '&:last-child').paddingBottom,
    ]).toEqual(['calc(2.5 * var(--mui-spacing, 8px))', 'calc(3 * var(--mui-spacing, 8px))'])
  })
})
