import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import FormGroup from '@mui/material/FormGroup'
import Radio from '@mui/material/Radio'
import RadioGroup from '@mui/material/RadioGroup'
import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
import {
  at,
  colorModes,
  emittedCss,
  renderThemed,
  resetThemeState,
  rootStyle,
  variantStyle,
} from '../testing/theme-test-utils'

afterEach(resetThemeState)

const languages = [
  { language: 'en', group: 'Show prices by', options: ['Hourly', 'Daily', 'Weekly'] },
  { language: 'lt', group: 'Rodyti kainas pagal', options: ['Valandinė', 'Dienos', 'Savaitės'] },
] as const

const segmented = (group: string, options: readonly string[], row = true) => (
  <RadioGroup row={row} aria-label={group} defaultValue="1">
    {options.map((option, index) => (
      <FormControlLabel key={option} value={String(index)} control={<Radio />} label={option} />
    ))}
  </RadioGroup>
)

// The emitted rules that turn a RadioGroup row into a pill track (the ones that make it `inline-flex`).
const trackSelectors = () =>
  (emittedCss().match(/[^{}]+\{[^{}]*display:inline-flex[^{}]*\}/g) ?? []).flatMap((rule) =>
    rule.slice(0, rule.indexOf('{')).split(','),
  )

const isChecked = (name: string) => {
  const radio = screen.getByRole('radio', { name })
  return radio instanceof HTMLInputElement && radio.checked
}

const matchingTrack = (element: Element | null) =>
  trackSelectors().filter((selector) => element?.matches(selector.trim()))

describe('segmented RadioGroup row', () => {
  describe.each(languages)('in $language', ({ group, options }) => {
    it.each(colorModes)('has no accessibility violations in %s mode', async (mode) => {
      const { container } = renderThemed(segmented(group, options), mode)

      expect((await axe(container)).violations).toEqual([])
    })

    it('enters once on the checked radio, then arrow keys move and select', async () => {
      const user = userEvent.setup()
      renderThemed(segmented(group, options))

      await user.tab()
      const focusedFirst = document.activeElement
      await user.keyboard('{ArrowRight}')
      const focusedSecond = document.activeElement

      expect([
        focusedFirst === screen.getByRole('radio', { name: options[1] }),
        focusedSecond === screen.getByRole('radio', { name: options[2] }),
        isChecked(options[2]),
        isChecked(options[1]),
      ]).toEqual([true, true, true, false])
    })

    it('leaves the group on the next Tab', async () => {
      const user = userEvent.setup()
      renderThemed(
        <>
          {segmented(group, options)}
          <button type="button">After</button>
        </>,
      )

      await user.tab()
      await user.tab()

      expect(screen.getByRole('button', { name: 'After' })).toBe(document.activeElement)
    })
  })

  it('styles a RadioGroup row as the pill track', () => {
    const { container } = renderThemed(segmented('Show prices by', ['Hourly', 'Daily']))

    expect(matchingTrack(container.querySelector('[role="radiogroup"]'))).toHaveLength(1)
  })

  it('leaves a checkbox FormGroup row without the pill track', () => {
    const { container } = renderThemed(
      <>
        {segmented('Show prices by', ['Hourly', 'Daily'])}
        <FormGroup row aria-label="Options">
          <FormControlLabel control={<Checkbox />} label="Weekends" />
          <FormControlLabel control={<Checkbox />} label="Holidays" />
        </FormGroup>
      </>,
    )

    expect(matchingTrack(container.querySelector('[aria-label="Options"]'))).toEqual([])
  })

  it('leaves a vertical RadioGroup as a plain list', () => {
    const { container } = renderThemed(
      <>
        {segmented('Show prices by', ['Hourly', 'Daily'])}
        {segmented('Plain', ['One', 'Two'], false)}
      </>,
    )

    expect(matchingTrack(container.querySelector('[aria-label="Plain"]'))).toEqual([])
  })

  it('fills the checked pill with primary and its contrast text', () => {
    const track = at(variantStyle(rootStyle('MuiFormGroup'), { row: true }), '&.MuiRadioGroup-root')
    const checked = at(track, '& .MuiFormControlLabel-root', '&:has(.Mui-checked)')

    expect(checked).toEqual({
      backgroundColor: 'var(--mui-palette-primary-main, #6D28D9)',
      color: 'var(--mui-palette-primary-contrastText, #fff)',
    })
  })

  it('fills the checked pill with Highlight under forced colours', () => {
    const track = at(variantStyle(rootStyle('MuiFormGroup'), { row: true }), '&.MuiRadioGroup-root')

    expect(at(track, '@media (forced-colors: active)')).toEqual({
      border: '1px solid ButtonBorder',
      '& .MuiFormControlLabel-root:has(.Mui-checked)': {
        forcedColorAdjust: 'none',
        backgroundColor: 'Highlight',
        color: 'HighlightText',
      },
    })
  })

  it('rings the pill that has keyboard focus', () => {
    const track = at(variantStyle(rootStyle('MuiFormGroup'), { row: true }), '&.MuiRadioGroup-root')

    expect(
      at(track, '& .MuiFormControlLabel-root', '&:has(.MuiRadio-root.Mui-focusVisible)'),
    ).toEqual({
      outline: '2px solid var(--mui-palette-primary-main, #6D28D9)',
      outlineOffset: 3,
    })
  })

  it('hides the radio icon but keeps the input over the whole pill', () => {
    const track = at(variantStyle(rootStyle('MuiFormGroup'), { row: true }), '&.MuiRadioGroup-root')

    expect(at(track, '& .MuiFormControlLabel-root', '& .MuiRadio-root')).toMatchObject({
      position: 'absolute',
      inset: 0,
      width: '100%',
      height: '100%',
      opacity: 0,
    })
  })

  const label = () =>
    at(
      variantStyle(rootStyle('MuiFormGroup'), { row: true }),
      '&.MuiRadioGroup-root',
      '& .MuiFormControlLabel-root',
    )

  it('makes each pill a positioned box with a bold label', () => {
    expect([label().position, at(label(), '& .MuiFormControlLabel-label').fontWeight]).toEqual([
      'relative',
      700,
    ])
  })

  it('tints an unchecked pill 12% primary on hover, inside the hover media query only', () => {
    const outside = JSON.stringify(label(), (key, value) =>
      key === '@media (hover: hover) and (pointer: fine)' ? undefined : (value as unknown),
    )

    expect(
      at(label(), '@media (hover: hover) and (pointer: fine)', '&:hover:not(:has(.Mui-checked))')
        .backgroundColor,
    ).toMatch(/^rgba\(var\(--mui-palette-primary-mainChannel[^)]*\) \/ 0\.12\)$/)
    expect(outside.includes(':hover')).toBe(false)
  })

  it('emits a hover tint rule that selects an unchecked pill and not the checked one', () => {
    const { container } = renderThemed(segmented('Show prices by', ['Hourly', 'Daily']))
    const rule =
      /@media \(hover: hover\) and \(pointer: fine\)\{([^{}]+)\{[^{}]*rgba[^{}]*\}\}/.exec(
        emittedCss(),
      )
    const selector = (rule?.[1] ?? '').replace(/:hover/, '').trim()
    const [unchecked, checked] = Array.from(container.querySelectorAll('.MuiFormControlLabel-root'))

    expect([unchecked?.matches(selector), checked?.matches(selector)]).toEqual([true, false])
  })

  it('keeps the track inside its container unwrapped, with each pill stretched and centred', () => {
    const track = at(variantStyle(rootStyle('MuiFormGroup'), { row: true }), '&.MuiRadioGroup-root')

    expect(track).toMatchObject({ maxWidth: '100%', flexWrap: 'nowrap' })
    expect(at(track, '& .MuiFormControlLabel-root')).toMatchObject({
      flex: '1 1 auto',
      justifyContent: 'center',
    })
  })
})
