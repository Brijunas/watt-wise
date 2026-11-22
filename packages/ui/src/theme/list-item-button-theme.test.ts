import { describe, expect, it } from 'vitest'
import { at, rootStyle } from '../testing/theme-test-utils'

const forcedColors = '@media (forced-colors: active)'
const focused = '&.Mui-focusVisible.Mui-focusVisible, &.Mui-selected.Mui-focusVisible'

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

  it('draws the non-forced focus ring without changing the item colours', () => {
    expect(Object.keys(at(rootStyle('MuiListItemButton'), '&.Mui-focusVisible'))).not.toContain(
      'backgroundColor',
    )
  })

  describe('a focus-visible item under forced colours (plain and selected)', () => {
    const rule = () => at(rootStyle('MuiListItemButton'), forcedColors, focused)

    it('is a Highlight fill with HighlightText text, shielded from the browser mixing colours', () => {
      expect([rule().forcedColorAdjust, rule().backgroundColor, rule().color]).toEqual([
        'none',
        'Highlight',
        'HighlightText',
      ])
    })

    it('draws a 2px HighlightText outline 4px inside the item', () => {
      expect([rule().outline, rule().outlineOffset]).toEqual(['2px solid HighlightText', -4])
    })

    it('inherits the HighlightText colour in the icon', () => {
      expect(at(rule(), '& .MuiListItemIcon-root')).toEqual({ color: 'inherit' })
    })

    it('draws the bar in HighlightText', () => {
      expect(at(rule(), '&::before')).toEqual({ backgroundColor: 'HighlightText' })
    })
  })
})
