import { createI18n } from '@wattwise/i18n'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { I18nextProvider } from 'react-i18next'
import { afterEach, describe, expect, it, vi } from 'vitest'
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

  it('renders nothing when closed', () => {
    renderPrompt({ open: false })
    expect(screen.queryByText('A new version is available.')).toBeNull()
  })
})
