import BottomNavigation from '@mui/material/BottomNavigation'
import BottomNavigationAction from '@mui/material/BottomNavigationAction'
import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import {
  at,
  emittedCss,
  renderThemed,
  resetThemeState,
  rootStyle,
} from '../testing/theme-test-utils'

afterEach(resetThemeState)

const forcedColors = '@media (forced-colors: active)'

describe('bottom navigation action theme', () => {
  it('replaces the gradient bar with a Highlight fill under forced colours', () => {
    const indicator = at(
      rootStyle('MuiBottomNavigationAction'),
      '&.Mui-selected',
      '&::before',
      forcedColors,
    )
    expect(indicator).toEqual({
      forcedColorAdjust: 'none',
      backgroundImage: 'none',
      backgroundColor: 'Highlight',
    })
  })

  it('draws the current-item bar with the brand gradient outside forced colours', () => {
    const indicator = at(rootStyle('MuiBottomNavigationAction'), '&.Mui-selected', '&::before')
    expect(indicator.backgroundImage).toMatch(/^var\(--mui-gradient-brand[,)]/)
  })

  it('lets the action shrink below MUI minimum of 80px so four actions fit at 320px', () => {
    expect(rootStyle('MuiBottomNavigationAction').minWidth).toBe(0)
  })

  it('draws the focus ring inside the action', () => {
    expect(at(rootStyle('MuiBottomNavigationAction'), '&.Mui-focusVisible').outlineOffset).toBe(-4)
  })

  it('keeps the selected label at 12px so choosing an item does not reflow the bar', () => {
    expect(rootStyle('MuiBottomNavigationAction', 'label')).toEqual({
      textAlign: 'center',
      overflowWrap: 'anywhere',
      '&.Mui-selected': { fontSize: '0.75rem' },
    })
  })

  it('emits the 12px selected label after MUI own 14px one', () => {
    renderThemed(
      <BottomNavigation showLabels value="a">
        <BottomNavigationAction value="a" label="Alpha" />
        <BottomNavigationAction value="b" label="Beta" />
      </BottomNavigation>,
    )
    const label = screen.getByText('Alpha')
    const cls = Array.from(label.classList).find((name) => name.startsWith('css-'))
    if (cls === undefined) throw new Error('Missing label class')
    const sizes = Array.from(
      emittedCss().matchAll(
        new RegExp('\\.' + cls + '\\.Mui-selected\\{[^}]*?font-size:([^;}]+)', 'g'),
      ),
    ).map((match) => match[1])
    expect(sizes[sizes.length - 1]).toBe('0.75rem')
  })
})
