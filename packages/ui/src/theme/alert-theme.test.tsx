import Alert from '@mui/material/Alert'
import { cleanup, render, within } from '@testing-library/react'
import { afterEach, describe, expect, it } from 'vitest'
import { axe } from 'vitest-axe'
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
