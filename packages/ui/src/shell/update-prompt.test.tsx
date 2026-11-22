import { createI18n } from '@wattwise/i18n'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { I18nextProvider } from 'react-i18next'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { axe } from 'vitest-axe'
import { cssFor } from '../testing/theme-test-utils'
import AppThemeProvider from '../theme/app-theme-provider'
import UpdatePrompt from './update-prompt'

afterEach(() => {
  cleanup()
  localStorage.clear()
})

function renderPrompt({
  open = true,
  lng,
  onReload = vi.fn(),
  onClose = vi.fn(),
}: {
  open?: boolean
  lng?: 'en' | 'lt'
  onReload?: () => void
  onClose?: () => void
} = {}) {
  render(
    <I18nextProvider i18n={createI18n({ lng })}>
      <AppThemeProvider>
        <UpdatePrompt open={open} onReload={onReload} onClose={onClose} />
      </AppThemeProvider>
    </I18nextProvider>,
  )
}

describe('UpdatePrompt', () => {
  it('shows the message and both actions', () => {
    renderPrompt()
    expect(screen.getByText('A new version is available.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Reload' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Later' })).toBeTruthy()
  })

  it('is translated to Lithuanian', () => {
    renderPrompt({ lng: 'lt' })
    expect(screen.getByText('Yra nauja versija.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Atnaujinti' })).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Vėliau' })).toBeTruthy()
  })

  it('calls onReload and onClose from their buttons', async () => {
    const onReload = vi.fn()
    const onClose = vi.fn()
    renderPrompt({ onReload, onClose })
    await userEvent.click(screen.getByRole('button', { name: 'Reload' }))
    expect(onReload).toHaveBeenCalledTimes(1)
    expect(onClose).not.toHaveBeenCalled()
    await userEvent.click(screen.getByRole('button', { name: 'Later' }))
    expect(onClose).toHaveBeenCalledTimes(1)
  })

  // The prompt renders outside the shell, which publishes the insets on <html>; jsdom ignores media
  // queries, so these read the emitted CSS.
  describe('clears the shell bars through the inset variables', () => {
    const snackbarCss = (media?: string) => {
      const snackbar = screen.getByText('A new version is available.').closest('.MuiSnackbar-root')
      if (snackbar === null) throw new Error('Missing snackbar')
      return cssFor(snackbar, media)
    }

    it('sits above the bottom bar below 600px, with a 0px fallback', () => {
      renderPrompt()
      expect(snackbarCss()).toContain(
        'bottom:calc(var(--ww-shell-inset-bottom, 0px) + var(--mui-spacing));',
      )
    })

    it('sits 24px from the bottom from 600px', () => {
      renderPrompt()
      expect(snackbarCss('(min-width:600px)')).toContain('bottom:calc(3 * var(--mui-spacing));')
    })

    it('centres in the space beside the rail or drawer from 600px, with a 0px fallback', () => {
      renderPrompt()
      expect(snackbarCss('(min-width:600px)')).toContain(
        'left:calc(50% + var(--ww-shell-inset-start, 0px) / 2);',
      )
    })

    it('stays narrower than the space beside the rail or drawer from 600px', () => {
      renderPrompt()
      expect(snackbarCss('(min-width:600px)')).toContain(
        'max-width:calc(100vw - var(--ww-shell-inset-start, 0px) - calc(6 * var(--mui-spacing)));',
      )
    })
  })

  describe.each([
    ['en', 'A new version is available.', 'Reload', 'Later'],
    ['lt', 'Yra nauja versija.', 'Atnaujinti', 'Vėliau'],
  ] as const)('look in %s', (lng, message, reload, later) => {
    it('makes Reload the one contained button', () => {
      renderPrompt({ lng })
      expect(
        screen.getByRole('button', { name: reload }).classList.contains('MuiButton-contained'),
      ).toBe(true)
    })

    it('makes Later an outlined button', () => {
      renderPrompt({ lng })
      expect([
        screen.getByRole('button', { name: later }).classList.contains('MuiButton-outlined'),
        screen.getByRole('button', { name: later }).classList.contains('MuiButton-contained'),
      ]).toEqual([true, false])
    })

    it('announces the message as an alert', () => {
      renderPrompt({ lng })
      expect(screen.getByRole('alert').textContent).toContain(message)
    })

    it('calls onClose from Later and onReload from Reload', async () => {
      const onReload = vi.fn()
      const onClose = vi.fn()
      renderPrompt({ lng, onReload, onClose })
      await userEvent.click(screen.getByRole('button', { name: later }))
      expect([onClose.mock.calls.length, onReload.mock.calls.length]).toEqual([1, 0])
      await userEvent.click(screen.getByRole('button', { name: reload }))
      expect([onClose.mock.calls.length, onReload.mock.calls.length]).toEqual([1, 1])
    })

    it('has no accessibility violations when open', async () => {
      renderPrompt({ lng })
      expect((await axe(screen.getByRole('alert'))).violations).toEqual([])
    })
  })

  it('renders nothing when closed', () => {
    renderPrompt({ open: false })
    expect(screen.queryByText('A new version is available.')).toBeNull()
  })
})
