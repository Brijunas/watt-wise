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

  it.each([
    ['box-sizing', 'border-box'],
    ['min-width', '0'],
    ['max-width', '100%'],
    ['background-image', 'none'],
  ])('sets %s to %s so the panel fits the viewport and has no dark overlay', (property, value) => {
    expect(lastDeclaration(cssFor(snackbarContent()), property)).toBe(value)
  })

  it('keeps the 288px minimum from 600px, never wider than the Snackbar', () => {
    expect(lastDeclaration(cssFor(snackbarContent(), '(min-width:600px)'), 'min-width')).toBe(
      'min(288px, 100%)',
    )
  })

  describe('the action', () => {
    const actionRules = () => {
      const generated = Array.from(snackbarContent().classList).find((name) =>
        name.startsWith('css-'),
      )
      return Array.from(document.querySelectorAll('style'))
        .map((tag) => tag.textContent)
        .filter((css) => css.includes('.' + (generated ?? '') + ' .MuiSnackbarContent-action{'))
    }

    it('wraps under the message when the buttons do not fit', () => {
      expect(actionRules().map((css) => lastDeclaration(css, 'flex-wrap'))).toEqual(['wrap'])
    })

    // characterization: snackbar-content-theme.ts:30, `theme.spacing(1)`
    it('puts one spacing unit between the action buttons', () => {
      expect(actionRules().map((css) => lastDeclaration(css, 'gap'))).toEqual([
        'var(--mui-spacing)',
      ])
    })

    // characterization: snackbar-content-theme.ts:31-32, `theme.spacing(1)`
    it.each(['padding-top', 'padding-bottom'])(
      'gives the action one spacing unit of %s',
      (property) => {
        expect(actionRules().map((css) => lastDeclaration(css, property))).toEqual([
          'var(--mui-spacing)',
        ])
      },
    )
  })

  it('draws a ButtonBorder border and no shadow in forced colours', () => {
    const css = cssFor(snackbarContent(), '(forced-colors: active)')

    expect([lastDeclaration(css, 'border'), lastDeclaration(css, 'box-shadow')]).toEqual([
      '1px solid ButtonBorder',
      'none',
    ])
  })
})
