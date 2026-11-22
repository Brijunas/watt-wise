import { describe, expect, it } from 'vitest'
import { at, rootStyle } from '../testing/theme-test-utils'

const forcedColors = '@media (forced-colors: active)'

describe('list item button theme (the rail and the drawer items)', () => {
  it('replaces the gradient bar with a Highlight fill under forced colours', () => {
    const indicator = at(
      rootStyle('MuiListItemButton'),
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
    const indicator = at(rootStyle('MuiListItemButton'), '&.Mui-selected', '&::before')
    expect(indicator.backgroundImage).toMatch(/^var\(--mui-gradient-brand[,)]/)
  })

  it('draws the focus ring inside the item so the drawer edge does not clip it', () => {
    expect(at(rootStyle('MuiListItemButton'), '&.Mui-focusVisible').outlineOffset).toBe(-2)
  })
})
