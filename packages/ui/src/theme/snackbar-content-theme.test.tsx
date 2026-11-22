import Snackbar from '@mui/material/Snackbar'
import { cleanup, screen } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { cssFor, renderThemed } from '../testing/theme-test-utils'

// jsdom ignores media queries and does not resolve the theme's CSS variables, so the look is read
// from the CSS Emotion emitted for the SnackbarContent. Its text is a language-neutral literal.
afterEach(cleanup)

const snackbarContent = () => {
  renderThemed(<Snackbar open message="Message" action={<button type="button">Action</button>} />)
  return screen.getByRole('alert')
}

const lastDeclaration = (css: string, property: string) =>
  Array.from(css.matchAll(new RegExp('[{;]' + property + ':([^;}]+)', 'g'))).at(-1)?.[1]

describe('snackbar content theme', () => {
  it.each([
    ['background-color', 'var(--mui-palette-background-paper)'],
    ['color', 'var(--mui-palette-text-primary)'],
    ['border', '1px solid var(--mui-palette-divider)'],
    ['border-radius', 'var(--mui-shape-radius-panel)'],
    ['box-shadow', 'var(--mui-glow-haloOff)'],
  ])('sets %s to %s', (property, value) => {
    expect(lastDeclaration(cssFor(snackbarContent()), property)).toBe(value)
  })

  it('puts one spacing unit between the action buttons (characterization: snackbar-content-theme.ts:16)', () => {
    const generated = Array.from(snackbarContent().classList).find((name) =>
      name.startsWith('css-'),
    )
    const rules = Array.from(document.querySelectorAll('style'))
      .map((tag) => tag.textContent)
      .filter((css) => css.includes('.' + (generated ?? '') + ' .MuiSnackbarContent-action{'))

    expect(rules.map((css) => lastDeclaration(css, 'gap'))).toEqual(['var(--mui-spacing)'])
  })

  it('draws a ButtonBorder border and no shadow in forced colours', () => {
    const css = cssFor(snackbarContent(), '(forced-colors: active)')

    expect([lastDeclaration(css, 'border'), lastDeclaration(css, 'box-shadow')]).toEqual([
      '1px solid ButtonBorder',
      'none',
    ])
  })
})
