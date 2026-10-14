import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import renderWithProviders from '@/testing/test-utils'
import PwaUpdatePrompt from './pwa-update-prompt'

const registerSw = vi.hoisted(() => ({
  needRefresh: false,
  setNeedRefresh: vi.fn(),
  updateServiceWorker: vi.fn(() => Promise.resolve()),
}))

vi.mock('virtual:pwa-register/react', () => ({
  useRegisterSW: () => ({
    needRefresh: [registerSw.needRefresh, registerSw.setNeedRefresh],
    offlineReady: [false, vi.fn()],
    updateServiceWorker: registerSw.updateServiceWorker,
  }),
}))

describe('PwaUpdatePrompt', () => {
  it('shows nothing while no new version is waiting', () => {
    registerSw.needRefresh = false
    renderWithProviders(<PwaUpdatePrompt />)
    expect(screen.queryByRole('button', { name: 'Reload' })).toBeNull()
  })

  it('shows the prompt when a new version is waiting and reloads into it', async () => {
    registerSw.needRefresh = true
    renderWithProviders(<PwaUpdatePrompt />)
    expect(screen.getByText('A new version is available.')).toBeTruthy()
    await userEvent.click(screen.getByRole('button', { name: 'Reload' }))
    expect(registerSw.updateServiceWorker).toHaveBeenCalledWith(true)
  })

  it('hides the prompt on Later', async () => {
    registerSw.needRefresh = true
    renderWithProviders(<PwaUpdatePrompt />)
    await userEvent.click(screen.getByRole('button', { name: 'Later' }))
    expect(registerSw.setNeedRefresh).toHaveBeenCalledWith(false)
  })
})
