import Alert from '@mui/material/Alert'
import { cleanup, render, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
import { cssFor } from '../testing/theme-test-utils'
import AppThemeProvider from './app-theme-provider'

// Pure icon mapping: the Alert text is a language-neutral literal, so no LT/EN run. The icons are
// decorative (no role), so the MUI icon test id is the last resort.
afterEach(cleanup)

const severities = [
  ['success', 'CheckCircleOutlineRoundedIcon'],
  ['info', 'InfoOutlineRoundedIcon'],
  ['warning', 'WarningAmberRoundedIcon'],
  ['error', 'ErrorOutlineRoundedIcon'],
] as const

const renderAlert = (severity: (typeof severities)[number][0]) =>
  render(
    <AppThemeProvider>
      <Alert severity={severity}>Message</Alert>
    </AppThemeProvider>,
  )

// jsdom ignores media queries and does not resolve the theme's CSS variables, so the look is read
// from the CSS Emotion emitted for the Alert: the last declaration of a property is the one that wins.
const lastDeclaration = (css: string, property: string) =>
  Array.from(css.matchAll(new RegExp('[{;]' + property + ':([^;}]+)', 'g'))).at(-1)?.[1]

const iconColor = (alert: Element) => {
  const generated = Array.from(alert.classList).find((name) => name.startsWith('css-'))
  const rules = Array.from(document.querySelectorAll('style'))
    .map((tag) => tag.textContent)
    .filter((css) => css.includes('.' + (generated ?? '') + ' .MuiAlert-icon{'))
    .join('\n')
  return lastDeclaration(rules, 'color')
}

const renderLook = (severity: (typeof severities)[number][0], variant?: 'filled') => {
  const { getByRole } = render(
    <AppThemeProvider>
      <Alert severity={severity} variant={variant}>
        Message
      </Alert>
    </AppThemeProvider>,
  )
  return getByRole('alert')
}

const standardLooks = [
  ['success', 'var(--mui-palette-success-tint)', 'var(--mui-palette-success-main)'],
  ['error', 'var(--mui-palette-error-tint)', 'var(--mui-palette-error-main)'],
  ['warning', 'var(--mui-palette-warning-tint)', 'var(--mui-palette-warning-main)'],
  ['info', 'var(--mui-palette-surface2)', 'var(--mui-palette-primary-main)'],
] as const

describe('alert look', () => {
  it.each(standardLooks)('puts the %s Alert on %s', (severity, background) => {
    const css = cssFor(renderLook(severity))

    expect(lastDeclaration(css, 'background-color')).toBe(background)
  })

  it.each(standardLooks)('colours the %s Alert icon with %s', (severity, _background, icon) => {
    expect(iconColor(renderLook(severity))).toBe(icon)
  })

  it.each(severities)('keeps text.primary on the standard %s Alert', (severity) => {
    expect(lastDeclaration(cssFor(renderLook(severity)), 'color')).toBe(
      'var(--mui-palette-text-primary)',
    )
  })

  it.each(severities)('uses the panel radius on the %s Alert', (severity) => {
    expect(lastDeclaration(cssFor(renderLook(severity)), 'border-radius')).toBe(
      'var(--mui-shape-radius-panel)',
    )
  })

  it.each([
    [
      'success',
      'var(--mui-palette-Alert-successFilledColor)',
      'var(--mui-palette-Alert-successFilledBg)',
    ],
    ['info', 'var(--mui-palette-Alert-infoFilledColor)', 'var(--mui-palette-Alert-infoFilledBg)'],
    [
      'warning',
      'var(--mui-palette-Alert-warningFilledColor)',
      'var(--mui-palette-Alert-warningFilledBg)',
    ],
    [
      'error',
      'var(--mui-palette-Alert-errorFilledColor)',
      'var(--mui-palette-Alert-errorFilledBg)',
    ],
  ] as const)(
    'leaves a filled %s Alert with its own text colour and background',
    (severity, color, background) => {
      const css = cssFor(renderLook(severity, 'filled'))

      expect([lastDeclaration(css, 'color'), lastDeclaration(css, 'background-color')]).toEqual([
        color,
        background,
      ])
    },
  )

  it.each(severities)(
    'draws a ButtonBorder border on the %s Alert in forced colours',
    (severity) => {
      expect(cssFor(renderLook(severity), '(forced-colors: active)')).toContain(
        'border:1px solid ButtonBorder;',
      )
    },
  )

  it.each(severities)('draws no border on the %s Alert otherwise', (severity) => {
    expect(lastDeclaration(cssFor(renderLook(severity)), 'border')).toBeUndefined()
  })
})

describe('alert theme', () => {
  it.each(severities)('shows the Rounded icon for the %s severity', (severity, testId) => {
    const { getByRole } = renderAlert(severity)

    expect(within(getByRole('alert')).getByTestId(testId).tagName).toBe('svg')
  })

  it.each(severities)('keeps the Alert icon size for the %s severity', (severity, testId) => {
    const { getByRole } = renderAlert(severity)

    const icon = within(getByRole('alert')).getByTestId(testId)

    expect(icon.classList.contains('MuiSvgIcon-fontSizeInherit')).toBe(true)
  })

  it.each(severities)('has no accessibility violations for the %s severity', async (severity) => {
    const { container } = renderAlert(severity)

    expect((await axe(container)).violations).toEqual([])
  })
})
