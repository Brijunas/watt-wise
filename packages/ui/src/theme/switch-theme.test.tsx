import FormControlLabel from '@mui/material/FormControlLabel'
import Switch from '@mui/material/Switch'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
import {
  at,
  emittedCss,
  colorModes,
  renderThemed,
  resetThemeState,
  rootStyle,
} from '../testing/theme-test-utils'

afterEach(resetThemeState)

const languages = [
  { language: 'en', label: 'Glow' },
  { language: 'lt', label: 'Švytėjimas' },
] as const

const labelled = (label: string, checked?: boolean) => (
  <FormControlLabel
    label={label}
    control={
      checked === undefined ? <Switch /> : <Switch checked={checked} onChange={() => undefined} />
    }
  />
)

describe('switch theme', () => {
  // Style-object cases read the theme's overrides; jsdom has no layout to measure.
  describe.each(languages)('in $language', ({ label }) => {
    it.each(colorModes)('has no accessibility violations unchecked in %s mode', async (mode) => {
      const { container } = renderThemed(labelled(label, false), mode)

      expect((await axe(container)).violations).toEqual([])
    })

    it.each(colorModes)('has no accessibility violations checked in %s mode', async (mode) => {
      const { container } = renderThemed(labelled(label, true), mode)

      expect((await axe(container)).violations).toEqual([])
    })

    it('toggles on Space', async () => {
      const user = userEvent.setup()
      renderThemed(labelled(label))

      await user.tab()
      await user.keyboard(' ')

      const toggle = screen.getByRole('switch', { name: label })

      expect(toggle instanceof HTMLInputElement && toggle.checked).toBe(true)
    })
  })

  it('draws the off track in the field outline colour at full opacity', () => {
    expect(at(rootStyle('MuiSwitch'), '& .MuiSwitch-track')).toEqual({
      backgroundColor: 'var(--mui-palette-fieldOutline, #7D7A98)',
      opacity: 1,
    })
  })

  it('draws the checked track as the brand gradient at full opacity', () => {
    const track = at(
      rootStyle('MuiSwitch'),
      '& .MuiSwitch-switchBase.Mui-checked:not(.Mui-disabled) + .MuiSwitch-track',
    )

    expect(track.opacity).toBe(1)
    expect(String(track.backgroundImage).startsWith('var(--mui-gradient-brand')).toBe(true)
  })

  it('gives way to system colours under forced colours', () => {
    expect(at(rootStyle('MuiSwitch'), '@media (forced-colors: active)')).toEqual({
      '& .MuiSwitch-switchBase.Mui-checked:not(.Mui-disabled) + .MuiSwitch-track': {
        backgroundImage: 'none',
        boxShadow: 'none',
      },
      '& .MuiSwitch-thumb': {
        forcedColorAdjust: 'none',
        backgroundColor: 'ButtonText',
        boxShadow: 'none',
      },
    })
  })

  it('gives a disabled checked switch no gradient track', () => {
    const keys = Object.keys(rootStyle('MuiSwitch')).filter((key) =>
      key.endsWith('+ .MuiSwitch-track'),
    )

    expect(keys).toEqual([
      '& .MuiSwitch-switchBase.Mui-checked:not(.Mui-disabled) + .MuiSwitch-track',
    ])
  })

  it('rings the whole switch root 2px in primary, 3px outside the medium track, with the chip radius', () => {
    expect(at(rootStyle('MuiSwitch'), '&:has(.Mui-focusVisible)')).toMatchObject({
      outline: '2px solid var(--mui-palette-primary-main, #6D28D9)',
      outlineOffset: '-9px',
      borderRadius: 'var(--mui-shape-radius-chip, 9999px)',
    })
  })

  it('rings a small switch 3px outside its track', () => {
    expect(
      at(rootStyle('MuiSwitch'), '&:has(.Mui-focusVisible)', '&.MuiSwitch-sizeSmall').outlineOffset,
    ).toBe('-4px')
  })

  it('turns the thumb base outline off so the ring is drawn once', () => {
    expect(at(rootStyle('MuiSwitch'), '& .MuiSwitch-switchBase.Mui-focusVisible')).toEqual({
      outline: 'none',
    })
  })

  it('emits a ring rule that matches the switch root while it has keyboard focus', async () => {
    const user = userEvent.setup()
    const { container } = renderThemed(labelled('Glow'))
    const root = container.querySelector('.MuiSwitch-root')
    const ring = (emittedCss().match(/[^{}]+\{[^{}]*outline:2px solid[^{}]*\}/g) ?? []).filter(
      (rule) => rule.includes(':has(.Mui-focusVisible)'),
    )
    const selector = (ring[0] ?? '').slice(0, (ring[0] ?? '').indexOf('{')).trim()

    const before = root?.matches(selector)
    await user.tab()
    const after = root?.matches(selector)

    expect([ring.length, before, after]).toEqual([1, false, true])
  })

  it('colours the checked thumb with the paper colour, not a disabled one', () => {
    expect(
      at(rootStyle('MuiSwitch'), '& .MuiSwitch-switchBase.Mui-checked:not(.Mui-disabled)').color,
    ).toMatch(/^var\(--mui-palette-background-paper(, [^)]+)?\)$/)
  })
})
