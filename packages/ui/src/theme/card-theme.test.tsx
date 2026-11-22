import Card from '@mui/material/Card'
import CardActionArea from '@mui/material/CardActionArea'
import CardContent from '@mui/material/CardContent'
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

// The rendered text is a fixture, not translated UI, so these run language-neutral like
// alert-theme.test.tsx; the language-dependent states (form-group, switch, icon-menu) run in LT and EN.
afterEach(resetThemeState)

const hoverQuery = '@media (hover: hover) and (pointer: fine)'
const clickable = '&:is(a, button), &:has(.MuiCardActionArea-root)'

// Splits CSS into the text inside `@media (hover: hover) and (pointer: fine){…}` and the rest.
const splitHoverBlocks = (css: string) => {
  const start = '@media (hover: hover) and (pointer: fine){'
  let inside = ''
  let outside = ''
  let rest = css
  for (let at = rest.indexOf(start); at !== -1; at = rest.indexOf(start)) {
    outside += rest.slice(0, at)
    let depth = 1
    let end = at + start.length
    while (depth > 0) {
      const char = rest[end]
      if (char === '{') depth += 1
      if (char === '}') depth -= 1
      end += 1
    }
    inside += rest.slice(at + start.length, end - 1)
    rest = rest.slice(end)
  }
  return { inside, outside: outside + rest }
}

// The selectors of the card rules in the hover media query, without their `:hover`.
const hoverSelectors = (css: string) =>
  splitHoverBlocks(css)
    .inside.split('}')
    .filter((rule) => rule.includes('translateY(-2px)'))
    .flatMap((rule) => rule.slice(0, rule.indexOf('{')).split(/,(?![^(]*\))/))
    .map((selector) => selector.trim().replace(/:hover$/, ''))

describe('card theme', () => {
  // Hover cases read the CSS that Emotion emitted and match its selectors against the rendered cards.
  describe.each(colorModes)('in %s mode', (mode) => {
    it('has no accessibility violations for the default card', async () => {
      const { container } = renderThemed(
        <Card>
          <CardContent>Tariff</CardContent>
        </Card>,
        mode,
      )

      expect((await axe(container)).violations).toEqual([])
    })

    it('has no accessibility violations for the highlighted card', async () => {
      const { container } = renderThemed(
        <Card variant="highlighted">
          <CardContent>Cheapest</CardContent>
        </Card>,
        mode,
      )

      expect((await axe(container)).violations).toEqual([])
    })

    it('has no accessibility violations for the clickable card', async () => {
      const { container } = renderThemed(
        <Card>
          <CardActionArea>
            <CardContent>Open tariff</CardContent>
          </CardActionArea>
        </Card>,
        mode,
      )

      expect((await axe(container)).violations).toEqual([])
    })
  })

  it('lifts and glows a clickable card only inside the hover media query', () => {
    const root = rootStyle('MuiCard')
    const style = at(root, clickable)

    expect(at(style, hoverQuery, '&:hover')).toMatchObject({
      transform: 'translateY(-2px)',
      outlineOffset: -1,
      boxShadow:
        'var(--mui-glow-hover, 0 0 24px -8px rgba(var(--mui-palette-primary-mainChannel) / 0.25))',
    })
  })

  it('keeps every hover rule of the card theme inside the hover media query', () => {
    const outsideHover = JSON.stringify(rootStyle('MuiCard'), (key, value) =>
      key === hoverQuery || key === '@media (forced-colors: active)'
        ? undefined
        : (value as unknown),
    )

    expect(outsideHover.includes(':hover')).toBe(false)
  })

  it('emits the hover lift only inside the hover media query', () => {
    renderThemed(
      <Card>
        <CardActionArea>Open</CardActionArea>
      </Card>,
    )

    const { inside, outside } = splitHoverBlocks(emittedCss())

    expect(inside.includes('transform:translateY(-2px)')).toBe(true)
    expect(outside.includes('translateY(-2px)')).toBe(false)
  })

  it('selects a card with an action area for the hover rules', () => {
    const { container } = renderThemed(
      <Card>
        <CardActionArea>Open</CardActionArea>
      </Card>,
    )

    expect(hoverSelectors(emittedCss()).some((selector) => container.querySelector(selector))).toBe(
      true,
    )
  })

  it('selects no static card for the hover rules', () => {
    const { container } = renderThemed(<Card>Static</Card>)

    expect(
      hoverSelectors(emittedCss()).filter((selector) => container.querySelector(selector)),
    ).toEqual([])
  })

  it('renders the highlighted card with the gradient border over a padding-box fill', () => {
    const style = variantStyle(rootStyle('MuiCard'), { variant: 'highlighted' })

    expect(style).toMatchObject({
      border: '1.5px solid transparent',
      backgroundOrigin: 'padding-box, border-box',
      backgroundClip: 'padding-box, border-box',
    })
    expect(String(style.backgroundImage)).toContain('var(--mui-gradient-brand')
    expect(String(style.boxShadow)).toMatch(/^var\(--mui-glow-halo/)
  })

  it('draws the highlighted card with a Highlight border and no gradient or glow under forced colours', () => {
    const style = variantStyle(rootStyle('MuiCard'), { variant: 'highlighted' })

    expect(style['@media (forced-colors: active)']).toEqual({
      backgroundImage: 'none',
      boxShadow: 'none',
      border: '1.5px solid Highlight',
    })
  })

  it('drops the glow of a clickable card under forced colours', () => {
    const forced = at(rootStyle('MuiCard'), '@media (forced-colors: active)')

    expect(forced).toEqual({
      boxShadow: 'none',
      [clickable]: { '&:hover': { boxShadow: 'none' } },
    })
  })

  it('gives keyboard focus on a clickable card a 1px primary outline', () => {
    const style = at(rootStyle('MuiCard'), clickable, '&:focus-visible, &:has(.Mui-focusVisible)')

    expect(style).toMatchObject({
      outline: '1px solid var(--mui-palette-primary-main, #6D28D9)',
      outlineOffset: -1,
    })
  })

  it('draws a 1px divider border on a plain background with no image', () => {
    const root = rootStyle('MuiCard')

    expect(root.border).toBe('1px solid var(--mui-palette-divider, #E0DEEE)')
    expect(root.backgroundImage).toBe('none')
  })

  it('animates transform and box-shadow for the short duration (250ms)', () => {
    expect(at(rootStyle('MuiCard'), clickable).transition).toBe(
      'transform 250ms cubic-bezier(0.4, 0, 0.2, 1) 0ms,box-shadow 250ms cubic-bezier(0.4, 0, 0.2, 1) 0ms',
    )
  })

  it('turns the action area ring off so the card draws one ring, not two', () => {
    expect(at(rootStyle('MuiCard'), '& .MuiCardActionArea-root.Mui-focusVisible')).toEqual({
      outline: 'none',
    })
  })
})
